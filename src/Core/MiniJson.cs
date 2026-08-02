using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DeskMate.Core
{
    /// <summary>Minimal JSON parser/writer (no external deps, net48-safe).
    /// Values map to: Dictionary&lt;string,object&gt;, List&lt;object&gt;, string, double, bool, null.</summary>
    public static class Json
    {
        /// <summary>Nesting limit. The parser is recursive, so unbounded depth raises
        /// StackOverflowException — which .NET cannot catch and which kills the process
        /// outright. Input reaches here from settings.json and from remote weather /
        /// geocoding / IP-location responses, so the limit is a hard requirement.</summary>
        public const int MaxDepth = 64;

        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            int pos = 0;
            var v = ParseValue(text, ref pos, 0);
            SkipWs(text, ref pos);
            if (pos != text.Length) throw new FormatException("Trailing characters at " + pos);
            return v;
        }

        static void SkipWs(string s, ref int p)
        {
            while (p < s.Length && (s[p] == ' ' || s[p] == '\t' || s[p] == '\r' || s[p] == '\n')) p++;
        }

        static object ParseValue(string s, ref int p, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("Nested deeper than " + MaxDepth + " at " + p);
            SkipWs(s, ref p);
            if (p >= s.Length) throw new FormatException("Unexpected end");
            char c = s[p];
            if (c == '{') return ParseObject(s, ref p, depth);
            if (c == '[') return ParseArray(s, ref p, depth);
            if (c == '"') return ParseString(s, ref p);
            if (c == 't') { Expect(s, ref p, "true"); return true; }
            if (c == 'f') { Expect(s, ref p, "false"); return false; }
            if (c == 'n') { Expect(s, ref p, "null"); return null; }
            return ParseNumber(s, ref p);
        }

        static void Expect(string s, ref int p, string word)
        {
            if (p + word.Length > s.Length || s.Substring(p, word.Length) != word)
                throw new FormatException("Invalid literal at " + p);
            p += word.Length;
        }

        static Dictionary<string, object> ParseObject(string s, ref int p, int depth)
        {
            var d = new Dictionary<string, object>();
            p++; // {
            SkipWs(s, ref p);
            if (p < s.Length && s[p] == '}') { p++; return d; }
            while (true)
            {
                SkipWs(s, ref p);
                string key = ParseString(s, ref p);
                SkipWs(s, ref p);
                if (p >= s.Length || s[p] != ':') throw new FormatException("Expected ':' at " + p);
                p++;
                d[key] = ParseValue(s, ref p, depth + 1);
                SkipWs(s, ref p);
                if (p >= s.Length) throw new FormatException("Unterminated object");
                if (s[p] == ',') { p++; continue; }
                if (s[p] == '}') { p++; return d; }
                throw new FormatException("Expected ',' or '}' at " + p);
            }
        }

        static List<object> ParseArray(string s, ref int p, int depth)
        {
            var a = new List<object>();
            p++; // [
            SkipWs(s, ref p);
            if (p < s.Length && s[p] == ']') { p++; return a; }
            while (true)
            {
                a.Add(ParseValue(s, ref p, depth + 1));
                SkipWs(s, ref p);
                if (p >= s.Length) throw new FormatException("Unterminated array");
                if (s[p] == ',') { p++; continue; }
                if (s[p] == ']') { p++; return a; }
                throw new FormatException("Expected ',' or ']' at " + p);
            }
        }

        static string ParseString(string s, ref int p)
        {
            // bounds-checked: ParseObject calls this for keys and may already be at the end
            if (p >= s.Length || s[p] != '"') throw new FormatException("Expected string at " + p);
            p++;
            var sb = new StringBuilder();
            while (true)
            {
                if (p >= s.Length) throw new FormatException("Unterminated string");
                char c = s[p++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (p >= s.Length) throw new FormatException("Bad escape");
                    char e = s[p++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (p + 4 > s.Length) throw new FormatException("Bad \\u escape");
                            sb.Append((char)int.Parse(s.Substring(p, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            p += 4;
                            break;
                        default: throw new FormatException("Bad escape '\\" + e + "'");
                    }
                }
                else sb.Append(c);
            }
        }

        static object ParseNumber(string s, ref int p)
        {
            int start = p;
            if (p < s.Length && (s[p] == '-' || s[p] == '+')) p++;
            while (p < s.Length && (char.IsDigit(s[p]) || s[p] == '.' || s[p] == 'e' || s[p] == 'E' || s[p] == '-' || s[p] == '+')) p++;
            string num = s.Substring(start, p - start);
            double d;
            if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw new FormatException("Bad number '" + num + "'");
            return d;
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is bool) { sb.Append(((bool)v) ? "true" : "false"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is Dictionary<string, object>)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in (Dictionary<string, object>)v)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key);
                    sb.Append(':');
                    WriteValue(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            if (v is List<object>)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in (List<object>)v)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteValue(sb, item);
                }
                sb.Append(']');
                return;
            }
            if (v is List<string>)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in (List<string>)v)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, item);
                }
                sb.Append(']');
                return;
            }
            // numbers (int, long, double, float)
            double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---- typed helpers for reading parsed dictionaries ----
        public static string GetStr(Dictionary<string, object> d, string key, string def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is string) return (string)v;
            return def;
        }
        public static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is bool) return (bool)v;
            return def;
        }
        public static double GetNum(Dictionary<string, object> d, string key, double def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is double) return (double)v;
            return def;
        }
        public static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            return (int)Math.Round(GetNum(d, key, def));
        }
        public static List<object> GetList(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is List<object>) return (List<object>)v;
            return new List<object>();
        }
        public static Dictionary<string, object> GetObj(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is Dictionary<string, object>) return (Dictionary<string, object>)v;
            return null;
        }
    }
}
