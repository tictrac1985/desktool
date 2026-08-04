using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using DeskMate.Core;

namespace DeskMate.Logic
{
    /// <summary>Material You: derive a Theme.Palette-compatible palette from the dominant
    /// colour of the user's desktop wallpaper. The extraction is split into:
    ///   1. <see cref="ExtractDominant"/> — pure function, tested in SelfTest.
    ///   2. <see cref="BuildPalette"/> — pure colour-math function.
    ///   3. <see cref="GenerateFromWallpaper"/> — impure wrapper that does the I/O once and
    ///      caches the result keyed by wallpaper file path + last-write-time.
    /// This split keeps the rules testable without spinning up a System.Drawing.Bitmap in
    /// the test runner, and the cached call is what the widget's tick actually hits.</summary>
    public static class MaterialYouCore
    {
        /// <summary>RGB color in 0..255 range.</summary>
        public struct Rgb { public int R, G, B; public Rgb(int r, int g, int b) { R = r; G = g; B = b; } }

        /// <summary>Pick the dominant colour of a bitmap. The bitmap is sampled at a low
        /// resolution (targetSide × targetSide) for speed, and the dominant colour is the
        /// chroma-weighted mean: pixels with high saturation contribute more than grey ones,
        /// because Material You accents should be colourful, not the average background.
        /// Very dark and very light pixels are filtered out so a black wallpaper doesn't
        /// produce a black theme.</summary>
        public static Rgb ExtractDominant(Bitmap bmp, int targetSide = 64)
        {
            if (bmp == null) return new Rgb(0x6E, 0xC1, 0xFF);  // safe default
            int w = bmp.Width, h = bmp.Height;
            // step so we hit roughly targetSide*targetSide samples regardless of aspect ratio
            int stepX = Math.Max(1, w / targetSide);
            int stepY = Math.Max(1, h / targetSide);
            long sumWR = 0, sumWG = 0, sumWB = 0, sumW = 0;
            for (int y = 0; y < h; y += stepY)
            {
                for (int x = 0; x < w; x += stepX)
                {
                    var c = bmp.GetPixel(x, y);
                    int r = c.R, g = c.G, b = c.B;
                    // skip near-black and near-white
                    int mx = Math.Max(r, Math.Max(g, b));
                    int mn = Math.Min(r, Math.Min(g, b));
                    if (mx < 24) continue;
                    if (mn > 232) continue;
                    // saturation * brightness weight; pure greys contribute ~0, vivid pixels ~max
                    int sat = mx == 0 ? 0 : (mx - mn) * 255 / mx;
                    double wgt = (sat / 255.0) * (0.35 + 0.65 * (mx / 255.0));
                    sumWR += (long)(r * wgt);
                    sumWG += (long)(g * wgt);
                    sumWB += (long)(b * wgt);
                    sumW += (long)wgt;
                }
            }
            if (sumW == 0)
            {
                // fallback: simple average (all-grey wallpaper)
                long sr = 0, sg = 0, sb = 0, n = 0;
                for (int y = 0; y < h; y += stepY)
                    for (int x = 0; x < w; x += stepX)
                    {
                        var c = bmp.GetPixel(x, y);
                        sr += c.R; sg += c.G; sb += c.B; n++;
                    }
                if (n == 0) return new Rgb(0x6E, 0xC1, 0xFF);
                return new Rgb((int)(sr / n), (int)(sg / n), (int)(sb / n));
            }
            return new Rgb((int)(sumWR / sumW), (int)(sumWG / sumW), (int)(sumWB / sumW));
        }

        /// <summary>Build a complete palette around an accent colour. Picks dark or light
        /// card based on the accent's perceived brightness: a light wallpaper → light card
        /// (and a darkened accent so the digits stay readable on white); a dark wallpaper →
        /// dark card (and a lifted accent, as before). Pure function — same input always
        /// yields same output.</summary>
        public static MaterialPalette BuildPalette(Rgb accent)
        {
            double lum = (0.2126 * accent.R + 0.7152 * accent.G + 0.0722 * accent.B) / 255.0;
            bool light = lum > 0.55;
            int lift = light ? -90 : 90;     // pull the accent the other way for contrast
            int aR = Clamp255(accent.R + lift);
            int aG = Clamp255(accent.G + lift);
            int aB = Clamp255(accent.B + lift);
            int bR, bG, bB, tR, tG, tB;
            if (light)
            {
                // light card: sit just under the wallpaper colour, with a subtle gradient
                bR = Clamp(accent.R - 18, 200, 252);
                bG = Clamp(accent.G - 18, 200, 252);
                bB = Clamp(accent.B - 18, 200, 252);
                tR = Clamp(accent.R - 8, 215, 255);
                tG = Clamp(accent.G - 8, 215, 255);
                tB = Clamp(accent.B - 8, 215, 255);
            }
            else
            {
                // dark card: a much darker version of the accent
                bR = (int)(accent.R * 0.18 + 12);
                bG = (int)(accent.G * 0.18 + 12);
                bB = (int)(accent.B * 0.18 + 12);
                bR = Clamp(bR, 8, 40);
                bG = Clamp(bG, 8, 40);
                bB = Clamp(bB, 8, 40);
                tR = Clamp(bR + 14, 8, 40);
                tG = Clamp(bG + 14, 8, 40);
                tB = Clamp(bB + 14, 8, 40);
            }
            return new MaterialPalette
            {
                Accent = new Rgb(aR, aG, aB),
                CardTop = new Rgb(tR, tG, tB),
                CardBottom = new Rgb(bR, bG, bB),
            };
        }

        static int Clamp255(int v) { return v < 0 ? 0 : v > 255 ? 255 : v; }
        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }

        public struct MaterialPalette
        {
            public Rgb Accent, CardTop, CardBottom;
        }

        // ---------------- caching ----------------

        static string _cachedPath = "";
        static DateTime _cachedMtime;
        static Rgb _cachedAccent;
        static bool _hasCached;

        /// <summary>Returns the dominant colour of the given image file, caching by path +
        /// last-write-time. Same file with no change returns the cached value (no I/O).
        /// Pure-DOM return: the Rgb is a struct.</summary>
        public static Rgb DominantOfFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new Rgb(0x6E, 0xC1, 0xFF);
                var mtime = File.GetLastWriteTimeUtc(path);
                if (_hasCached && _cachedPath == path && _cachedMtime == mtime) return _cachedAccent;
                using (var bmp = new Bitmap(path))
                {
                    _cachedAccent = ExtractDominant(bmp);
                }
                _cachedPath = path;
                _cachedMtime = mtime;
                _hasCached = true;
                return _cachedAccent;
            }
            catch (Exception ex)
            {
                Log.Error("MaterialYou.DominantOfFile", ex);
                return _hasCached ? _cachedAccent : new Rgb(0x6E, 0xC1, 0xFF);
            }
        }

        /// <summary>End-to-end: read the current wallpaper, extract the dominant colour,
        /// and build a palette. Caches everything to avoid hitting the disk on every tick.</summary>
        public static MaterialPalette GenerateFromWallpaper()
        {
            var path = Win32.GetCurrentWallpaper();
            var accent = DominantOfFile(path);
            return BuildPalette(accent);
        }

        /// <summary>Forget the cached result. Called when the user changes the wallpaper
        /// through the Wallpaper feature, so the next tick recomputes.</summary>
        public static void Invalidate()
        {
            _hasCached = false;
            _cachedPath = "";
        }
    }
}
