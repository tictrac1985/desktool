using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DeskMate.Core;

namespace DeskMate.Logic
{
    /// <summary>What the update channel says is current. Written by the GitHub workflow
    /// (see .github/workflows/build.yml) next to the binaries it describes.</summary>
    public class UpdateManifest
    {
        /// <summary>Four-part version of the published build, e.g. "1.12.0.34".</summary>
        public string Version = "";
        /// <summary>UTC time the build was published (ISO 8601). Display only.</summary>
        public string Published = "";
        /// <summary>One or two lines describing the change. Display only.</summary>
        public string Notes = "";
        /// <summary>Portable app: file name on the channel, exact size and SHA-256.</summary>
        public string File = "";
        public long Size;
        public string Sha256 = "";
        /// <summary>Installer, for machines where the app cannot replace itself.</summary>
        public string SetupFile = "";
        public long SetupSize;
        public string SetupSha256 = "";
    }

    /// <summary>Everything about self-update that can be decided without touching the
    /// network or the disk: where the channel lives, how versions compare, what a valid
    /// manifest looks like, which hosts may answer, and how a download is verified.
    /// Kept pure so each rule is pinned by SelfTest — this is the code path that ends
    /// with the app replacing its own executable.</summary>
    public static class UpdateCore
    {
        // The channel is a branch of the project's own repository. The workflow rebuilds it
        // (one commit: latest.json + the two executables) after every successful build of
        // main, so "push the code" is the whole release process.
        public const string Owner = "tictrac1985";
        public const string Repo = "desktool";
        public const string Branch = "updates";
        public const string ManifestName = "latest.json";

        /// <summary>Hard ceiling for a downloaded package. The app is under 1 MB; a manifest
        /// claiming hundreds of megabytes is wrong or hostile.</summary>
        public const long MaxPackageBytes = 48L * 1024 * 1024;

        public static string ChannelBase
        {
            get { return "https://raw.githubusercontent.com/" + Owner + "/" + Repo + "/" + Branch + "/"; }
        }
        public static string ManifestUrl { get { return ChannelBase + ManifestName; } }
        public static string ProjectPage { get { return "https://github.com/" + Owner + "/" + Repo; } }
        public static string ReleasesPage { get { return ProjectPage + "/releases/latest"; } }

        /// <summary>URL of a file on the channel, or null when the name is not one the
        /// channel could legitimately carry. The manifest never supplies a URL — only a file
        /// name — so a tampered manifest cannot point the download anywhere else.</summary>
        public static string FileUrl(string fileName)
        {
            return IsSafeFileName(fileName) ? ChannelBase + fileName : null;
        }

        static readonly Regex SafeName = new Regex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}\.exe$", RegexOptions.CultureInvariant);

        public static bool IsSafeFileName(string name)
        {
            return !string.IsNullOrEmpty(name) && SafeName.IsMatch(name) && name.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        /// <summary>Hosts a channel request may end on after redirects. GitHub serves raw
        /// files itself and release assets from its object store.</summary>
        public static bool IsTrustedHost(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            host = host.ToLowerInvariant();
            return host == "raw.githubusercontent.com"
                || host == "github.com"
                || host == "objects.githubusercontent.com"
                || host == "release-assets.githubusercontent.com";
        }

        // ---------------- versions ----------------

        /// <summary>Parses "1.12.0", "v1.12.0.34" and the like into four numeric parts.
        /// Missing parts count as zero; anything non-numeric fails.</summary>
        public static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            if (t.Length > 0 && (t[0] == 'v' || t[0] == 'V')) t = t.Substring(1);
            var parts = t.Split('.');
            if (parts.Length < 2 || parts.Length > 4) return false;
            var n = new int[4];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || parts[i].Length > 9) return false;
                foreach (char c in parts[i]) if (c < '0' || c > '9') return false;
                n[i] = int.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
            }
            version = new Version(n[0], n[1], n[2], n[3]);
            return true;
        }

        /// <summary>True only when both parse and <paramref name="remote"/> is strictly
        /// greater. An unreadable version is never "newer": the app must not replace itself
        /// on the strength of a string it cannot understand, and never downgrades.</summary>
        public static bool IsNewer(string remote, string local)
        {
            Version r, l;
            if (!TryParseVersion(remote, out r) || !TryParseVersion(local, out l)) return false;
            return r > l;
        }

        /// <summary>"1.12.0.34" → "1.12.0 (34)" — the form shown to the user.</summary>
        public static string Display(string fourPart)
        {
            Version v;
            if (!TryParseVersion(fourPart, out v)) return fourPart ?? "";
            string three = v.Major + "." + v.Minor + "." + v.Build;
            return v.Revision > 0 ? three + " (" + v.Revision + ")" : three;
        }

        // ---------------- manifest ----------------

        static readonly Regex Hex64 = new Regex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);

        /// <summary>Reads latest.json. Returns null unless every field needed to download and
        /// verify the portable build is present and well-formed — a half-valid manifest is
        /// treated as no manifest at all.</summary>
        public static UpdateManifest ParseManifest(string json)
        {
            Dictionary<string, object> d;
            try { d = Json.Parse(json) as Dictionary<string, object>; }
            catch { return null; }
            if (d == null) return null;

            var m = new UpdateManifest
            {
                Version = Json.GetStr(d, "version", ""),
                Published = Clip(Json.GetStr(d, "published", ""), 40),
                Notes = Clip(Json.GetStr(d, "notes", ""), 400),
                File = Json.GetStr(d, "file", ""),
                Size = (long)Json.GetNum(d, "size", 0),
                Sha256 = Json.GetStr(d, "sha256", ""),
                SetupFile = Json.GetStr(d, "setupFile", ""),
                SetupSize = (long)Json.GetNum(d, "setupSize", 0),
                SetupSha256 = Json.GetStr(d, "setupSha256", ""),
            };
            Version v;
            if (!TryParseVersion(m.Version, out v)) return null;
            if (!IsSafeFileName(m.File)) return null;
            if (m.Size <= 0 || m.Size > MaxPackageBytes) return null;
            if (!Hex64.IsMatch(m.Sha256)) return null;
            // the installer is optional; a broken entry is dropped rather than failing the lot
            if (!IsSafeFileName(m.SetupFile) || m.SetupSize <= 0 || m.SetupSize > MaxPackageBytes
                || !Hex64.IsMatch(m.SetupSha256))
            {
                m.SetupFile = ""; m.SetupSize = 0; m.SetupSha256 = "";
            }
            return m;
        }

        static string Clip(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        /// <summary>Writes a manifest in the exact shape <see cref="ParseManifest"/> reads —
        /// used by the tests to prove the two stay in step with the workflow's output.</summary>
        public static string WriteManifest(UpdateManifest m)
        {
            var d = new Dictionary<string, object>
            {
                { "version", m.Version }, { "published", m.Published }, { "notes", m.Notes },
                { "file", m.File }, { "size", (double)m.Size }, { "sha256", m.Sha256 },
                { "setupFile", m.SetupFile }, { "setupSize", (double)m.SetupSize }, { "setupSha256", m.SetupSha256 },
            };
            return Json.Write(d);
        }

        // ---------------- verification ----------------

        public static string Sha256Hex(Stream stream)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static string Sha256Hex(byte[] data)
        {
            using (var ms = new MemoryStream(data)) return Sha256Hex(ms);
        }

        public static bool HashEquals(string a, string b)
        {
            return !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
                && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The first bytes of a Windows executable: "MZ", and room for a PE header.</summary>
        public static bool LooksLikeExe(byte[] head)
        {
            return head != null && head.Length >= 64 && head[0] == (byte)'M' && head[1] == (byte)'Z';
        }

        /// <summary>Verdict on a downloaded file against the manifest that announced it.
        /// Returns null when the file is exactly what was promised, otherwise a short reason
        /// (for the log — the user only sees "the download could not be verified").</summary>
        public static string VerifyPackage(string path, long expectedSize, string expectedSha256)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return "missing";
                if (fi.Length != expectedSize) return "size " + fi.Length + " != " + expectedSize;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var head = new byte[64];
                    int n = fs.Read(head, 0, head.Length);
                    if (n < head.Length || !LooksLikeExe(head)) return "not an executable";
                    fs.Position = 0;
                    string hash = Sha256Hex(fs);
                    if (!HashEquals(hash, expectedSha256)) return "sha256 mismatch";
                }
                return null;
            }
            catch (Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
        }

        // ---------------- scheduling ----------------

        /// <summary>Is an automatic check due? <paramref name="lastCheckUtc"/> is the stored
        /// ISO timestamp of the previous one ("" = never). A timestamp from the future (clock
        /// was wrong when it was written) counts as due rather than postponing checks forever.</summary>
        public static bool DueForCheck(string lastCheckUtc, DateTime utcNow, double everyHours)
        {
            if (string.IsNullOrEmpty(lastCheckUtc)) return true;
            DateTime last;
            if (!DateTime.TryParse(lastCheckUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out last))
                return true;
            if (last > utcNow.AddMinutes(5)) return true;
            return (utcNow - last).TotalHours >= everyHours;
        }

        public static string Stamp(DateTime utcNow)
        {
            return utcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
