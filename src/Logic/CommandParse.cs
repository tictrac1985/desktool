using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DeskMate.Logic
{
    /// <summary>What the quick-command bar understood from one line of typed text.</summary>
    public class Command
    {
        /// <summary>1 = task (optional reminder) · 2 = calculator expression · 3 = open app.</summary>
        public int Kind;
        /// <summary>Task text, expression, or app search term.</summary>
        public string Text = "";
        /// <summary>Reminder time for tasks; MinValue = none.</summary>
        public DateTime When = DateTime.MinValue;
    }

    /// <summary>Parses quick-command lines, Arabic and English:
    /// "ذكّرني بشراء الخبز غداً الساعة 9" · "= 20% من 450" · "افتح كروم".
    /// Bare text with no prefix becomes a reminderless task — the fastest path from
    /// thought to list. Kept UI-free so every rule is unit-tested.</summary>
    public static class CommandParse
    {
        public const int Task = 1, Calc = 2, OpenApp = 3;

        static readonly string[] TaskPrefixes =
            { "ذكرني", "ذكّرني", "مهمة", "task", "remind", "reminder" };
        static readonly string[] OpenPrefixes =
            { "افتح", "شغل", "شغّل", "open", "run", "launch", "start" };

        /// <summary>Arabic-Indic digits → ASCII, diacritics stripped: people type ٩ and
        /// ذكّرني with or without the shadda, and the parser must not care.</summary>
        static string Normalize(string s)
        {
            if (s == null) return "";
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c >= '٠' && c <= '٩') chars[i] = (char)('0' + (c - '٠'));
            }
            s = new string(chars);
            return Regex.Replace(s, "[\u064B-\u0652\u0640]", "");
        }

        public static Command Parse(string input, DateTime now)
        {
            var cmd = new Command { Kind = Task };   // the bar's default verb
            if (string.IsNullOrWhiteSpace(input)) return cmd;
            string raw = input.Trim();

            if (raw.StartsWith("="))
            {
                cmd.Kind = Calc;
                cmd.Text = raw.Substring(1).Trim();
                return cmd;
            }

            string norm = Normalize(raw);
            string lowered = norm.ToLowerInvariant();

            foreach (var p in OpenPrefixes)
                if (StartsWithWord(lowered, Normalize(p).ToLowerInvariant()))
                {
                    cmd.Kind = OpenApp;
                    cmd.Text = norm.Substring(p.Length).Trim();
                    return cmd;
                }

            // tasks: explicit prefix, or bare text (the bar's default verb is "remember this")
            cmd.Kind = Task;
            string body = norm;
            foreach (var p in TaskPrefixes)
                if (StartsWithWord(lowered, Normalize(p).ToLowerInvariant()))
                {
                    body = norm.Substring(p.Length).Trim();
                    break;
                }
            // drop a leading "ب" connector: "ذكرني بشراء الخبز" → "شراء الخبز"
            // (the tatweel form "بـ" can never appear — Normalize already strips U+0640)
            if (body.StartsWith("ب", StringComparison.Ordinal) && body.Length > 1 && body[1] != ' ')
            {
                string stripped = body.Substring(1).Trim();
                if (stripped.Length > 0) body = stripped;
            }

            cmd.When = ExtractWhen(ref body, now);
            cmd.Text = body.Trim();
            return cmd;
        }

        static bool StartsWithWord(string s, string word)
        {
            // ordinal: culture-aware comparison ignores the tatweel (U+0640) in Arabic,
            // which once matched "بـ" against "بش" and ate a letter off the task text
            if (!s.StartsWith(word, StringComparison.Ordinal)) return false;
            return s.Length == word.Length || s[word.Length] == ' ';
        }

        /// <summary>Pulls time expressions out of the task text and returns the reminder
        /// time (MinValue when none is found). Understands: اليوم/غداً/بعد غد/tomorrow,
        /// "بعد X دقيقة/ساعة", "بعد ساعة/نصف ساعة", "الساعة 9[:30]" (at 9[:30]) and named
        /// dayparts (صباحاً/الظهر/العصر/المغرب/مساءً/الليل).</summary>
        static DateTime ExtractWhen(ref string text, DateTime now)
        {
            var when = DateTime.MinValue;
            var words = new List<string>(text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            var drop = new bool[words.Count];

            int dayOffset = 0;
            bool dateSeen = false;
            int hour = -1, minute = 0;
            bool timeSeen = false;

            for (int i = 0; i < words.Count; i++)
            {
                string w = words[i];
                switch (w)
                {
                    case "اليوم": case "today":
                        dateSeen = true; drop[i] = true; break;
                    case "غدا": case "غداً": case "tomorrow":
                        dayOffset = 1; dateSeen = true; drop[i] = true; break;
                    case "بعد":
                        // "بعد غد" | "بعد X دقيقة/دقائق/ساعة/ساعات" | "بعد ساعة" | "بعد نصف ساعة"
                        if (i + 1 < words.Count && words[i + 1] == "غد")
                        {
                            dayOffset = 2; dateSeen = true; drop[i] = drop[i + 1] = true; i++;
                        }
                        else if (i + 1 < words.Count && string.Equals(words[i + 1], "نصف", StringComparison.Ordinal) &&
                                 i + 2 < words.Count && words[i + 2].StartsWith("ساعة", StringComparison.Ordinal))
                        {
                            when = now.AddMinutes(30);
                            drop[i] = drop[i + 1] = drop[i + 2] = true; i += 2;
                        }
                        else if (i + 1 < words.Count && words[i + 1].StartsWith("ساعة", StringComparison.Ordinal))
                        {
                            when = now.AddHours(1);
                            drop[i] = drop[i + 1] = true; i++;
                        }
                        else if (i + 2 < words.Count)
                        {
                            int n;
                            if (int.TryParse(words[i + 1], out n) && n > 0 && n < 10000)
                            {
                                string unit = words[i + 2];
                                if (unit.StartsWith("دقيقة", StringComparison.Ordinal) || unit.StartsWith("دقايق", StringComparison.Ordinal) || unit.StartsWith("دق", StringComparison.Ordinal))
                                {
                                    when = now.AddMinutes(n);
                                    drop[i] = drop[i + 1] = drop[i + 2] = true; i += 2;
                                }
                                else if (unit.StartsWith("ساعة", StringComparison.Ordinal) || unit.StartsWith("ساعات", StringComparison.Ordinal))
                                {
                                    when = now.AddHours(n);
                                    drop[i] = drop[i + 1] = drop[i + 2] = true; i += 2;
                                }
                            }
                        }
                        break;
                    case "الساعة": case "at":
                        if (i + 1 < words.Count)
                        {
                            int hh, mm;
                            if (TryParseClock(words[i + 1], out hh, out mm))
                            {
                                hour = hh; minute = mm; timeSeen = true;
                                drop[i] = drop[i + 1] = true; i++;
                                // a bare number after the clock hour is the minute
                                // ("الساعة 9 30") is NOT consumed — too ambiguous with task text
                            }
                        }
                        break;
                    case "صباحا": case "صباحاً": case "الصباح":
                        hour = 9; minute = 0; timeSeen = true; drop[i] = true; break;
                    case "الظهر": case "ظهرا": case "ظهراً":
                        hour = 12; minute = 0; timeSeen = true; drop[i] = true; break;
                    case "العصر": case "عصرا": case "عصراً":
                        hour = 15; minute = 0; timeSeen = true; drop[i] = true; break;
                    case "المغرب":
                        hour = 18; minute = 0; timeSeen = true; drop[i] = true; break;
                    case "مساء": case "مساءً": case "المساء":
                        hour = 19; minute = 0; timeSeen = true; drop[i] = true; break;
                    case "الليل": case "ليلا": case "ليلاً":
                        hour = 21; minute = 0; timeSeen = true; drop[i] = true; break;
                }
            }

            if (when != DateTime.MinValue) { /* relative "after X" already absolute */ }
            else if (timeSeen || dateSeen)
            {
                var day = now.Date.AddDays(dayOffset);
                if (!timeSeen) { hour = 9; minute = 0; }
                when = day.AddHours(hour).AddMinutes(minute);
                // "الساعة 9" typed at 10:00 means the next 9 o'clock, not a past one
                if (!dateSeen && when <= now) when = when.AddDays(1);
            }
            else return DateTime.MinValue;

            var kept = new List<string>();
            for (int i = 0; i < words.Count; i++)
                if (!drop[i]) kept.Add(words[i]);
            text = string.Join(" ", kept);
            return when;
        }

        static bool TryParseClock(string s, out int hour, out int minute)
        {
            hour = 0; minute = 0;
            var m = Regex.Match(s, @"^(\d{1,2})(?::(\d{1,2}))?$");
            if (!m.Success) return false;
            hour = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            if (hour > 24) return false;
            if (hour == 24) hour = 0;
            if (m.Groups[2].Success)
            {
                minute = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                if (minute > 59) return false;
            }
            return true;
        }
    }
}
