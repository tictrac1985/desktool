using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.Logic;

namespace DeskMate.Features
{
    /// <summary>Holds the exchange rates for the calculator's currency ribbon and the
    /// command bar. Rates are fetched only when the converter is actually used, at most
    /// twice a day, and kept in %AppData%\DeskMate\rates.json — so the converter still
    /// answers offline, with the date of the figures it is using always on show.</summary>
    public static class CurrencyFeature
    {
        static CurrencyRates _rates;
        static bool _cacheRead;
        static int _busy;

        static string CachePath { get { return Path.Combine(Log.Dir, "rates.json"); } }

        /// <summary>The currency a bare amount converts into: the user's pick, or — until
        /// they make one — the currency of the country the widget is located in.</summary>
        public static string HomeCurrency
        {
            get
            {
                string to = Settings.I.CurTo;
                return string.IsNullOrEmpty(to) ? CurrencyCore.ForCountry(Settings.I.Country) : to;
            }
        }

        /// <summary>Last known rates (memory, then disk). Never touches the network.</summary>
        public static CurrencyRates Cached
        {
            get
            {
                if (_rates == null && !_cacheRead)
                {
                    _cacheRead = true;
                    try
                    {
                        if (File.Exists(CachePath))
                        {
                            var fi = new FileInfo(CachePath);
                            if (fi.Length < 64 * 1024) _rates = CurrencyCore.FromCache(File.ReadAllText(CachePath));
                        }
                    }
                    catch (Exception ex) { Log.Error("rates cache", ex); }
                }
                return _rates;
            }
        }

        /// <summary>Hands <paramref name="done"/> the best rates available — at once when
        /// they are fresh; otherwise at once with the stale ones (if any) and again when the
        /// refresh lands. <paramref name="done"/> always runs on the dispatcher's thread; its
        /// second argument is true while a refresh is still on its way.</summary>
        public static void Ensure(Dispatcher ui, Action<CurrencyRates, bool> done, bool force = false)
        {
            var have = Cached;
            if (!force && CurrencyCore.IsFresh(have, DateTime.UtcNow)) { done(have, false); return; }
            done(have, true);
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
            Task.Run(() =>
            {
                CurrencyRates fresh = null;
                foreach (string url in new[] { CurrencyCore.PrimaryUrl, CurrencyCore.FallbackUrl })
                {
                    try
                    {
                        fresh = CurrencyCore.Parse(Http.Get(url, 9000), DateTime.UtcNow);
                        if (fresh != null) break;
                    }
                    catch (Exception ex) { Log.Write("rates " + new Uri(url).Host + ": " + ex.Message); }
                }
                if (fresh != null)
                {
                    _rates = fresh;
                    try { File.WriteAllText(CachePath, CurrencyCore.ToCache(fresh)); }
                    catch (Exception ex) { Log.Error("rates save", ex); }
                }
                Interlocked.Exchange(ref _busy, 0);
                ui.BeginInvoke(new Action(() => done(fresh ?? _rates, false)));
            });
        }
    }
}
