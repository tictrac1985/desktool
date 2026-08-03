using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DeskMate.Core;

namespace DeskMate.UI
{
    public interface IClockView
    {
        FrameworkElement Root { get; }
        void Tick(DateTime now);
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
            _date.Text = DateText.Gregorian(now);
            _hijri.Visibility = s.ShowHijri ? Visibility.Visible : Visibility.Collapsed;
            if (s.ShowHijri) _hijri.Text = DateText.Hijri(now);
        }
    }

    /// <summary>Analog dial with hour/minute(/second) hands.</summary>
    public class AnalogClock : IClockView
    {
        readonly StackPanel _root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        readonly Canvas _canvas;
        readonly Line _hour, _min, _sec;
        readonly TextBlock _date, _hijri;
        const double Size = 168, C = Size / 2;

        public AnalogClock()
        {
            _canvas = new Canvas { Width = Size, Height = Size, FlowDirection = FlowDirection.LeftToRight };
            var dial = new Ellipse
            {
                Width = Size, Height = Size,
                Stroke = Theme.DialStroke,
                StrokeThickness = 2.5,
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
            var domeEl = new Ellipse { Width = Size - 5, Height = Size - 5, Fill = dome, IsHitTestVisible = false };
            Canvas.SetLeft(domeEl, 2.5);
            Canvas.SetTop(domeEl, 2.5);
            _canvas.Children.Add(domeEl);
            for (int i = 0; i < 12; i++)
            {
                double a = i * 30 * Math.PI / 180;
                bool quarter = i % 3 == 0;
                double r1 = C - (quarter ? 14 : 10), r2 = C - 5;
                var tick = new Line
                {
                    X1 = C + Math.Sin(a) * r1, Y1 = C - Math.Cos(a) * r1,
                    X2 = C + Math.Sin(a) * r2, Y2 = C - Math.Cos(a) * r2,
                    Stroke = quarter ? Theme.Accent : Theme.TextFaint,
                    StrokeThickness = quarter ? 2.6 : 1.4,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                };
                _canvas.Children.Add(tick);
            }
            _hour = MakeHand(4.6, C * 0.48, Theme.Text);
            _min = MakeHand(3.2, C * 0.70, Theme.Text);
            _sec = MakeHand(1.4, C * 0.78, Theme.Accent);
            var dot = new Ellipse { Width = 9, Height = 9, Fill = Theme.Accent };
            Canvas.SetLeft(dot, C - 4.5); Canvas.SetTop(dot, C - 4.5);
            _canvas.Children.Add(dot);
            _root.Children.Add(_canvas);

            _date = Theme.MakeText("", 14, Theme.TextDim);
            _date.HorizontalAlignment = HorizontalAlignment.Center;
            _date.Margin = new Thickness(0, 8, 0, 0);
            _root.Children.Add(_date);
            _hijri = Theme.MakeText("", 12, Theme.TextFaint);
            _hijri.HorizontalAlignment = HorizontalAlignment.Center;
            _root.Children.Add(_hijri);
        }

        Line MakeHand(double thickness, double length, Brush brush)
        {
            var l = new Line
            {
                X1 = C, Y1 = C, X2 = C, Y2 = C - length,
                Stroke = brush, StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            };
            _canvas.Children.Add(l);
            return l;
        }

        static void SetAngle(Line hand, double deg, double length)
        {
            double a = deg * Math.PI / 180;
            hand.X2 = C + Math.Sin(a) * length;
            hand.Y2 = C - Math.Cos(a) * length;
        }

        public FrameworkElement Root { get { return _root; } }

        public void Tick(DateTime now)
        {
            var s = Settings.I;
            SetAngle(_hour, (now.Hour % 12 + now.Minute / 60.0) * 30, C * 0.48);
            // sweep the minute hand only when the second hand is shown; otherwise step per minute
            SetAngle(_min, (now.Minute + (s.ShowSeconds ? now.Second / 60.0 : 0)) * 6, C * 0.70);
            _sec.Visibility = s.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;
            if (s.ShowSeconds) SetAngle(_sec, now.Second * 6, C * 0.78);
            _date.Text = DateText.Gregorian(now);
            _hijri.Visibility = s.ShowHijri ? Visibility.Visible : Visibility.Collapsed;
            if (s.ShowHijri) _hijri.Text = DateText.Hijri(now);
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
            string d = DateText.Gregorian(now);
            if (s.ShowHijri)
            {
                string h = DateText.Hijri(now);
                if (h.Length > 0) d += "  •  " + h;
            }
            _date.Text = d;
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
}
