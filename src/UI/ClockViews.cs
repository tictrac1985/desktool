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
            var accentPill = new SolidColorBrush(Color.FromArgb(0x2A, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            accentPill.Freeze();
            _ampmHost = new Border
            {
                Background = accentPill,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 2.5, 8, 2.5),
                Child = _ampm,
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
            _ampm.Text = L.IsAr ? (now.Hour < 12 ? "ص" : "م") : (now.Hour < 12 ? "AM" : "PM");
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
        readonly Border _ampmHost;

        public TimeOnlyClock()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _time = Theme.MakeText("", 52, Theme.Text, Theme.LightFont);
            _time.FlowDirection = FlowDirection.LeftToRight;
            _ampm = Theme.MakeText("", 12.5, Theme.Accent);
            _ampm.FontWeight = FontWeights.SemiBold;
            var pill = new SolidColorBrush(Color.FromArgb(0x2A, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            pill.Freeze();
            _ampmHost = new Border
            {
                Background = pill,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 2, 7, 2),
                Child = _ampm,
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
            _ampm.Text = L.IsAr ? (now.Hour < 12 ? "ص" : "م") : (now.Hour < 12 ? "AM" : "PM");
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
            if (!s.Use24h) t += L.IsAr ? (now.Hour < 12 ? " ص" : " م") : (now.Hour < 12 ? " AM" : " PM");
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
}
