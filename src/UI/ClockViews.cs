using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using DeskMate.Core;

namespace DeskMate.UI
{
    public interface IClockView
    {
        FrameworkElement Root { get; }
        void Tick(DateTime now);
    }

    /// <summary>Shape hints for layouts that cut a circle around the clock (Orbit, Halo).
    /// A dial tagged Round only occupies the ellipse inscribed in its box; a Dome only the
    /// upper half-ellipse standing on its bottom edge. Untagged elements count as their
    /// full rectangle.</summary>
    public static class Footprint
    {
        public const string Round = "fp:round";
        public const string Dome = "fp:dome";
    }

    /// <summary>Date lines shared by every clock face. Tri-panel shows the dates in its own
    /// side cards, so it switches them off here instead of printing everything twice.</summary>
    static class ClockDates
    {
        public static bool Hidden;

        public static void Paint(TextBlock date, TextBlock hijri, DateTime now)
        {
            var s = Settings.I;
            date.Visibility = Hidden ? Visibility.Collapsed : Visibility.Visible;
            if (!Hidden) date.Text = DateText.Gregorian(now);
            bool showH = s.ShowHijri && !Hidden;
            hijri.Visibility = showH ? Visibility.Visible : Visibility.Collapsed;
            if (showH) hijri.Text = DateText.Hijri(now);
        }
    }

    static class DateText
    {
        // formatting + Hijri conversion are relatively costly — cache per day/language
        static string _cacheKey;
        static string _cachedGregorian = "", _cachedHijri = "";

        static void EnsureCache(DateTime now)
        {
            string key = now.ToString("yyyyMMdd") + Settings.I.Lang;
            if (key == _cacheKey) return;
            _cacheKey = key;
            _cachedGregorian = FormatGregorian(now);
            _cachedHijri = FormatHijri(now);
        }

        public static string Gregorian(DateTime now) { EnsureCache(now); return _cachedGregorian; }
        public static string Hijri(DateTime now) { EnsureCache(now); return _cachedHijri; }

        static readonly string[] HijriMonthsAr =
        {
            "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
            "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة"
        };
        static readonly string[] HijriMonthsEn =
        {
            "Muharram", "Safar", "Rabi I", "Rabi II", "Jumada I", "Jumada II",
            "Rajab", "Sha'ban", "Ramadan", "Shawwal", "Dhu al-Qi'dah", "Dhu al-Hijjah"
        };

        static string FormatGregorian(DateTime now)
        {
            var ci = L.IsAr ? new CultureInfo("ar-EG") : new CultureInfo("en-US");
            string s = now.ToString("dddd، d MMMM yyyy", ci);
            if (!L.IsAr) s = now.ToString("dddd, MMMM d, yyyy", ci);
            return s;
        }

        static string FormatHijri(DateTime now)
        {
            try
            {
                var c = new UmAlQuraCalendar();
                int d = c.GetDayOfMonth(now), m = c.GetMonth(now), y = c.GetYear(now);
                return L.IsAr
                    ? string.Format("{0} {1} {2} هـ", d, HijriMonthsAr[m - 1], y)
                    : string.Format("{0} {1} {2} AH", HijriMonthsEn[m - 1], d, y);
            }
            catch { return ""; }
        }
    }

    /// <summary>Large glass digits with AM/PM and date lines.</summary>
    public class DigitalClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly TextBlock _time;
        readonly TextBlock _ampm;
        readonly System.Windows.Shapes.Path _ampmGlyph = new System.Windows.Shapes.Path { Stretch = Stretch.Uniform };
        readonly Viewbox _ampmGlyphHost;
        readonly Border _ampmHost;
        readonly TextBlock _date;
        readonly TextBlock _hijri;

        public DigitalClock(double timeSize = 58)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _time = Theme.MakeText("", timeSize, Theme.Text, Theme.LightFont);
            _time.FlowDirection = FlowDirection.LeftToRight;
            _ampm = Theme.MakeText("", 13.5, Theme.Accent);
            _ampm.FontWeight = FontWeights.SemiBold;
            // an explicitly LTR run never goes through the RTL offscreen composition path that
            // mirrors isolated glyphs in the transparent window (the time digits prove it)
            _ampm.FlowDirection = FlowDirection.LeftToRight;
            _ampmGlyph.Fill = Theme.Accent;
            _ampmGlyphHost = new Viewbox { Child = _ampmGlyph, Height = 12.5, FlowDirection = FlowDirection.LeftToRight };
            var ampmContent = new Grid();
            ampmContent.Children.Add(_ampm);
            ampmContent.Children.Add(_ampmGlyphHost);
            var accentPill = new SolidColorBrush(Color.FromArgb(0x2A, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            accentPill.Freeze();
            _ampmHost = new Border
            {
                Background = accentPill,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 2.5, 8, 2.5),
                Child = ampmContent,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(9, 0, 0, timeSize * 0.24),
            };
            row.Children.Add(_time);
            row.Children.Add(_ampmHost);
            _root.Children.Add(row);
            _date = Theme.MakeText("", 15, Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _date.Margin = new Thickness(0, -4, 0, 0);
            _root.Children.Add(_date);
            _hijri = Theme.MakeText("", 12.5, Theme.TextFaint);
            _hijri.HorizontalAlignment = HorizontalAlignment.Center;
            _hijri.Margin = new Thickness(0, 1, 0, 0);
            _root.Children.Add(_hijri);
        }

        public FrameworkElement Root { get { return _root; } }

        public void Tick(DateTime now)
        {
            var s = Settings.I;
            string fmt = s.Use24h
                ? (s.ShowSeconds ? "HH:mm:ss" : "HH:mm")
                : (s.ShowSeconds ? "h:mm:ss" : "h:mm");
            _time.Text = now.ToString(fmt, CultureInfo.InvariantCulture);
            _ampmHost.Visibility = s.Use24h ? Visibility.Collapsed : Visibility.Visible;
            // Arabic AM/PM is drawn as a vector path (AmPmGlyph), not shaped text — immune
            // to the DirectWrite isolated-letter shaping defect that mirrored the meem.
            bool ar = L.IsAr;
            _ampm.Visibility = ar ? Visibility.Collapsed : Visibility.Visible;
            _ampmGlyphHost.Visibility = ar ? Visibility.Visible : Visibility.Collapsed;
            if (ar) _ampmGlyph.Data = now.Hour < 12 ? AmPmGlyph.Sad : AmPmGlyph.Meem;
            else _ampm.Text = now.Hour < 12 ? "AM" : "PM";
            ClockDates.Paint(_date, _hijri, now);
        }
    }

    /// <summary>Analog dial with hour/minute(/second) hands. Honours the user's ClockStyle
    /// in every layout — including the compact Tri-panel / Sidebar / Dock ones, where
    /// the dial is shrunk so it still fits the surrounding card without overflowing.</summary>
    public class AnalogClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly Canvas _canvas;
        readonly Line _hour, _min, _sec;
        readonly TextBlock _date, _hijri, _time;
        readonly double _size, _cx, _cy;
        // runtime-mutable hand lengths so Sidebar/Dock can pass a smaller dial without
        // duplicating the entire class
        double _hourLen, _minLen, _secLen;
        readonly double _sizeConst;

        public AnalogClock() : this(168) { }

        public AnalogClock(double size)
        {
            // The first version of this class set X1/Y1/X2/Y2 directly on each tick.
            // That works in WPF — these are dependency properties, so changes invalidate
            // the visual. The "clock stuck" bug the user reported was NOT in this class:
            // it was the OnTick Material You loop in MainWindow.cs (every tick reloaded
            // the visual tree), and a hard-coded MinimalClock in the Sidebar/Dock layouts.
            //
            // I tried a RotateTransform on a Canvas parent as an alternative. That
            // approach had two issues:
            //   1. RenderTransform = new RotateTransform(deg) on a Line doesn't always
            //      invalidate the visual in WPF — the new instance replaces the old one
            //      but the layout system kept the previous frame.
            //   2. With a Grid parent, the Line is placed in row 0 column 0 starting at
            //      the origin, which made the RenderTransformOrigin offset wrong.
            // The X1/Y1/X2/Y2 approach below is what worked in v1.5.0 and earlier, and
            // it works again now that the OnTick Material You loop is fixed.
            _size = size;
            _sizeConst = size;   // captured for the static SetHandAngle to use without an instance
            _cx = size / 2;
            _cy = size / 2;
            // hand lengths scale with the dial — the per-class "Size = 168" was the reason
            // the previous fixed code broke Sidebar/Dock/Tri-panel when they tried to
            // honour the user's ClockStyle=Analog
            _hourLen = _cx * 0.48;
            _minLen = _cx * 0.70;
            _secLen = _cx * 0.78;
            _canvas = new Canvas { Width = size, Height = size, FlowDirection = FlowDirection.LeftToRight, Tag = Footprint.Round };
            var dial = new Ellipse
            {
                Width = size, Height = size,
                Stroke = Theme.DialStroke,
                StrokeThickness = Math.Max(1.0, size * 0.015),
                Fill = Theme.DialFill,
            };
            _canvas.Children.Add(dial);
            // convex glass dome over the dial face: radial light from the top-left
            var dome = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.32, 0.28),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.72,
                RadiusY = 0.72,
            };
            byte dHi = Theme.IsLight ? (byte)0x35 : (byte)0x20;
            byte dLo = Theme.IsLight ? (byte)0x10 : (byte)0x26;
            dome.GradientStops.Add(new GradientStop(Color.FromArgb(dHi, 255, 255, 255), 0));
            dome.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.55));
            dome.GradientStops.Add(new GradientStop(Color.FromArgb(dLo, 0, 0, 0), 1));
            var domeEl = new Ellipse { Width = _size - 5, Height = _size - 5, Fill = dome, IsHitTestVisible = false };
            Canvas.SetLeft(domeEl, 2.5);
            Canvas.SetTop(domeEl, 2.5);
            _canvas.Children.Add(domeEl);
            for (int i = 0; i < 12; i++)
            {
                double a = i * 30 * Math.PI / 180;
                bool quarter = i % 3 == 0;
                double r1 = _cx - (quarter ? Math.Max(6, _cx * 0.08) : Math.Max(4, _cx * 0.06));
                double r2 = _cx - Math.Max(2, _cx * 0.03);
                var tick = new Line
                {
                    X1 = _cx + Math.Sin(a) * r1, Y1 = _cy - Math.Cos(a) * r1,
                    X2 = _cx + Math.Sin(a) * r2, Y2 = _cy - Math.Cos(a) * r2,
                    Stroke = quarter ? Theme.Accent : Theme.TextFaint,
                    StrokeThickness = quarter ? Math.Max(1.2, _size * 0.016) : Math.Max(0.7, _size * 0.008),
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                };
                _canvas.Children.Add(tick);
            }
            _hour = MakeHand(Math.Max(2.0, _size * 0.027), _hourLen, Theme.Text);
            _min = MakeHand(Math.Max(1.4, _size * 0.019), _minLen, Theme.Text);
            _sec = MakeHand(Math.Max(0.6, _size * 0.008), _secLen, Theme.Accent);
            var dotSize = Math.Max(3, _size * 0.054);
            var dot = new Ellipse { Width = dotSize, Height = dotSize, Fill = Theme.Accent };
            Canvas.SetLeft(dot, _cx - dotSize / 2); Canvas.SetTop(dot, _cy - dotSize / 2);
            _canvas.Children.Add(dot);
            _root.Children.Add(_canvas);

            // Small digital readout beneath the dial. Useful for the user to confirm the
            // time Tick() is firing with, and a graceful fallback if the hands look
            // misaligned on a particular screen. Skipped in compact Sidebar/Dock so the
            // strip does not balloon.
            _time = Theme.MakeText("", Math.Max(9, _size * 0.077), Theme.TextDim);
            _time.HorizontalAlignment = HorizontalAlignment.Center;
            _time.Margin = new Thickness(0, 4, 0, 0);
            _time.FlowDirection = FlowDirection.LeftToRight;
            _root.Children.Add(_time);

            _date = Theme.MakeText("", Math.Max(10, _size * 0.083), Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _date.Margin = new Thickness(0, 2, 0, 0);
            _root.Children.Add(_date);
            _hijri = Theme.MakeText("", Math.Max(9, _size * 0.071), Theme.TextFaint);
            _hijri.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_hijri);
        }

        Line MakeHand(double thickness, double length, Brush brush)
        {
            var l = new Line
            {
                X1 = _cx, Y1 = _cy, X2 = _cx, Y2 = _cy - length,
                Stroke = brush, StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            };
            _canvas.Children.Add(l);
            return l;
        }

        public FrameworkElement Root { get { return _root; } }

        public void Tick(DateTime now)
        {
            var s = Settings.I;
            SetHandAngle(_hour, (now.Hour % 12 + now.Minute / 60.0) * 30, _hourLen);
            // sweep the minute hand smoothly when the second hand is shown; step per minute otherwise
            SetHandAngle(_min, (now.Minute + (s.ShowSeconds ? now.Second / 60.0 : 0)) * 6, _minLen);
            _sec.Visibility = s.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;
            if (s.ShowSeconds) SetHandAngle(_sec, now.Second * 6, _secLen);
            // digital readout under the dial. It used to be hard-wired to :ss, which meant the
            // analog face repainted every second even for users who had switched seconds off —
            // and on a layered window that repaint is CPU work. It follows the setting now,
            // like every other clock style.
            string fmt = s.Use24h
                ? (s.ShowSeconds ? "HH:mm:ss" : "HH:mm")
                : (s.ShowSeconds ? "h:mm:ss" : "h:mm");
            _time.Text = now.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
            ClockDates.Paint(_date, _hijri, now);
        }

        void SetHandAngle(Line hand, double deg, double length)
        {
            double a = deg * Math.PI / 180;
            hand.X2 = _cx + Math.Sin(a) * length;
            hand.Y2 = _cy - Math.Cos(a) * length;
        }
    }

    /// <summary>Time only (used by the tri-panel layout where dates live in side cards).</summary>
    public class TimeOnlyClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly TextBlock _time;
        readonly TextBlock _ampm;
        readonly System.Windows.Shapes.Path _ampmGlyph = new System.Windows.Shapes.Path { Stretch = Stretch.Uniform };
        readonly Viewbox _ampmGlyphHost;
        readonly Border _ampmHost;

        public TimeOnlyClock()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _time = Theme.MakeText("", 52, Theme.Text, Theme.LightFont);
            _time.FlowDirection = FlowDirection.LeftToRight;
            _ampm = Theme.MakeText("", 12.5, Theme.Accent);
            _ampm.FontWeight = FontWeights.SemiBold;
            _ampm.FlowDirection = FlowDirection.LeftToRight; // see DigitalClock: avoid RTL mirror path
            _ampmGlyph.Fill = Theme.Accent;
            _ampmGlyphHost = new Viewbox { Child = _ampmGlyph, Height = 12, FlowDirection = FlowDirection.LeftToRight };
            var ampmContent = new Grid();
            ampmContent.Children.Add(_ampm);
            ampmContent.Children.Add(_ampmGlyphHost);
            var pill = new SolidColorBrush(Color.FromArgb(0x2A, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            pill.Freeze();
            _ampmHost = new Border
            {
                Background = pill,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 2, 7, 2),
                Child = ampmContent,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(8, 0, 0, 12),
            };
            row.Children.Add(_time);
            row.Children.Add(_ampmHost);
            _root.Children.Add(row);
        }

        public FrameworkElement Root { get { return _root; } }

        public void Tick(DateTime now)
        {
            var s = Settings.I;
            string fmt = s.Use24h
                ? (s.ShowSeconds ? "HH:mm:ss" : "HH:mm")
                : (s.ShowSeconds ? "h:mm:ss" : "h:mm");
            _time.Text = now.ToString(fmt, CultureInfo.InvariantCulture);
            _ampmHost.Visibility = s.Use24h ? Visibility.Collapsed : Visibility.Visible;
            bool ar = L.IsAr;
            _ampm.Visibility = ar ? Visibility.Collapsed : Visibility.Visible;
            _ampmGlyphHost.Visibility = ar ? Visibility.Visible : Visibility.Collapsed;
            if (ar) _ampmGlyph.Data = now.Hour < 12 ? AmPmGlyph.Sad : AmPmGlyph.Meem;
            else _ampm.Text = now.Hour < 12 ? "AM" : "PM";
        }
    }

    /// <summary>Compact single-line time + date.</summary>
    public class MinimalClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly TextBlock _time;
        readonly TextBlock _date;

        public MinimalClock()
        {
            _time = Theme.MakeText("", 34, Theme.Text, Theme.LightFont);
            _time.HorizontalAlignment = HorizontalAlignment.Center;
            _time.FlowDirection = FlowDirection.LeftToRight;
            _date = Theme.MakeText("", 12.5, Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_time);
            _root.Children.Add(_date);
        }

        public FrameworkElement Root { get { return _root; } }

        public void Tick(DateTime now)
        {
            var s = Settings.I;
            string fmt = s.Use24h ? "HH:mm" : "h:mm";
            if (s.ShowSeconds) fmt += ":ss";
            string t = now.ToString(fmt, CultureInfo.InvariantCulture);
            if (!s.Use24h) t += L.IsAr ? (now.Hour < 12 ? " ﺹ" : " ﻡ") : (now.Hour < 12 ? " AM" : " PM");
            _time.Text = t;
            _date.Visibility = ClockDates.Hidden ? Visibility.Collapsed : Visibility.Visible;
            string d = DateText.Gregorian(now);
            if (s.ShowHijri)
            {
                string h = DateText.Hijri(now);
                if (h.Length > 0) d += "  •  " + h;
            }
            _date.Text = d;
        }
    }

    /// <summary>Flip clock — split-flap digits on a recessed tray. v1.11 rebuild: each card is
    /// two CLIPPED halves around a seam (the old card drew two whole digits offset from each
    /// other, so every number looked doubled), the row is pinned left-to-right (in Arabic it
    /// ran right-to-left and printed 6:43 as 03:46), and the hour is zero-padded so the four
    /// cards always mean HH:MM. The flip itself is the real split-flap motion: the old top
    /// half folds down onto the seam, then the new bottom half unfolds from it.</summary>
    public class FlipClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly StackPanel _digitsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            FlowDirection = FlowDirection.LeftToRight,
        };
        readonly TextBlock _date, _hijri, _ampm;
        readonly Border _ampmPill;
        readonly FlipCard[] _cards = new FlipCard[4];   // H H : M M
        string _lastShown = "";

        public FlipClock()
        {
            // AM/PM pill above the cards so the digits keep their full width
            var accentPill = new SolidColorBrush(Color.FromArgb(0x2A, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            accentPill.Freeze();
            _ampm = Theme.MakeText("", 13, Theme.Accent);
            _ampm.FontWeight = FontWeights.Bold;
            _ampmPill = new Border
            {
                Background = accentPill,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 3, 12, 3),
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = _ampm,
            };
            _root.Children.Add(_ampmPill);

            var tray = new Border
            {
                Background = Theme.InputBg,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 9, 10, 9),
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                Child = _digitsRow,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _root.Children.Add(tray);

            for (int i = 0; i < _cards.Length; i++)
            {
                var c = new FlipCard();
                _cards[i] = c;
                _digitsRow.Children.Add(c.Host);
                if (i == 1) _digitsRow.Children.Add(MakeColon());
            }
            _date = Theme.MakeText("", 14.5, Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _date.Margin = new Thickness(0, 9, 0, 0);
            _root.Children.Add(_date);
            _hijri = Theme.MakeText("", 12.5, Theme.TextFaint);
            _hijri.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_hijri);
        }

        static FrameworkElement MakeColon()
        {
            var g = new Grid { Width = 14, Height = FlipCard.H, Margin = new Thickness(2, 0, 2, 0) };
            g.Children.Add(new Ellipse
            {
                Width = 7, Height = 7, Fill = Theme.Accent,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, FlipCard.H * 0.30, 0, 0),
            });
            g.Children.Add(new Ellipse
            {
                Width = 7, Height = 7, Fill = Theme.Accent,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, FlipCard.H * 0.30),
            });
            return g;
        }

        public FrameworkElement Root { get { return _root; } }

        public void Tick(DateTime now)
        {
            var s = Settings.I;
            bool showAmPm = !s.Use24h && s.Scale >= 0.85;
            _ampmPill.Visibility = showAmPm ? Visibility.Visible : Visibility.Collapsed;
            if (showAmPm) _ampm.Text = L.IsAr ? (now.Hour < 12 ? "صباحاً" : "مساءً") : (now.Hour < 12 ? "AM" : "PM");

            // always four digits: 12-hour "643" would leave the last card stale
            string t = now.ToString(s.Use24h ? "HHmm" : "hhmm", CultureInfo.InvariantCulture);
            bool first = _lastShown.Length != t.Length;
            for (int i = 0; i < _cards.Length; i++)
            {
                if (first) _cards[i].SetImmediate(t[i]);
                else if (t[i] != _lastShown[i]) _cards[i].FlipTo(t[i]);
            }
            _lastShown = t;
            ClockDates.Paint(_date, _hijri, now);
        }

        /// <summary>One split-flap digit: static top and bottom halves, plus two flaps used
        /// only while flipping. Each half is a clipped window onto a full-height digit, so the
        /// seam really cuts the number in two.</summary>
        class FlipCard
        {
            public const double Wd = 50, H = 68;
            public readonly FrameworkElement Host;
            readonly TextBlock _top, _bottom, _flapTopText, _flapBottomText;
            readonly Border _flapTop, _flapBottom;
            char _current = '0';
            bool _animating;
            char _queued = '\0';

            public FlipCard()
            {
                var g = new Grid { Width = Wd, Height = H, Margin = new Thickness(3, 0, 3, 0) };
                Border topHalf, bottomHalf;
                _top = Half(true, out topHalf);
                _bottom = Half(false, out bottomHalf);
                g.Children.Add(topHalf);
                g.Children.Add(bottomHalf);
                _flapTopText = Half(true, out _flapTop);
                _flapBottomText = Half(false, out _flapBottom);
                _flapTop.RenderTransformOrigin = new Point(0.5, 1.0);
                _flapBottom.RenderTransformOrigin = new Point(0.5, 0.0);
                _flapTop.Visibility = Visibility.Hidden;
                _flapBottom.Visibility = Visibility.Hidden;
                g.Children.Add(_flapTop);
                g.Children.Add(_flapBottom);

                // the seam: a dark hairline with a faint light line under it
                g.Children.Add(new Border
                {
                    Height = 1.2, VerticalAlignment = VerticalAlignment.Center,
                    Background = new SolidColorBrush(Color.FromArgb(Theme.IsLight ? (byte)0x30 : (byte)0x90, 0, 0, 0)),
                    IsHitTestVisible = false,
                });
                Host = new Border { Child = g, Effect = Theme.Elevation(1) };
            }

            /// <summary>A half-card window. The digit inside is laid out at full card height
            /// and the window shows only its upper or lower half.</summary>
            static TextBlock Half(bool upper, out Border half)
            {
                var digit = new TextBlock
                {
                    Text = "0",
                    FontSize = 46,
                    FontFamily = Theme.LightFont,
                    FontWeight = Theme.LightWeight,
                    Foreground = Theme.Text,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FlowDirection = FlowDirection.LeftToRight,
                };
                var full = new Grid
                {
                    Height = H,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, upper ? 0 : -H / 2, 0, 0),
                };
                full.Children.Add(digit);
                var face = Theme.IsLight
                    ? new LinearGradientBrush(Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0xEE, 0xF2, 0xF7), 90)
                    : new LinearGradientBrush(
                        upper ? Color.FromRgb(0x2E, 0x34, 0x44) : Color.FromRgb(0x24, 0x29, 0x37),
                        upper ? Color.FromRgb(0x26, 0x2B, 0x39) : Color.FromRgb(0x1C, 0x20, 0x2C), 90);
                face.Freeze();
                half = new Border
                {
                    Height = H / 2,
                    VerticalAlignment = upper ? VerticalAlignment.Top : VerticalAlignment.Bottom,
                    CornerRadius = upper ? new CornerRadius(8, 8, 0, 0) : new CornerRadius(0, 0, 8, 8),
                    Background = face,
                    ClipToBounds = true,
                    Child = full,
                    IsHitTestVisible = false,
                };
                return digit;
            }

            public void SetImmediate(char c)
            {
                _current = c;
                _top.Text = _bottom.Text = c.ToString();
                _flapTop.Visibility = _flapBottom.Visibility = Visibility.Hidden;
                _animating = false;
            }

            public void FlipTo(char next)
            {
                if (next == _current && !_animating) return;
                if (_animating) { _queued = next; return; }
                if (Settings.SaverActive) { SetImmediate(next); return; }
                _animating = true;
                char old = _current;
                _current = next;

                // behind the falling flap the new top is already there; the old bottom stays
                // until the lower flap covers it
                _top.Text = next.ToString();
                _bottom.Text = old.ToString();
                _flapTopText.Text = old.ToString();
                _flapBottomText.Text = next.ToString();

                var fold = new ScaleTransform(1, 1);
                _flapTop.RenderTransform = fold;
                _flapTop.Visibility = Visibility.Visible;
                var unfold = new ScaleTransform(1, 0);
                _flapBottom.RenderTransform = unfold;
                _flapBottom.Visibility = Visibility.Visible;

                var down = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140))
                {
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn },
                };
                down.Completed += (s, e) =>
                {
                    _flapTop.Visibility = Visibility.Hidden;
                    var up = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))
                    {
                        EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut },
                    };
                    up.Completed += (s2, e2) =>
                    {
                        _bottom.Text = _current.ToString();
                        _flapBottom.Visibility = Visibility.Hidden;
                        _animating = false;
                        if (_queued != '\0' && _queued != _current) { char q = _queued; _queued = '\0'; FlipTo(q); }
                        else _queued = '\0';
                    };
                    unfold.BeginAnimation(ScaleTransform.ScaleYProperty, up);
                };
                fold.BeginAnimation(ScaleTransform.ScaleYProperty, down);
            }
        }
    }

    /// <summary>Word clock — the time written in words, big enough to read at a glance.
    /// Bilingual: Arabic phrase in one line, English beneath. The minute granularity is
    /// five-minute steps (matching how split-flap word clocks work in public spaces) so the
    /// list of strings stays manageable; exact minutes read in Arabic as a small suffix.</summary>
    public class WordClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly TextBlock _arWord, _enWord, _date, _hijri;

        // hours, 1..12. ar uses the sun-letter-based forms most clock apps use.
        static readonly string[] HoursAr =
        {
            "", "الواحدة", "الثانية", "الثالثة", "الرابعة", "الخامسة", "السادسة",
            "السابعة", "الثامنة", "التاسعة", "العاشرة", "الحادية عشرة", "الثانية عشرة"
        };
        static readonly string[] HoursEn =
        {
            "", "one", "two", "three", "four", "five", "six",
            "seven", "eight", "nine", "ten", "eleven", "twelve"
        };

        // minute buckets, index = minute / 5 (0..11). ar uses ساعة feminine form.
        static readonly string[] MinutesAr =
        {
            "تماماً", "وخمس", "وعشر", "وخمس عشرة", "وعشرون", "وخمس وعشرون",
            "وثلاثون", "وخمس وثلاثون", "وأربعون", "وخمس وأربعون", "وخمسون", "وخمس وخمسون"
        };
        static readonly string[] MinutesEn =
        {
            "o'clock", "five past", "ten past", "quarter past", "twenty past", "twenty-five past",
            "half past", "twenty-five to", "twenty to", "quarter to", "ten to", "five to"
        };

        public WordClock()
        {
            _arWord = Theme.MakeText("", 28, Theme.Text, Theme.LightFont);
            _arWord.HorizontalAlignment = HorizontalAlignment.Center;
            _arWord.TextWrapping = TextWrapping.Wrap;
            _arWord.MaxWidth = 360;
            _arWord.TextAlignment = TextAlignment.Center;
            _root.Children.Add(_arWord);

            _enWord = Theme.MakeText("", 13, Theme.TextDim);
            _enWord.HorizontalAlignment = HorizontalAlignment.Center;
            _enWord.FontStyle = FontStyles.Italic;
            _enWord.Margin = new Thickness(0, 2, 0, 0);
            _root.Children.Add(_enWord);

            _date = Theme.MakeText("", 13, Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _date.Margin = new Thickness(0, 8, 0, 0);
            _root.Children.Add(_date);
            _hijri = Theme.MakeText("", 11.5, Theme.TextFaint);
            _hijri.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_hijri);
        }

        public FrameworkElement Root { get { return _root; } }

        public void Tick(DateTime now)
        {
            var s = Settings.I;
            int h12 = now.Hour % 12; if (h12 == 0) h12 = 12;
            int bucket = now.Minute / 5;
            int rem = now.Minute % 5;
            // "quarter to" / "five to" reads against the NEXT hour; bucket == 7..11 belong to h+1
            int hForAr = bucket >= 7 ? (h12 % 12) + 1 : h12;
            if (hForAr == 13) hForAr = 1;
            int hForEn = hForAr;   // English uses the same convention

            if (L.IsAr)
            {
                string phrase = "الساعة " + HoursAr[hForAr] + " " + MinutesAr[bucket];
                if (rem > 0) phrase += " و" + RemAr(rem);
                _arWord.Text = phrase;
                _enWord.Text = ToEnglish(h12, bucket, rem);
            }
            else
            {
                _enWord.Text = ToEnglish(h12, bucket, rem);
                _arWord.Text = "الساعة " + HoursAr[hForAr] + " " + MinutesAr[bucket];
            }
            ClockDates.Paint(_date, _hijri, now);
        }

        static string RemAr(int m)
        {
            if (m == 1) return "دقيقة";
            if (m == 2) return "دقيقتين";
            return m + " دقائق";
        }

        static string ToEnglish(int h, int bucket, int rem)
        {
            string phrase = bucket == 0
                ? HoursEn[h] + " o'clock"
                : MinutesEn[bucket] + " " + HoursEn[bucket >= 7 ? (h % 12) + 1 : h];
            if (rem > 0) phrase += " " + rem;
            return phrase;
        }
    }

    /// <summary>Neon clock — glowing digits under a living sky strip: a real sun by day and a
    /// real moon (at its astronomical phase) by night ride a shallow arc ABOVE the time.
    /// v1.11: the old arc was centred 84px left of the stage, so at sunrise the sun hung
    /// outside the widget and it never reached the right half at all; the AM/PM letter sat
    /// under a stray horizon bar. The arc now spans the stage and stays inside it, and the
    /// AM/PM marker is the same pill every other face uses.</summary>
    public class NeonClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly TextBlock _time;
        readonly AmPmBadge _ampm;
        readonly TextBlock _date, _hijri;
        readonly Canvas _sky;
        readonly Grid _sun;
        readonly MoonDisc _moon;
        const double W = 264, H = 122;
        const double ArcCx = W / 2, ArcBase = 42, ArcRx = 104, ArcRy = 26;
        static DropShadowEffect _glow;   // one effect, frozen; rebuilt only on theme change

        public NeonClock()
        {
            _sky = new Canvas { Width = W, Height = H, FlowDirection = FlowDirection.LeftToRight };
            // (no backdrop box: on the card it read as a stray grey panel behind the time)
            _sun = SkyBodies.BuildSun(9, true);
            Canvas.SetZIndex(_sun, 1);
            _sky.Children.Add(_sun);
            _moon = new MoonDisc(10);
            Canvas.SetZIndex(_moon, 1);
            _sky.Children.Add(_moon);

            _time = Theme.MakeText("", 54, Theme.Accent, Theme.LightFont);
            _time.FontWeight = FontWeights.Normal;
            _time.FlowDirection = FlowDirection.LeftToRight;
            _time.VerticalAlignment = VerticalAlignment.Bottom;
            _glow = MakeGlow();
            _time.Effect = _glow;
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 6),
                FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            };
            row.Children.Add(_time);
            _ampm = new AmPmBadge(54);
            row.Children.Add(_ampm.Root);

            var dial = new Grid { Width = W, Height = H, FlowDirection = FlowDirection.LeftToRight };
            dial.Children.Add(_sky);
            dial.Children.Add(row);
            _root.Children.Add(dial);

            _date = Theme.MakeText("", 14.5, Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _date.Margin = new Thickness(0, 4, 0, 0);
            _root.Children.Add(_date);
            _hijri = Theme.MakeText("", 12.5, Theme.TextFaint);
            _hijri.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_hijri);
        }

        /// <summary>Power-saving state baked into the current <see cref="_glow"/>.</summary>
        static bool _glowSaver;

        static DropShadowEffect MakeGlow()
        {
            _glowSaver = Settings.SaverActive;
            var e = new DropShadowEffect
            {
                Color = Theme.AccentColor,
                // a Gaussian blur is the most expensive thing on this face and its cost
                // scales with the radius; power saving keeps the halo and makes it cheap
                BlurRadius = _glowSaver ? 6 : 16,
                ShadowDepth = 0,
                Opacity = 0.85,
            };
            e.Freeze();
            return e;
        }

        public FrameworkElement Root { get { return _root; } }

        static Point OnArc(double f)
        {
            double ang = Math.PI * (1 - f);
            return new Point(ArcCx + ArcRx * Math.Cos(ang), ArcBase - ArcRy * Math.Sin(ang));
        }

        public void Tick(DateTime now)
        {
            var s = Settings.I;
            if (_glow.Color != Theme.AccentColor || _glowSaver != Settings.SaverActive)
            {
                _glow = MakeGlow();
                _time.Effect = _glow;
            }

            double sunrise = 6, sunset = 18;
            double[] times = DeskMate.Features.PrayerFeature.PublicToday();
            if (times != null && !double.IsNaN(times[1]) && !double.IsNaN(times[4]))
            {
                sunrise = times[1];
                sunset = times[4];
            }
            double h = now.TimeOfDay.TotalHours;
            bool day = h >= sunrise && h <= sunset;
            if (day)
            {
                _sun.Visibility = Visibility.Visible;
                _moon.Visibility = Visibility.Collapsed;
                var p = OnArc((h - sunrise) / Math.Max(0.001, sunset - sunrise));
                Canvas.SetLeft(_sun, p.X - _sun.Width / 2);
                Canvas.SetTop(_sun, p.Y - _sun.Height / 2);
            }
            else
            {
                _sun.Visibility = Visibility.Hidden;
                _moon.Visibility = Visibility.Visible;
                double nightStart = h >= sunrise ? sunset : sunset - 24;
                double nightEnd = nightStart + (24 - (sunset - sunrise));
                double nf = Math.Max(0, Math.Min(1, (h - nightStart) / Math.Max(0.001, nightEnd - nightStart)));
                var p = OnArc(nf);
                Canvas.SetLeft(_moon, p.X - _moon.Radius);
                Canvas.SetTop(_moon, p.Y - _moon.Radius);
                _moon.UpdatePhase(now);
            }

            string fmt = s.Use24h
                ? (s.ShowSeconds ? "HH:mm:ss" : "HH:mm")
                : (s.ShowSeconds ? "h:mm:ss" : "h:mm");
            _time.Text = now.ToString(fmt, CultureInfo.InvariantCulture);
            _ampm.Update(now);
            ClockDates.Paint(_date, _hijri, now);
        }
    }

    /// <summary>Binary clock — each digit of HH:MM as a 2×2 grid of glowing dots (BCD):
    /// the left column carries bits 1 (top) and 2 (bottom), the right column bits 4 and 8.
    /// A quiet geek classic; the date lines keep it readable for everyone else.</summary>
    public class BinaryClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly TextBlock _date, _hijri;
        // per-digit dot grids, laid out left→right in the order the digits appear
        readonly Grid[] _digitGrids = new Grid[4];

        public BinaryClock()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Thickness(0, 6, 0, 4),
            };
            for (int i = 0; i < 4; i++)
            {
                var g = new Grid { Margin = new Thickness(5, 0, 5, 0) };
                // 2 columns (bit 1 | bit 2 on top row, bit 4 | bit 8 below) × 2 rows
                for (int c = 0; c < 2; c++)
                    for (int r = 0; r < 2; r++)
                        g.Children.Add(MakeDot(c, r));
                _digitGrids[i] = g;
                row.Children.Add(g);
                if (i == 1) row.Children.Add(MakeColon());
            }
            _root.Children.Add(row);

            _date = Theme.MakeText("", 14, Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _date.Margin = new Thickness(0, 4, 0, 0);
            _root.Children.Add(_date);
            _hijri = Theme.MakeText("", 12, Theme.TextFaint);
            _hijri.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_hijri);
        }

        static System.Windows.Shapes.Ellipse MakeDot(int col, int row)
        {
            var e = new System.Windows.Shapes.Ellipse
            {
                Width = 13, Height = 13,
                Fill = Theme.TextFaint,
                Opacity = 0.28,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(col * 19 + 2, row * 19 + 2, 0, 0),
            };
            return e;
        }

        static FrameworkElement MakeColon()
        {
            var g = new Grid { Width = 10, Margin = new Thickness(2, 6, 2, 6) };
            var top = new System.Windows.Shapes.Ellipse
            {
                Width = 7, Height = 7, Fill = Theme.Accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 0, 0),
            };
            var bot = new System.Windows.Shapes.Ellipse
            {
                Width = 7, Height = 7, Fill = Theme.Accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 8),
            };
            g.Children.Add(top);
            g.Children.Add(bot);
            return g;
        }

        public FrameworkElement Root { get { return _root; } }

        public void Tick(DateTime now)
        {
            var s = Settings.I;
            string t = now.ToString(s.Use24h ? "HHmm" : "hmm", CultureInfo.InvariantCulture);
            while (t.Length < 4) t = "0" + t;   // 12h "100" → "0100"
            for (int d = 0; d < 4 && d < t.Length; d++)
                PaintDigit(_digitGrids[d], t[d] - '0');
            ClockDates.Paint(_date, _hijri, now);
        }

        /// <summary>Lights the dots encoding <paramref name="value"/> (0..9) in BCD:
        /// the left column holds bits 1 (top) and 2 (bottom), the right column bits 4
        /// and 8 — so 9 lights the whole grid, 6 lights everything but the bottom-left.</summary>
        static void PaintDigit(Grid g, int value)
        {
            int bit = 1;
            foreach (System.Windows.Shapes.Ellipse dot in g.Children)
            {
                bool on = (value & bit) != 0;
                dot.Fill = on ? Theme.Accent : Theme.TextFaint;
                dot.Opacity = on ? 1.0 : 0.28;
                dot.Effect = on ? Theme.Elevation(1) : null;
                bit <<= 1;
            }
        }
    }

    /// <summary>Day-progress ring — an analog-style dial whose single accent arc sweeps from
    /// midnight to midnight with the sun marker at noon, so one glance answers "how much of
    /// my day is gone". The centre shows the time; the arc is exact, not decorative.</summary>
    public class DayRingClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly TextBlock _time, _date, _hijri;
        readonly AmPmBadge _ampm;
        readonly System.Windows.Media.ArcSegment _arc;
        readonly System.Windows.Shapes.Path _arcPath;
        const double Dial = 168;

        public DayRingClock()
        {
            double r = Dial / 2 - 9;
            // faint full circle as the track, then the live accent arc on top
            var track = new System.Windows.Shapes.Ellipse
            {
                Width = Dial - 18, Height = Dial - 18,
                Stroke = Theme.CardBorder, StrokeThickness = 3,
            };
            var fig = new PathFigure { StartPoint = new Point(Dial / 2, 9) };   // 12 o'clock
            _arc = new System.Windows.Media.ArcSegment
            {
                Size = new Size(r, r),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = false,
                Point = new Point(Dial / 2, 9),
            };
            fig.Segments.Add(_arc);
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            _arcPath = new System.Windows.Shapes.Path
            {
                Stroke = Theme.Accent,
                StrokeThickness = 5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = geo,
            };

            var dial = new Grid { Width = Dial, Height = Dial, FlowDirection = FlowDirection.LeftToRight, Tag = Footprint.Round };
            dial.Children.Add(track);
            dial.Children.Add(_arcPath);

            var centre = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            _time = Theme.MakeText("", 36, Theme.Text, Theme.LightFont);
            _time.HorizontalAlignment = HorizontalAlignment.Center;
            _time.FlowDirection = FlowDirection.LeftToRight;
            centre.Children.Add(_time);
            _ampm = new AmPmBadge(36);
            _ampm.Root.HorizontalAlignment = HorizontalAlignment.Center;
            _ampm.Root.Margin = new Thickness(0, 4, 0, 0);
            centre.Children.Add(_ampm.Root);
            dial.Children.Add(centre);

            // tick marks at 6 / 12 / 18 / 24 h so the sweep has anchors — drawn as straight
            // lines between two radii (transform origins are relative to the element, so
            // rotating a small rect about the dial centre needs this explicit geometry)
            double cx = Dial / 2, cy = Dial / 2;
            for (int i = 0; i < 4; i++)
            {
                double ang = i * Math.PI / 2;
                var tick = new System.Windows.Shapes.Line
                {
                    X1 = cx + 73 * Math.Sin(ang),
                    Y1 = cy - 73 * Math.Cos(ang),
                    X2 = cx + 82 * Math.Sin(ang),
                    Y2 = cy - 82 * Math.Cos(ang),
                    Stroke = Theme.TextFaint,
                    StrokeThickness = 2.4,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                };
                dial.Children.Add(tick);
            }

            _root.Children.Add(dial);
            _date = Theme.MakeText("", 14.5, Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _date.Margin = new Thickness(0, 6, 0, 0);
            _root.Children.Add(_date);
            _hijri = Theme.MakeText("", 12.5, Theme.TextFaint);   // same pattern as DigitalClock
            _hijri.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_hijri);
        }

        public FrameworkElement Root { get { return _root; } }

        public void Tick(DateTime now)
        {
            var s = Settings.I;
            string fmt = s.Use24h ? "HH:mm" : "h:mm";
            if (s.ShowSeconds) fmt += ":ss";
            _time.Text = now.ToString(fmt, CultureInfo.InvariantCulture);
            _ampm.Update(now);

            // fraction of the day elapsed → sweep the arc clockwise from 12 o'clock
            // The arc advances 0.004° per second: real, and far below anything an eye can
            // resolve. Including seconds only when the user asked for them keeps the face
            // repainting on the minute the rest of the time.
            double secs = s.ShowSeconds ? now.Second : 0;
            double frac = (now.Hour * 3600.0 + now.Minute * 60.0 + secs) / 86400.0;
            if (frac <= 0.00001 || frac >= 0.99999)
            {
                // degenerate arcs render nothing — hide instead of drawing a zero-length line
                _arcPath.Visibility = Visibility.Hidden;
            }
            else
            {
                _arcPath.Visibility = Visibility.Visible;
                double r = Dial / 2 - 9;
                double angle = frac * 2 * Math.PI;
                double x = Dial / 2 + r * Math.Sin(angle);
                double y = Dial / 2 - r * Math.Cos(angle);   // start at top, sweep clockwise
                _arc.Point = new Point(x, y);
                _arc.IsLargeArc = frac > 0.5;
            }
            ClockDates.Paint(_date, _hijri, now);
        }
    }

    /// <summary>Sun-path clock — a half-dome sky over a horizon line. The sun rides an arc
    /// from today's real sunrise (left) to sunset (right); the part of the arc it has already
    /// travelled is lit in the accent, the rest is a dotted track. At night the moon, at its
    /// true phase, rides the same track. v1.11 rebuild: the arc is an ellipse cut to the
    /// dial so the sun — rays included — never leaves the face (it used to poke out of the
    /// card at sunrise and sunset), the moon no longer skims low across the AM/PM marker,
    /// and the heavy ground slab under the horizon is gone.</summary>
    public class SunPathClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly TextBlock _time, _date, _hijri;
        readonly AmPmBadge _ampm;
        readonly Canvas _sky;
        readonly Path _track, _travelled;
        readonly ArcSegment _travelArc;
        readonly Grid _sun;                 // layered real sun
        readonly MoonDisc _moon;            // phase-accurate night moon
        const double W = 252, H = 128;      // the dial box
        const double HorizonY = 112;        // horizon line
        const double Rx = 96, Ry = 82;      // the sky arc (ellipse) — clears the sun's rays
        const double Cx = W / 2;

        public SunPathClock()
        {
            _sky = new Canvas { Width = W, Height = H };

            // horizon: a hairline that fades out at both ends instead of a hard slab
            var hz = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            var hc = Theme.IsLight ? Color.FromRgb(0x16, 0x1E, 0x2A) : Colors.White;
            hz.GradientStops.Add(new GradientStop(Color.FromArgb(0, hc.R, hc.G, hc.B), 0));
            hz.GradientStops.Add(new GradientStop(Color.FromArgb(Theme.IsLight ? (byte)0x50 : (byte)0x60, hc.R, hc.G, hc.B), 0.5));
            hz.GradientStops.Add(new GradientStop(Color.FromArgb(0, hc.R, hc.G, hc.B), 1));
            hz.Freeze();
            var horizon = new Rectangle { Width = W - 16, Height = 1.5, Fill = hz };
            Canvas.SetLeft(horizon, 8);
            Canvas.SetTop(horizon, HorizonY - 0.75);
            _sky.Children.Add(horizon);

            // the whole path as a dotted track
            var fig = new PathFigure { StartPoint = new Point(Cx - Rx, HorizonY) };
            fig.Segments.Add(new ArcSegment
            {
                Size = new Size(Rx, Ry), SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = false, Point = new Point(Cx + Rx, HorizonY),
            });
            var geo = new PathGeometry(); geo.Figures.Add(fig); geo.Freeze();
            _track = new Path
            {
                Data = geo, Stroke = Theme.TextFaint, StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 0.1, 3.2 },
                StrokeDashCap = PenLineCap.Round,
                Opacity = 0.75,
            };
            _sky.Children.Add(_track);

            // the travelled part, lit
            var tf = new PathFigure { StartPoint = new Point(Cx - Rx, HorizonY) };
            _travelArc = new ArcSegment { Size = new Size(Rx, Ry), SweepDirection = SweepDirection.Clockwise, IsLargeArc = false, Point = new Point(Cx - Rx, HorizonY) };
            tf.Segments.Add(_travelArc);
            var tg = new PathGeometry(); tg.Figures.Add(tf);
            _travelled = new Path
            {
                Data = tg, Stroke = Theme.Accent, StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            };
            _sky.Children.Add(_travelled);

            _sun = SkyBodies.BuildSun(10, true);
            Canvas.SetZIndex(_sun, 5);
            _sky.Children.Add(_sun);
            _moon = new MoonDisc(11);
            Canvas.SetZIndex(_moon, 4);
            _sky.Children.Add(_moon);

            var dial = new Grid { Width = W, Height = H, FlowDirection = FlowDirection.LeftToRight, Tag = Footprint.Dome };
            dial.Children.Add(_sky);

            // time + AM/PM, sitting on the horizon under the apex of the arc
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, H - HorizonY + 6),
                FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            };
            _time = Theme.MakeText("", 38, Theme.Text, Theme.LightFont);
            _time.FlowDirection = FlowDirection.LeftToRight;
            _time.VerticalAlignment = VerticalAlignment.Bottom;
            row.Children.Add(_time);
            _ampm = new AmPmBadge(38);
            row.Children.Add(_ampm.Root);
            dial.Children.Add(row);

            _root.Children.Add(dial);
            _date = Theme.MakeText("", 14.5, Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _date.Margin = new Thickness(0, 2, 0, 0);
            _root.Children.Add(_date);
            _hijri = Theme.MakeText("", 12.5, Theme.TextFaint);
            _hijri.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_hijri);
        }

        public FrameworkElement Root { get { return _root; } }

        /// <summary>Point on the sky arc at fraction f (0 = left horizon, 1 = right).</summary>
        static Point OnArc(double f)
        {
            double ang = Math.PI * (1 - f);
            return new Point(Cx + Rx * Math.Cos(ang), HorizonY - Ry * Math.Sin(ang));
        }

        public void Tick(DateTime now)
        {
            var s = Settings.I;

            double sunrise = 6, sunset = 18;
            double[] times = DeskMate.Features.PrayerFeature.PublicToday();
            if (times != null && !double.IsNaN(times[1]) && !double.IsNaN(times[4]))
            {
                sunrise = times[1];
                sunset = times[4];
            }
            double h = now.TimeOfDay.TotalHours;
            double frac = (h - sunrise) / Math.Max(0.001, sunset - sunrise);
            bool day = frac >= 0 && frac <= 1;
            if (day)
            {
                var p = OnArc(frac);
                _sun.Visibility = Visibility.Visible;
                _moon.Visibility = Visibility.Collapsed;
                Canvas.SetLeft(_sun, p.X - _sun.Width / 2);
                Canvas.SetTop(_sun, p.Y - _sun.Height / 2);
                _travelArc.Point = p;
                _travelled.Visibility = frac > 0.004 ? Visibility.Visible : Visibility.Hidden;
            }
            else
            {
                _sun.Visibility = Visibility.Hidden;
                _moon.Visibility = Visibility.Visible;
                _travelled.Visibility = Visibility.Hidden;
                // night fraction: sunset → next sunrise mapped onto the same track
                double nightStart = h >= sunrise ? sunset : sunset - 24;
                double nightEnd = nightStart + (24 - (sunset - sunrise));
                double nf = Math.Max(0, Math.Min(1, (h - nightStart) / Math.Max(0.001, nightEnd - nightStart)));
                var p = OnArc(nf);
                Canvas.SetLeft(_moon, p.X - _moon.Radius);
                Canvas.SetTop(_moon, p.Y - _moon.Radius);
                _moon.UpdatePhase(now);
            }

            string fmt = s.Use24h ? "HH:mm" : "h:mm";
            if (s.ShowSeconds) fmt += ":ss";
            _time.Text = now.ToString(fmt, CultureInfo.InvariantCulture);
            _ampm.Update(now);
            ClockDates.Paint(_date, _hijri, now);
        }
    }

    /// <summary>AM/PM marker shared by the dial faces: an accent pill holding the vector
    /// ص/م outline in Arabic (immune to the isolated-letter shaping defect that mirrored
    /// the meem in the layered window) or AM/PM text in English. Hidden in 24-hour mode
    /// and on very small widgets.</summary>
    sealed class AmPmBadge
    {
        public readonly Border Root;
        readonly TextBlock _text;
        readonly Path _glyph = new Path { Stretch = Stretch.Uniform };
        readonly Viewbox _glyphHost;

        /// <param name="timeSize">Font size of the time it sits beside, to align baselines.</param>
        public AmPmBadge(double timeSize)
        {
            _text = Theme.MakeText("", 12, Theme.Accent);
            _text.FontWeight = FontWeights.Bold;
            _text.FlowDirection = FlowDirection.LeftToRight;
            _glyph.Fill = Theme.Accent;
            _glyphHost = new Viewbox { Child = _glyph, Height = 12, FlowDirection = FlowDirection.LeftToRight };
            var content = new Grid();
            content.Children.Add(_text);
            content.Children.Add(_glyphHost);
            var pill = new SolidColorBrush(Color.FromArgb(Theme.IsLight ? (byte)0x22 : (byte)0x2A,
                Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            pill.Freeze();
            Root = new Border
            {
                Background = pill,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 3, 7, 3),
                Child = content,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(8, 0, 8, Math.Round(timeSize * 0.2)),
            };
        }

        public void Update(DateTime now)
        {
            var s = Settings.I;
            bool show = !s.Use24h && s.Scale >= 0.85;
            Root.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show) return;
            bool ar = L.IsAr;
            _text.Visibility = ar ? Visibility.Collapsed : Visibility.Visible;
            _glyphHost.Visibility = ar ? Visibility.Visible : Visibility.Collapsed;
            if (ar) _glyph.Data = now.Hour < 12 ? AmPmGlyph.Sad : AmPmGlyph.Meem;
            else _text.Text = now.Hour < 12 ? "AM" : "PM";
        }
    }


    /// <summary>A real moon: a pale disc whose lit share follows the actual astronomical
    /// phase, with a cratered surface that shows only where sunlight falls. Two layered
    /// shapes do all eight phases: a bright half-disc on the lit limb, plus a terminator
    /// half-ellipse that either carries shadow into the lit side (crescents) or light
    /// across the centre (gibbous/full). v1.10: the lit face is limb-darkened (brighter
    /// toward the Sun side, dimmer at the edge — real lunar scattering), seven fixed
    /// craters ride the lit region, a thin atmosphere glow halos the disc, and the old
    /// full-moon seam (a hard half-dark line at 100% phase) is gone: at fullness the whole
    /// disc lights. Brushes are cached static — this repaints every second at night.</summary>
    public class MoonDisc : Canvas
    {
        static readonly Brush ShadowFill = Mk(Color.FromRgb(0x8E, 0x93, 0xA4), 0x38);
        static readonly Brush DarkFill = Mk(Color.FromRgb(0x8E, 0x93, 0xA4), 0x30);
        static readonly Brush RimStroke = Mk(Color.FromRgb(0xC9, 0xCE, 0xDC), 0x50);
        // limb-darkened lit faces: the Sun-side limb is brightest, the far edge falls off
        static readonly Brush LitWax = LitBrush(0.78);
        static readonly Brush LitWane = LitBrush(0.22);

        static Brush Mk(Color c, byte a)
        {
            var b = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
            b.Freeze();
            return b;
        }

        static Brush LitBrush(double originX)
        {
            var b = new RadialGradientBrush
            {
                GradientOrigin = new Point(originX, 0.42),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.75, RadiusY = 0.75,
            };
            b.GradientStops.Add(new GradientStop(Color.FromRgb(0xFD, 0xFC, 0xF4), 0));
            b.GradientStops.Add(new GradientStop(Color.FromRgb(0xE9, 0xE7, 0xD9), 0.6));
            b.GradientStops.Add(new GradientStop(Color.FromRgb(0xC9, 0xC7, 0xB8), 1));
            b.Freeze();
            return b;
        }

        readonly Path _litLimb = new Path();
        readonly Path _terminator = new Path { Fill = ShadowFill };
        readonly Canvas _craters = new Canvas { IsHitTestVisible = false };
        readonly double _r;

        /// <summary>Pixel radius of the disc — callers offset by this when placing on a Canvas.</summary>
        public double Radius { get { return _r; } }

        public MoonDisc(double radius)
        {
            _r = radius;
            Width = Height = radius * 2;

            // atmosphere glow: a cold halo that makes the disc read as hanging in a sky
            var glow = new Ellipse
            {
                Width = radius * 3, Height = radius * 3,
                IsHitTestVisible = false,
            };
            var gb = new RadialGradientBrush { GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5) };
            gb.GradientStops.Add(new GradientStop(Color.FromArgb(0x40, 0xC9, 0xD8, 0xFF), 0.55));
            gb.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            glow.Fill = gb;
            Canvas.SetLeft(glow, -radius * 0.5);
            Canvas.SetTop(glow, -radius * 0.5);
            Children.Add(glow);

            // base disc: shadow tone + faint earthshine rim keeps new-moon visible
            var dark = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = DarkFill };
            Children.Add(dark);
            Children.Add(_litLimb);
            Children.Add(_terminator);

            // craters — fixed positions (a real surface, not noise), clipped to the lit
            // region each repaint so the dark side stays smooth earthshine
            BuildCraters(radius);
            Children.Add(_craters);

            var rim = new Ellipse { Width = radius * 2, Height = radius * 2, Stroke = RimStroke, StrokeThickness = 1 };
            Children.Add(rim);
        }

        /// <summary>Seven craters at hand-tuned unit positions (fractions of the disc),
        /// each a soft dark pool with a brighter far rim — the way low sun angle carves
        /// real relief. Deterministic: the moon must not rearrange its face every rebuild.</summary>
        void BuildCraters(double r)
        {
            double[,] craters =
            {   //  x,     y,     size   (unit fractions of the radius)
                { 0.52, 0.34, 0.30 },
                { 0.86, 0.62, 0.24 },
                { 0.38, 0.88, 0.18 },
                { 1.02, 0.30, 0.17 },
                { 0.30, 0.58, 0.14 },
                { 0.68, 1.05, 0.13 },
                { 0.95, 0.92, 0.10 },
            };
            for (int i = 0; i < craters.GetLength(0); i++)
            {
                double d = craters[i, 2] * r * 2;
                var fill = new RadialGradientBrush { GradientOrigin = new Point(0.42, 0.4), Center = new Point(0.5, 0.5) };
                fill.GradientStops.Add(new GradientStop(Color.FromArgb(0x59, 0x8A, 0x87, 0x7A), 0));
                fill.GradientStops.Add(new GradientStop(Color.FromArgb(0x30, 0x9A, 0x97, 0x8A), 0.7));
                fill.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
                fill.Freeze();
                var e = new Ellipse { Width = d, Height = d, Fill = fill, IsHitTestVisible = false };
                Canvas.SetLeft(e, craters[i, 0] * r * 2 - d / 2 + r * 0.0);
                Canvas.SetTop(e, craters[i, 1] * r * 2 - d / 2);
                _craters.Children.Add(e);
            }
            _craters.Width = _craters.Height = r * 2;
        }

        /// <summary>Repaints the phase. Waxing lights the right limb (northern-hemisphere
        /// view), waning the left. cos(phase) signs the terminator: positive near new →
        /// shadow bites the lit limb; negative near full → light spills across the centre.
        /// The crater layer is clipped to the sunlit region, so relief lives only in the
        /// light. Near-full phases light the ENTIRE disc — the old build left a dark
        /// half-disc seam at 100%, which read as a bug against the phase name «بدر».</summary>
        public void UpdatePhase(DateTime now)
        {
            double f = DeskMate.Logic.MoonPhase.AgeFraction(now);
            bool waxing = f < 0.5;
            double k = Math.Cos(2 * Math.PI * f);          // +1 new … −1 full … +
            double ax = Math.Max(0.01, _r * Math.Abs(k));  // terminator semi-axis
            var litFace = waxing ? LitWax : LitWane;
            _litLimb.Fill = litFace;

            Geometry lit;
            if (k <= -0.985)
            {
                // (near) full: one lit disc, no terminator
                lit = new EllipseGeometry(new Point(_r, _r), _r, _r);
                _litLimb.Data = lit;
                _terminator.Visibility = Visibility.Collapsed;
            }
            else
            {
                var limb = LimbGeo(waxing);
                _litLimb.Data = limb;
                _terminator.Fill = k > 0 ? ShadowFill : litFace;
                // crescent (k>0): shadow carves the LIT side; gibbous (k<0): light spills onto
                // the DARK side. Either way the bulge points lit-side when k>0, dark-side when k<0.
                var term = TermGeo(ax, (k > 0) == waxing);
                _terminator.Data = term;
                _terminator.Visibility = term == null ? Visibility.Collapsed : Visibility.Visible;
                if (term == null) lit = limb;
                else if (k < 0) lit = new CombinedGeometry(GeometryCombineMode.Union, limb, term);
                else lit = new CombinedGeometry(GeometryCombineMode.Exclude, limb, term);
            }
            _craters.Clip = lit;
        }

        /// <summary>Bright semicircle: vertical diameter chord, arc through the lit side.
        /// On screen (Y-down) top→right→bottom is Clockwise.</summary>
        Geometry LimbGeo(bool rightSide)
        {
            var fig = new PathFigure { StartPoint = new Point(_r, 0) };
            fig.Segments.Add(new ArcSegment
            {
                Size = new Size(_r, _r),
                SweepDirection = rightSide ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                IsLargeArc = false,
                Point = new Point(_r, 2 * _r),
            });
            var geo = new PathGeometry(); geo.Figures.Add(fig); geo.Freeze();
            return geo;
        }

        /// <summary>The terminator half-ellipse from top-centre to bottom-centre; the implicit
        /// closing chord makes its fill exactly the half-disc it bulges into.</summary>
        Geometry TermGeo(double ax, bool bulgeRight)
        {
            if (ax >= _r - 0.01) return null;   // full moon: no shadow half to paint
            var fig = new PathFigure { StartPoint = new Point(_r, 0) };
            fig.Segments.Add(new ArcSegment
            {
                Size = new Size(ax, _r),
                SweepDirection = bulgeRight ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                IsLargeArc = false,
                Point = new Point(_r, 2 * _r),
            });
            var geo = new PathGeometry(); geo.Figures.Add(fig); geo.Freeze();
            return geo;
        }
    }

    /// <summary>Moon-phase clock (style 10) — a night-sky dome that is NOT a sun-path clone:
    /// a deep navy well with scattered stars, the moon at its true phase floating above a
    /// thin horizon, the phase name beneath the dial, and time/date lines like every other
    /// style. Wide landscape shape so it can't be confused with the round dials.</summary>
    public class MoonPhaseClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly TextBlock _time, _date, _hijri, _phaseName;
        readonly AmPmBadge _ampm;
        readonly MoonDisc _moon;
        const double W = 240, H = 118;
        readonly Canvas _sky;

        public MoonPhaseClock()
        {
            _sky = BuildNightSky(W, H);

            // the moon lives on its OWN overlay canvas so Canvas.SetLeft/Top work
            // (a Grid parent ignores them and would stretch the disc across the dial)
            var moonLayer = new Canvas { Width = W, Height = H, IsHitTestVisible = false };
            _moon = new MoonDisc(17);
            moonLayer.Children.Add(_moon);

            var dial = new Grid { Width = W, Height = H, FlowDirection = FlowDirection.LeftToRight, Tag = Footprint.Dome };
            dial.Children.Add(_sky);
            dial.Children.Add(moonLayer);

            var centre = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16),
                FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            };
            _time = Theme.MakeText("", 36, Theme.Text, Theme.LightFont);
            _time.VerticalAlignment = VerticalAlignment.Bottom;
            _time.FlowDirection = FlowDirection.LeftToRight;
            centre.Children.Add(_time);
            _ampm = new AmPmBadge(36);
            centre.Children.Add(_ampm.Root);
            dial.Children.Add(centre);

            _root.Children.Add(dial);
            _phaseName = Theme.MakeText("", 13, Theme.Accent);
            _phaseName.FontWeight = FontWeights.Medium;
            _phaseName.HorizontalAlignment = HorizontalAlignment.Center;
            _phaseName.Margin = new Thickness(0, 4, 0, 0);
            _root.Children.Add(_phaseName);
            _date = Theme.MakeText("", 14.5, Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_date);
            _hijri = Theme.MakeText("", 12.5, Theme.TextFaint);
            _hijri.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_hijri);
        }

        /// <summary>The night dome: radial midnight gradient, ~26 deterministic stars
        /// (hash-seeded so they don't jump every rebuild), horizon line at the base and the
        /// phase-true moon floating upper-right of the time text. The moon is added to the
        /// DIAL grid (not this canvas) by the constructor, so nothing here is parented twice.</summary>
        static Canvas BuildNightSky(double w, double h)
        {
            var sky = new Canvas { Width = w, Height = h };

            var dome = new System.Windows.Shapes.Path { Stretch = Stretch.None };
            // an ellipse cut to the box: the old circular dome (radius = half the width) rose
            // above the top of its own frame
            double r = w / 2 - 6, ry = h - 12;
            var fig = new PathFigure { StartPoint = new Point(6, h - 10) };
            fig.Segments.Add(new ArcSegment
            {
                Size = new Size(r, ry), SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = false, Point = new Point(w - 6, h - 10),
            });
            var domeGeo = new PathGeometry(); domeGeo.Figures.Add(fig); domeGeo.Freeze();
            var domeFill = new RadialGradientBrush { Center = new Point(0.5, 0.85), GradientOrigin = new Point(0.5, 0.85) };
            domeFill.GradientStops.Add(new GradientStop(Color.FromRgb(0x14, 0x1D, 0x3A), 0));
            domeFill.GradientStops.Add(new GradientStop(Color.FromRgb(0x08, 0x0C, 0x1E), 1));
            dome.Data = domeGeo;
            dome.Fill = domeFill;
            dome.Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            dome.StrokeThickness = 1;
            sky.Children.Add(dome);

            // stars — positions derived from a stable hash so each rebuild draws the same sky
            var starBrush = new SolidColorBrush(Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF)); starBrush.Freeze();
            var dimStar = new SolidColorBrush(Color.FromArgb(0x54, 0xD8, 0xE4, 0xFF)); dimStar.Freeze();
            for (int i = 0; i < 26; i++)
            {
                int hash = (int)((i * 2654435761L) & 0x7FFFFFFF);
                double fx = (hash % 1000) / 1000.0;
                int h2 = (i * 40503 + 97) & 0x7FFFFFFF;
                double fy = (h2 % 1000) / 1000.0;
                // keep stars inside the dome and off the very edges
                double x = 16 + fx * (w - 32);
                double yTop = h - 18;
                double y = 8 + fy * (yTop - 14);
                double dx = x - w / 2, dy = y - (h - 10);
                if (dx * dx / (r * r) + dy * dy / (ry * ry) > 0.82) continue;   // outside dome → skip
                bool big = i % 6 == 0;
                var s = new System.Windows.Shapes.Rectangle
                {
                    Width = big ? 2.2 : 1.4, Height = big ? 2.2 : 1.4,
                    RadiusX = 1, RadiusY = 1,
                    Fill = big ? starBrush : dimStar,
                };
                Canvas.SetLeft(s, x);
                Canvas.SetTop(s, y);
                sky.Children.Add(s);
            }

            // thin ground line inside the same canvas — a second parent would throw
            // "element is already the logical child of another element"
            var ground = new System.Windows.Shapes.Rectangle
            {
                Width = w - 60, Height = 6,
                RadiusX = 3, RadiusY = 3,
                Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                Opacity = 0.8,
            };
            Canvas.SetLeft(ground, 30);
            Canvas.SetTop(ground, h - 9);
            sky.Children.Add(ground);
            return sky;
        }

        public FrameworkElement Root { get { return _root; } }

        public void Tick(DateTime now)
        {
            var s = Settings.I;

            // moon parked in the dome's upper-right sky: clear of the centred time text at
            // any hour, and the phase repaint is the live element here — not a position race
            double mx = W - 50;
            double my = 33;
            Canvas.SetLeft(_moon, mx - _moon.Radius);
            Canvas.SetTop(_moon, my - _moon.Radius);
            _moon.UpdatePhase(now);

            string fmt = s.Use24h ? "HH:mm" : "h:mm";
            if (s.ShowSeconds) fmt += ":ss";
            _time.Text = now.ToString(fmt, CultureInfo.InvariantCulture);
            bool ar = L.IsAr;
            _ampm.Update(now);

            _phaseName.Text = ar ? DeskMate.Logic.MoonPhase.NameAr(now)
                                 : "☾ " + DeskMate.Logic.MoonPhase.NameEn(now);
            ClockDates.Paint(_date, _hijri, now);
        }
    }


    /// <summary>Day-stripes clock — 24 slim vertical bars, one per hour; past hours glow in
    /// the accent, the current hour pulses brighter with the minutes shown beneath, and the
    /// rest stay faint. Reads like a wall of tally marks: how much day is spent, how much
    /// is left.</summary>
    public class DayStripesClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly TextBlock _time, _date, _hijri;
        readonly AmPmBadge _ampm;
        readonly StackPanel _stripesRow;
        readonly Border[] _bars = new Border[24];

        public DayStripesClock()
        {
            var timeRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            };
            _time = Theme.MakeText("", 42, Theme.Text, Theme.LightFont);
            _time.FlowDirection = FlowDirection.LeftToRight;
            _time.VerticalAlignment = VerticalAlignment.Bottom;
            timeRow.Children.Add(_time);
            _ampm = new AmPmBadge(42);
            timeRow.Children.Add(_ampm.Root);
            _root.Children.Add(timeRow);

            var stripesHost = new Grid { Width = 312, Height = 34, Margin = new Thickness(0, 8, 0, 2), FlowDirection = FlowDirection.LeftToRight };
            _stripesRow = new StackPanel { Orientation = Orientation.Horizontal };
            stripesHost.Children.Add(_stripesRow);
            for (int i = 0; i < 24; i++)
            {
                var b = new Border
                {
                    Width = 8,
                    Height = 26,
                    CornerRadius = new CornerRadius(3),
                    Background = Theme.CardBorder,
                    Opacity = 0.55,
                    Margin = new Thickness(2.5, 4, 2.5, 4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = null,
                };
                _bars[i] = b;
                _stripesRow.Children.Add(b);
            }
            _root.Children.Add(stripesHost);

            _date = Theme.MakeText("", 14.5, Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_date);
            _hijri = Theme.MakeText("", 12.5, Theme.TextFaint);
            _hijri.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_hijri);
        }

        public FrameworkElement Root { get { return _root; } }

        public void Tick(DateTime now)
        {
            var s = Settings.I;
            string fmt = s.Use24h ? "HH:mm" : "h:mm";
            if (s.ShowSeconds) fmt += ":ss";
            _time.Text = now.ToString(fmt, CultureInfo.InvariantCulture);
            _ampm.Update(now);

            int hr = now.Hour;
            for (int i = 0; i < 24; i++)
            {
                if (i < hr)          // spent hours: solid accent
                {
                    _bars[i].Background = Theme.Accent;
                    _bars[i].Opacity = 0.85;
                    _bars[i].Height = 26;
                }
                else if (i == hr)    // current hour: tallest, brightest
                {
                    _bars[i].Background = Theme.Accent;
                    _bars[i].Opacity = 1.0;
                    _bars[i].Height = 34;
                }
                else                 // remaining: faint
                {
                    _bars[i].Background = Theme.CardBorder;
                    _bars[i].Opacity = 0.55;
                    _bars[i].Height = 22;
                }
            }

            ClockDates.Paint(_date, _hijri, now);
        }
    }

    /// <summary>Vector outlines of the isolated ص/م AM/PM markers, extracted from GDI+
    /// (Tahoma Bold at 200px, flattened). The pill draws these instead of shaped text:
    /// on machines where DirectWrite mis-shapes a lone isolated Arabic letter (the
    /// mirrored-م defect), text shaping is simply taken out of the equation.</summary>
    static class AmPmGlyph
    {
public static readonly StreamGeometry Sad = (StreamGeometry)Geometry.Parse("F1 M 232.9,159.2 L 238.2,156.5 L 242.1,153.2 L 244.4,149.8 L 245.1,146 L 244,140.5 L 240.6,135.6 L 235.2,132.2 L 228,131.1 L 218.9,132 L 210.4,134.5 L 202.5,138.4 L 195.1,143.1 L 183.1,154.1 L 178.2,159.9 L 174,165 L 192.5,165 L 205.4,164.6 L 216.5,163.5 L 225.6,161.6 L 232.9,159.2 Z M 75.5,241.6 L 61.8,240.5 L 49.4,237.2 L 38.5,231.9 L 29.1,224.2 L 21.6,214.8 L 16.2,203.9 L 13.1,191.4 L 12,177.4 L 12.4,163.9 L 13.6,152 L 14.5,147.1 L 15,143.6 L 15.4,141.1 L 15.6,140 L 43.4,140 L 43.4,141.1 L 42,147.8 L 40.6,156.9 L 39.1,176.9 L 39.8,183 L 41.6,188.6 L 44.9,193.6 L 49.4,198 L 54.9,201.5 L 61.1,204.1 L 68,205.6 L 75.5,206.1 L 83.1,205.5 L 90.4,203.8 L 97,200.9 L 102.4,197 L 107.5,191.6 L 111.1,185.2 L 113.4,177.9 L 114.1,169.8 L 114.1,163.4 L 114,157.8 L 113.8,153 L 113.5,149.1 L 113,143.4 L 112.5,140 L 140.4,140 L 141,143.8 L 142.2,156.1 L 143,161.2 L 144.8,163.8 L 151.5,154.4 L 158,145.8 L 164.2,138 L 170.1,131.2 L 175.8,125.2 L 181,120 L 186,115.8 L 190.6,112.2 L 199.8,107 L 209,103.2 L 218.4,100.9 L 227.8,100.1 L 236.9,101 L 245.2,103.4 L 252.8,107.5 L 259.5,113.4 L 265,120.4 L 269,128.5 L 271.4,137.5 L 272.1,147.5 L 270.9,158.1 L 266.9,168 L 260.6,177 L 252.2,184.8 L 241.9,191.1 L 229.8,196 L 216.2,199.1 L 201.1,200.1 L 139.1,200.1 L 135.1,209.4 L 129.9,217.6 L 123.4,224.6 L 115.6,230.6 L 106.9,235.4 L 97.4,238.9 L 86.9,240.9 L 75.5,241.6 Z ");
        public static readonly StreamGeometry Meem = (StreamGeometry)Geometry.Parse("F1 M 108.6,120.9 L 114.8,127.9 L 119.4,136.2 L 122.4,145.9 L 123.4,157 L 122.4,169.1 L 119.2,178.9 L 114.5,186.8 L 108.9,192.6 L 102.5,196.9 L 95.8,199.6 L 89.1,201.2 L 82.8,201.8 L 74.6,201 L 66.5,198.8 L 58.9,195 L 51.9,190 L 47.9,200.8 L 43,210.1 L 37.2,218.2 L 30.8,225 L 23.5,230.6 L 16,235.2 L 8.1,239 L 0,241.6 L 0,206.6 L 6.5,204.2 L 12.1,201.4 L 17.4,197.5 L 22.4,192 L 26.9,184.9 L 30.6,175.9 L 32.2,170.5 L 33.6,164.4 L 34.8,157.4 L 35.8,149.6 L 37.4,140.1 L 39.9,131.8 L 43.2,124.6 L 47.6,118.6 L 53,113.9 L 59.4,110.5 L 66.9,108.4 L 75.4,107.8 L 84.6,108.6 L 93.4,111.1 L 101.4,115.2 L 108.6,120.9 Z M 96,154.5 L 94.5,148 L 90.1,142.5 L 83.9,138.6 L 76.8,137.4 L 71.2,138.4 L 66.9,141.5 L 64,146.1 L 63,151.5 L 64.4,157.2 L 68.4,162.1 L 74.4,165.6 L 81.6,166.8 L 87.4,165.9 L 92,163.5 L 95,159.6 L 96,154.5 Z ");
        static AmPmGlyph() { Sad.Freeze(); Meem.Freeze(); }
    }

    /// <summary>Shared celestial bodies for every sky in the app (Sun-path, Neon sky).
    /// The sun is a genuinely burning object: a wide corona wash, twelve tapered rays that
    /// rotate so slowly you only notice on a second glance, a layered amber body, and a
    /// white-hot core — no flat accent dot anywhere.</summary>
    static class SkyBodies
    {
        /// <summary>Every sun's ray layer currently on screen.
        ///
        /// The rays used to turn on a <c>RepeatBehavior.Forever</c> animation. A never-ending
        /// WPF animation keeps the composition loop running at the monitor's refresh rate for
        /// the life of the process, and because the widget is a layered window that loop is
        /// driven by the CPU — roughly sixty full repaints a second, for ever, whether the
        /// widget was on screen or buried under a maximised browser. It was by a wide margin
        /// the most expensive thing DeskMate did.
        ///
        /// The rotation is now one degree per painted tick, driven by the widget's own timer.
        /// It costs nothing beyond a repaint that was happening anyway, keeps the same
        /// six-minutes-per-revolution drift, and stops dead the moment the widget is hidden,
        /// covered or power-saving.</summary>
        /// <summary>The ray layers themselves rather than their transforms, so both answers
        /// below can be gated on <c>IsVisible</c>: a sun that is below the horizon is Hidden,
        /// and turning a sun nobody can see is exactly the kind of work this whole change is
        /// about removing.</summary>
        static readonly System.Collections.Generic.List<Canvas> _rayLayers =
            new System.Collections.Generic.List<Canvas>();

        /// <summary>Called from BuildUI: the previous visual tree is gone and so are its
        /// transforms, so the list must not keep them alive.</summary>
        public static void Reset() { _rayLayers.Clear(); }

        /// <summary>True when a sun is actually on screen right now. The widget asks before
        /// deciding whether a per-second repaint cadence still buys anything — at night, on a
        /// sun-path face, it does not.</summary>
        public static bool HasSpin
        {
            get
            {
                for (int i = 0; i < _rayLayers.Count; i++)
                    if (_rayLayers[i].IsVisible) return true;
                return false;
            }
        }

        /// <summary>Advances every visible sun by one degree. Called once per painted tick.</summary>
        public static void Advance()
        {
            for (int i = 0; i < _rayLayers.Count; i++)
            {
                var layer = _rayLayers[i];
                if (!layer.IsVisible) continue;
                var rt = layer.RenderTransform as RotateTransform;
                if (rt != null) rt.Angle = (rt.Angle + 1.0) % 360.0;
            }
        }

        /// <summary>Builds the sun. The returned grid starts Hidden; callers toggle its
        /// Visibility and place it with Canvas.SetLeft/Top using its Width/Height.</summary>
        public static Grid BuildSun(double radius, bool hidden)
        {
            double box = radius * 5.2;
            double c = box / 2;
            var g = new Grid { Width = box, Height = box, Visibility = hidden ? Visibility.Hidden : Visibility.Visible };

            // corona wash
            var corona = new Ellipse { Width = box, Height = box, IsHitTestVisible = false };
            var cg = new RadialGradientBrush { GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5) };
            cg.GradientStops.Add(new GradientStop(Color.FromArgb(0x59, 0xFF, 0xD2, 0x7A), 0));
            cg.GradientStops.Add(new GradientStop(Color.FromArgb(0x24, 0xFF, 0xB8, 0x5C), 0.5));
            cg.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            corona.Fill = cg;
            g.Children.Add(corona);

            // rays: 12 tapered blades fading outward, on a layer that rotates once per 90 s —
            // a physical sun's shimmer without a strobe
            var rays = new Canvas { Width = box, Height = box, IsHitTestVisible = false };
            for (int i = 0; i < 12; i++)
            {
                double rw = Math.Max(1.2, radius * 0.26);
                double rh = radius * 1.15;
                var rayFill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                rayFill.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
                rayFill.GradientStops.Add(new GradientStop(Color.FromArgb(0x8C, 0xFF, 0xC8, 0x66), 1));
                var ray = new System.Windows.Controls.Border
                {
                    Width = rw, Height = rh,
                    CornerRadius = new CornerRadius(rw / 2),
                    Background = rayFill,
                    IsHitTestVisible = false,
                };
                double x = c - rw / 2;
                double y = c - radius * 1.72 - rh;   // blade standing above the limb
                Canvas.SetLeft(ray, x);
                Canvas.SetTop(ray, y);
                ray.RenderTransform = new RotateTransform(i * 30, c - x, c - y);
                rays.Children.Add(ray);
            }
            rays.RenderTransform = new RotateTransform(0, c, c);
            _rayLayers.Add(rays);   // advanced one degree per painted tick — see Advance()
            g.Children.Add(rays);

            // body: white-hot heart → amber limb
            var body = new Ellipse { Width = radius * 2, Height = radius * 2 };
            var bg = new RadialGradientBrush { GradientOrigin = new Point(0.35, 0.3), Center = new Point(0.45, 0.42) };
            bg.GradientStops.Add(new GradientStop(Colors.White, 0));
            bg.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xE9, 0xB0), 0.35));
            bg.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xC2, 0x66), 0.78));
            bg.GradientStops.Add(new GradientStop(Color.FromRgb(0xF5, 0xA2, 0x4A), 1));
            body.Fill = bg;
            var glow = new DropShadowEffect { Color = Color.FromRgb(0xFF, 0xC0, 0x60), BlurRadius = 16, ShadowDepth = 0, Opacity = 0.9 };
            glow.Freeze();
            body.Effect = glow;
            g.Children.Add(body);

            // hotter core: a small white bloom at the heart of the disc
            double cs = radius * 1.1;
            var core = new Ellipse { Width = cs, Height = cs, IsHitTestVisible = false };
            var kc = new RadialGradientBrush { GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5) };
            kc.GradientStops.Add(new GradientStop(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF), 0));
            kc.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1));
            core.Fill = kc;
            core.HorizontalAlignment = HorizontalAlignment.Center;
            core.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(core);
            return g;
        }
    }
}
