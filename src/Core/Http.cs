using System;
using System.IO;
using System.Net;

namespace DeskMate.Core
{
    /// <summary>The one place DeskMate talks to the network. Small text GETs (weather,
    /// geocoding, exchange rates, the update manifest) and one bounded binary download
    /// (the update itself). Every call is HTTPS-capable, size-capped and time-limited:
    /// nothing a server sends is ever buffered without a ceiling.</summary>
    public static class Http
    {
        /// <summary>Largest text response read by <see cref="Get"/>. The endpoints used here
        /// answer with a few KB; anything bigger is a hostile or broken server.</summary>
        public const int MaxTextBytes = 256 * 1024;

        static bool _tlsReady;

        /// <summary>SystemDefault (the .NET 4.8 default) is the right choice in principle —
        /// Windows then picks the best protocol, future versions included. In the field it
        /// fails on some machines ("Could not create SSL/TLS secure channel"), so 1.2 and 1.3
        /// are enabled explicitly as a floor. The two are enabled in separate attempts on
        /// purpose: TLS 1.3 (12288) is not a defined value on every patch level, and a single
        /// combined assignment that threw left the process with neither.</summary>
        public static void EnsureTls()
        {
            if (_tlsReady) return;
            _tlsReady = true;
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch (Exception ex) { Log.Error("tls12", ex); }
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288; }
            catch { /* TLS 1.3 unknown to this OS/runtime — 1.2 above is enough */ }
        }

        /// <summary>Minutes to wait after the n-th consecutive failure of a background
        /// refresh: 2, 4, 8, 16, then 30. Keeps an offline machine from retrying (and
        /// logging) every single minute.</summary>
        public static int BackoffMinutes(int failures)
        {
            if (failures <= 0) return 0;
            int m = 1 << Math.Min(failures, 5);
            return Math.Min(30, m);
        }

        static HttpWebRequest Create(string url, int timeoutMs)
        {
            EnsureTls();
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.UserAgent = "DeskMate/" + AppVersion.Text;   // one version source, not a literal
            req.AllowAutoRedirect = true;
            req.MaximumAutomaticRedirections = 3;
            return req;
        }

        /// <summary>GET a small text document. Throws on any failure.</summary>
        public static string Get(string url, int timeoutMs = 8000)
        {
            return Get(url, timeoutMs, null);
        }

        /// <summary>GET a small text document, and — when <paramref name="hostOk"/> is given —
        /// refuse the answer unless the request ended, after any redirects, on HTTPS at a host
        /// the caller trusts. The update manifest is read this way.</summary>
        public static string Get(string url, int timeoutMs, Func<string, bool> hostOk)
        {
            var req = Create(url, timeoutMs);
            using (var resp = (HttpWebResponse)req.GetResponse())
            {
                CheckFinalHost(resp, hostOk);
                using (var stream = resp.GetResponseStream())
                {
                    if (stream == null) return "";
                    var buffer = new char[8192];
                    var sb = new System.Text.StringBuilder();
                    using (var reader = new StreamReader(stream))
                    {
                        int read;
                        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            sb.Append(buffer, 0, read);
                            if (sb.Length > MaxTextBytes)
                                throw new InvalidOperationException("response exceeds " + MaxTextBytes + " bytes");
                        }
                    }
                    return sb.ToString();
                }
            }
        }

        static void CheckFinalHost(HttpWebResponse resp, Func<string, bool> hostOk)
        {
            if (hostOk == null) return;
            var final = resp.ResponseUri;
            if (final == null || final.Scheme != Uri.UriSchemeHttps || !hostOk(final.Host))
                throw new InvalidOperationException("response came from an untrusted address");
        }

        /// <summary>Streams a binary download to <paramref name="path"/>. Stops — and deletes
        /// the partial file — the moment the body passes <paramref name="maxBytes"/>, the
        /// transfer fails, or <paramref name="cancelled"/> answers true. Returns bytes written.</summary>
        public static long Download(string url, string path, long maxBytes, Func<string, bool> hostOk,
                                    Action<long, long> progress, Func<bool> cancelled, int timeoutMs = 30000)
        {
            var req = Create(url, timeoutMs);
            long total = 0;
            bool done = false;
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    CheckFinalHost(resp, hostOk);
                    long declared = resp.ContentLength;
                    if (declared > maxBytes) throw new InvalidOperationException("download larger than allowed");
                    using (var stream = resp.GetResponseStream())
                    using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        if (stream == null) throw new InvalidOperationException("empty response");
                        var buf = new byte[64 * 1024];
                        int n;
                        while ((n = stream.Read(buf, 0, buf.Length)) > 0)
                        {
                            total += n;
                            if (total > maxBytes) throw new InvalidOperationException("download larger than allowed");
                            if (cancelled != null && cancelled()) throw new OperationCanceledException();
                            file.Write(buf, 0, n);
                            if (progress != null) progress(total, declared);
                        }
                    }
                }
                done = true;
                return total;
            }
            finally
            {
                if (!done) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
            }
        }
    }
}
