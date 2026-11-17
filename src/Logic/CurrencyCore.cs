using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using DeskMate.Core;

namespace DeskMate.Logic
{
    public class CurrencyInfo
    {
        public string Code, NameAr, NameEn;
        public CurrencyInfo(string code, string ar, string en) { Code = code; NameAr = ar; NameEn = en; }
    }

    /// <summary>A snapshot of exchange rates, all expressed as "units per one US dollar".</summary>
    public class CurrencyRates
    {
        public readonly Dictionary<string, double> PerUsd = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        /// <summary>The provider's own date for the figures (yyyy-MM-dd), "" when unknown.</summary>
        public string Date = "";
        public DateTime FetchedUtc = DateTime.MinValue;
    }

    /// <summary>Currency conversion for the calculator and the command bar: the list of
    /// currencies offered, parsing of the two rate feeds, the arithmetic, the on-disk cache
    /// format, and recognising a typed phrase such as "100 دولار كم ريال". No network here —
    /// Features.CurrencyFeature fetches, this decides — so every rule is unit-tested.</summary>
    public static class CurrencyCore
    {
        /// <summary>Primary feed: a static daily file on a CDN, no key, no rate limit, 300+
        /// currencies including the Gulf and North-African ones. Fallback: a second
        /// independent provider, same idea. Both are keyed on the US dollar.</summary>
        public const string PrimaryUrl = "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/usd.min.json";
        public const string FallbackUrl = "https://open.er-api.com/v6/latest/USD";

        /// <summary>Rates older than this are refreshed when the converter is used.</summary>
        public const double FreshHours = 12;

        // Order matters: it is the order of the pickers. The currencies an Arabic-speaking
        // user reaches for first lead; the rest follow by how often they are traded.
        public static readonly CurrencyInfo[] All =
        {
            new CurrencyInfo("SAR", "ريال سعودي", "Saudi riyal"),
            new CurrencyInfo("EGP", "جنيه مصري", "Egyptian pound"),
            new CurrencyInfo("USD", "دولار أمريكي", "US dollar"),
            new CurrencyInfo("EUR", "يورو", "Euro"),
            new CurrencyInfo("GBP", "جنيه إسترليني", "British pound"),
            new CurrencyInfo("AED", "درهم إماراتي", "UAE dirham"),
            new CurrencyInfo("KWD", "دينار كويتي", "Kuwaiti dinar"),
            new CurrencyInfo("QAR", "ريال قطري", "Qatari riyal"),
            new CurrencyInfo("BHD", "دينار بحريني", "Bahraini dinar"),
            new CurrencyInfo("OMR", "ريال عُماني", "Omani rial"),
            new CurrencyInfo("JOD", "دينار أردني", "Jordanian dinar"),
            new CurrencyInfo("IQD", "دينار عراقي", "Iraqi dinar"),
            new CurrencyInfo("LBP", "ليرة لبنانية", "Lebanese pound"),
            new CurrencyInfo("SYP", "ليرة سورية", "Syrian pound"),
            new CurrencyInfo("YER", "ريال يمني", "Yemeni rial"),
            new CurrencyInfo("LYD", "دينار ليبي", "Libyan dinar"),
            new CurrencyInfo("TND", "دينار تونسي", "Tunisian dinar"),
            new CurrencyInfo("DZD", "دينار جزائري", "Algerian dinar"),
            new CurrencyInfo("MAD", "درهم مغربي", "Moroccan dirham"),
            new CurrencyInfo("SDG", "جنيه سوداني", "Sudanese pound"),
            new CurrencyInfo("TRY", "ليرة تركية", "Turkish lira"),
            new CurrencyInfo("INR", "روبية هندية", "Indian rupee"),
            new CurrencyInfo("PKR", "روبية باكستانية", "Pakistani rupee"),
            new CurrencyInfo("CNY", "يوان صيني", "Chinese yuan"),
            new CurrencyInfo("JPY", "ين ياباني", "Japanese yen"),
            new CurrencyInfo("CHF", "فرنك سويسري", "Swiss franc"),
            new CurrencyInfo("CAD", "دولار كندي", "Canadian dollar"),
            new CurrencyInfo("AUD", "دولار أسترالي", "Australian dollar"),
            new CurrencyInfo("MYR", "رينغيت ماليزي", "Malaysian ringgit"),
            new CurrencyInfo("IDR", "روبية إندونيسية", "Indonesian rupiah"),
            new CurrencyInfo("RUB", "روبل روسي", "Russian ruble"),
        };

        public static CurrencyInfo Find(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (var c in All) if (string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        public static int IndexOf(string code)
        {
            for (int i = 0; i < All.Length; i++)
                if (string.Equals(All[i].Code, code, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>The home currency of an ISO country code — the natural "convert to"
        /// default. Unknown countries get the Saudi riyal, the app's home ground.</summary>
        public static string ForCountry(string iso2)
        {
            switch ((iso2 ?? "").ToUpperInvariant())
            {
                case "SA": return "SAR";
                case "EG": return "EGP";
                case "AE": return "AED";
                case "KW": return "KWD";
                case "QA": return "QAR";
                case "BH": return "BHD";
                case "OM": return "OMR";
                case "JO": return "JOD";
                case "IQ": return "IQD";
                case "LB": return "LBP";
                case "SY": return "SYP";
                case "YE": return "YER";
                case "LY": return "LYD";
                case "TN": return "TND";
                case "DZ": return "DZD";
                case "MA": return "MAD";
                case "SD": return "SDG";
                case "TR": return "TRY";
                case "IN": return "INR";
                case "PK": return "PKR";
                case "CN": return "CNY";
                case "JP": return "JPY";
                case "CH": return "CHF";
                case "CA": return "CAD";
                case "AU": return "AUD";
                case "MY": return "MYR";
                case "ID": return "IDR";
                case "RU": return "RUB";
                case "GB": return "GBP";
                case "US": return "USD";
                case "DE": case "FR": case "IT": case "ES": case "NL": case "BE": case "AT":
                case "IE": case "PT": case "GR": case "FI":
                    return "EUR";
                default: return "SAR";
            }
        }

        // ---------------- feeds ----------------

        /// <summary>Reads either feed. Primary shape: {"date":"…","usd":{"sar":3.75,…}}.
        /// Fallback shape: {"result":"success","rates":{"SAR":3.75,…}}. Returns null unless
        /// the document carries a believable table — at least a handful of positive, finite
        /// rates including the dollar itself at (about) one.</summary>
        public static CurrencyRates Parse(string json, DateTime fetchedUtc)
        {
            Dictionary<string, object> d;
            try { d = Json.Parse(json) as Dictionary<string, object>; }
            catch { return null; }
            if (d == null) return null;

            var table = Json.GetObj(d, "usd");
            if (table == null || table.Count == 0) table = Json.GetObj(d, "rates");
            if (table == null || table.Count == 0) return null;

            var r = new CurrencyRates { FetchedUtc = fetchedUtc };
            foreach (var kv in table)
            {
                if (!(kv.Value is double)) continue;
                double v = (double)kv.Value;
                if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0) continue;
                string code = kv.Key.ToUpperInvariant();
                if (code.Length != 3) continue;                 // skips crypto tickers and oddities
                r.PerUsd[code] = v;
            }
            if (!r.PerUsd.ContainsKey("USD")) r.PerUsd["USD"] = 1.0;
            if (r.PerUsd.Count < 10 || Math.Abs(r.PerUsd["USD"] - 1.0) > 0.001) return null;

            string date = Json.GetStr(d, "date", "");
            if (date.Length == 0)
            {
                double unix = Json.GetNum(d, "time_last_update_unix", 0);
                if (unix > 0)
                    date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix)
                        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            r.Date = date.Length > 10 ? date.Substring(0, 10) : date;
            return r;
        }

        /// <summary>amount × (to per USD) ÷ (from per USD). NaN when either side is unknown.</summary>
        public static double Convert(CurrencyRates r, double amount, string from, string to)
        {
            if (r == null || from == null || to == null) return double.NaN;
            double f, t;
            if (!r.PerUsd.TryGetValue(from, out f) || !r.PerUsd.TryGetValue(to, out t) || f <= 0) return double.NaN;
            return amount * t / f;
        }

        /// <summary>Money as people read it: thousands separated, two decimals — but enough
        /// digits that a small figure (0.0186 dinar) does not round away to nothing.</summary>
        public static string Format(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "—";
            double a = Math.Abs(v);
            string fmt = a >= 1 || a == 0 ? "#,##0.##" : a >= 0.01 ? "0.####" : "0.######";
            return v.ToString(fmt, CultureInfo.InvariantCulture);
        }

        public static bool IsFresh(CurrencyRates r, DateTime utcNow)
        {
            return r != null && r.FetchedUtc <= utcNow.AddMinutes(5)
                && (utcNow - r.FetchedUtc).TotalHours < FreshHours;
        }

        // ---------------- cache file ----------------

        /// <summary>Only the currencies the app offers are kept: the cache is a few hundred
        /// bytes, and the converter keeps working offline with the last figures it saw.</summary>
        public static string ToCache(CurrencyRates r)
        {
            var rates = new Dictionary<string, object>();
            foreach (var c in All)
            {
                double v;
                if (r.PerUsd.TryGetValue(c.Code, out v)) rates[c.Code] = v;
            }
            var d = new Dictionary<string, object>
            {
                { "date", r.Date },
                { "fetched", r.FetchedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) },
                { "rates", rates },
            };
            return Json.Write(d);
        }

        public static CurrencyRates FromCache(string json)
        {
            Dictionary<string, object> d;
            try { d = Json.Parse(json) as Dictionary<string, object>; }
            catch { return null; }
            if (d == null) return null;
            DateTime fetched;
            if (!DateTime.TryParse(Json.GetStr(d, "fetched", ""), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out fetched))
                return null;
            var r = Parse(json, fetched);
            return r;
        }

        // ---------------- typed phrases ----------------

        static readonly Dictionary<string, string> Alias = BuildAliases();

        static Dictionary<string, string> BuildAliases()
        {
            var a = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in All) a[c.Code] = c.Code;
            Action<string, string[]> add = (code, names) => { foreach (var n in names) a[Loose(n)] = code; };
            // a bare "ريال" / "جنيه" / "دينار" means the one most people mean; the full
            // two-word names ("ريال قطري") are matched before these (see TryParsePhrase)
            add("SAR", new[] { "ريال", "ريالات", "riyal", "riyals", "سعودي" });
            add("EGP", new[] { "جنيه", "جنيهات", "جنية", "مصري" });
            add("USD", new[] { "دولار", "دولارات", "dollar", "dollars", "$" });
            add("EUR", new[] { "يورو", "euro", "euros", "€" });
            add("GBP", new[] { "استرليني", "إسترليني", "sterling", "£" });
            add("AED", new[] { "درهم", "دراهم", "dirham", "dirhams", "اماراتي", "إماراتي" });
            add("KWD", new[] { "دينار", "دنانير", "dinar", "dinars", "كويتي" });
            add("QAR", new[] { "قطري" });
            add("BHD", new[] { "بحريني" });
            add("OMR", new[] { "عماني", "عُماني" });
            add("JOD", new[] { "اردني", "أردني" });
            add("TRY", new[] { "ليرة", "ليره", "lira", "تركي", "تركية" });
            add("INR", new[] { "روبية", "روبيه", "rupee", "rupees" });
            add("CNY", new[] { "يوان", "yuan" });
            add("JPY", new[] { "ين", "yen" });
            add("CHF", new[] { "فرنك", "franc" });
            add("RUB", new[] { "روبل", "ruble" });
            return a;
        }

        // words that may sit between the pieces without making the line something else
        static readonly HashSet<string> Filler = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "كم", "كام", "بكم", "بكام", "يساوي", "تساوي", "يعادل", "الى", "إلى", "الي", "ل", "من", "في", "ب",
            "to", "in", "into", "is", "how", "much", "convert", "حول", "حوّل", "=", "؟", "?",
        };

        static string Fold(string s)
        {
            if (s == null) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char ch in s)
            {
                char c = ch;
                if (c >= '٠' && c <= '٩') c = (char)('0' + (c - '٠'));       // Arabic-Indic digits
                else if (c >= '۰' && c <= '۹') c = (char)('0' + (c - '۰'));  // Persian digits
                else if (c == '٫') c = '.';                                    // Arabic decimal mark
                else if (c == '٬' || c == '،') c = ',';
                if ((c >= 'ً' && c <= 'ْ') || c == 'ـ') continue;   // diacritics, tatweel
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Peels the Arabic prefixes people glue onto a currency word:
        /// "بالريال", "للدولار", "الجنيه", "بريال".</summary>
        static string Resolve(string word)
        {
            word = Loose(word);
            string code;
            if (Alias.TryGetValue(word, out code)) return code;
            foreach (string pre in new[] { "بال", "لل", "ال", "ب", "ل" })
            {
                if (word.Length > pre.Length + 1 && word.StartsWith(pre, StringComparison.Ordinal)
                    && Alias.TryGetValue(word.Substring(pre.Length), out code))
                    return code;
            }
            return null;
        }

        static readonly Regex NumberRx = new Regex(@"^[+-]?(\d{1,3}(,\d{3})+|\d+)(\.\d+)?$", RegexOptions.CultureInvariant);

        /// <summary>Recognises a conversion typed as a sentence: an amount, a currency, and
        /// optionally a second currency — "100 usd sar", "250 ريال بالجنيه", "٥٠ دولار كم
        /// جنيه؟", "20 eur to egp". Anything else in the line (beyond filler such as "كم" or
        /// "to") means this is not a conversion: "اشترِ بـ 100 ريال خبز" stays a task.</summary>
        public static bool TryParsePhrase(string text, string defaultTo, out double amount, out string from, out string to)
        {
            amount = 0; from = null; to = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = Fold(text).Trim();
            // glue-free symbols and trailing question marks: "$100" → "$ 100", "ريال؟" → "ريال ؟"
            t = Regex.Replace(t, @"([$€£؟?=])", " $1 ");
            // "100usd" / "100ريال" → "100 usd"
            t = Regex.Replace(t, @"(\d)([^\d\s.,])", "$1 $2");
            var words = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2 || words.Length > 9) return false;

            bool haveAmount = false;
            var codes = new List<string>();
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                if (NumberRx.IsMatch(w))
                {
                    if (haveAmount) return false;                // two numbers: not ours
                    if (!double.TryParse(w.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
                        return false;
                    haveAmount = true;
                    continue;
                }
                // two-word names first: "ريال قطري", "جنيه إسترليني", "دولار كندي"
                if (i + 1 < words.Length)
                {
                    string pair = TwoWord(w, words[i + 1]);
                    if (pair != null) { codes.Add(pair); i++; continue; }
                }
                string code = Resolve(w);
                if (code != null) { codes.Add(code); continue; }
                if (Filler.Contains(w)) continue;
                return false;                                    // a real word: this is not a conversion
            }
            if (!haveAmount || codes.Count == 0 || codes.Count > 2) return false;
            from = codes[0];
            to = codes.Count == 2 ? codes[1] : defaultTo;
            if (string.IsNullOrEmpty(to) || Find(to) == null) return false;
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                // "100 ريال" with the riyal as home currency: show dollars instead of 100 = 100
                if (codes.Count == 2) return false;
                to = from == "USD" ? "EUR" : "USD";
            }
            return true;
        }

        /// <summary>Matches a currency written as noun + nationality by its Arabic name.</summary>
        static string TwoWord(string a, string b)
        {
            string first = StripPrefix(a);
            string second = StripPrefix(b);
            foreach (var c in All)
            {
                var parts = Fold(c.NameAr).Split(' ');
                if (parts.Length != 2) continue;
                if (SameArabic(parts[0], first) && SameArabic(parts[1], second)) return c.Code;
            }
            return null;
        }

        static string StripPrefix(string w)
        {
            foreach (string pre in new[] { "بال", "لل", "ال" })
                if (w.Length > pre.Length + 1 && w.StartsWith(pre, StringComparison.Ordinal)) return w.Substring(pre.Length);
            return w;
        }

        /// <summary>Arabic comparison that forgives the spellings people actually type:
        /// hamza forms of alef, ta marbuta vs ha, alef maqsura vs ya.</summary>
        static bool SameArabic(string x, string y)
        {
            return Loose(x) == Loose(y);
        }

        static string Loose(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c == 'أ' || c == 'إ' || c == 'آ') sb.Append('ا');
                else if (c == 'ة') sb.Append('ه');
                else if (c == 'ى') sb.Append('ي');
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
