using System;
using System.Collections.Generic;
using System.Globalization;

namespace DeskMate.Logic
{
    /// <summary>Colour arithmetic for the picker: the three notations people paste into
    /// design tools and stylesheets (HEX, RGB, HSL), and the recent-colours list.</summary>
    public static class ColorCore
    {
        public static string ToHex(byte r, byte g, byte b)
        {
            return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        public static bool TryParseHex(string hex, out byte r, out byte g, out byte b)
        {
            r = g = b = 0;
            if (string.IsNullOrEmpty(hex)) return false;
            string h = hex.Trim().TrimStart('#');
            if (h.Length == 3) h = "" + h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
            if (h.Length != 6) return false;
            uint v;
            if (!uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) return false;
            r = (byte)(v >> 16); g = (byte)(v >> 8); b = (byte)v;
            return true;
        }

        public static string ToRgbText(byte r, byte g, byte b)
        {
            return "rgb(" + r + ", " + g + ", " + b + ")";
        }

        /// <summary>RGB → hue 0..360, saturation and lightness 0..100 (rounded, CSS style).</summary>
        public static void ToHsl(byte r, byte g, byte b, out int h, out int s, out int l)
        {
            double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
            double max = Math.Max(rf, Math.Max(gf, bf)), min = Math.Min(rf, Math.Min(gf, bf));
            double lig = (max + min) / 2, hue = 0, sat = 0;
            if (max > min)
            {
                double d = max - min;
                sat = lig > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == rf) hue = (gf - bf) / d + (gf < bf ? 6 : 0);
                else if (max == gf) hue = (bf - rf) / d + 2;
                else hue = (rf - gf) / d + 4;
                hue *= 60;
            }
            h = (int)Math.Round(hue) % 360;
            s = (int)Math.Round(sat * 100);
            l = (int)Math.Round(lig * 100);
        }

        public static string ToHslText(byte r, byte g, byte b)
        {
            int h, s, l;
            ToHsl(r, g, b, out h, out s, out l);
            return "hsl(" + h + ", " + s + "%, " + l + "%)";
        }

        /// <summary>True when black text reads better than white on this colour
        /// (WCAG relative luminance above the midpoint).</summary>
        public static bool PrefersDarkText(byte r, byte g, byte b)
        {
            Func<byte, double> lin = c =>
            {
                double v = c / 255.0;
                return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            };
            double lum = 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
            return lum > 0.179;
        }

        /// <summary>Puts a colour at the front of the recent list: no duplicates, newest
        /// first, never longer than <paramref name="max"/>.</summary>
        public static void PushRecent(List<string> recent, string hex, int max)
        {
            byte r, g, b;
            if (recent == null || !TryParseHex(hex, out r, out g, out b)) return;
            string norm = ToHex(r, g, b);
            recent.RemoveAll(c => string.Equals(c, norm, StringComparison.OrdinalIgnoreCase));
            recent.Insert(0, norm);
            while (recent.Count > max) recent.RemoveAt(recent.Count - 1);
        }
    }
}
