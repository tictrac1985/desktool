using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DeskMate.Core;

namespace DeskMate.UI
{
    /// <summary>The widget's motion language. Three levels, set in Settings → Motion:
    /// 0 none · 1 calm (a choreographed entrance) · 2 cinematic (entrance, a pointer light that
    /// follows the mouse across each card, and a light glint that sweeps the clock card when the
    /// minute turns). The power-saving mode forces level 0: the window is a CPU-rendered
    /// layered surface, and motion is the first thing to give up when battery matters.</summary>
    static class Cinema
    {
        public static int Level { get { return Settings.SaverActive ? 0 : Settings.I.Motion; } }

        static bool _entered;
        static int _lastMove;
        static int _lastMinute = -1;

        // ---------------- entrance ----------------

        /// <summary>Cards rise into place one after another — once per launch. Later rebuilds
        /// (changing a setting) must never replay it: that would make every tweak a show.</summary>
        public static void Entrance(IList<Border> cards)
        {
            if (_entered) return;
            _entered = true;
            if (Level < 1 || cards == null) return;
            double ms = Level >= 2 ? 620 : 380;
            double rise = Level >= 2 ? 22 : 10;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var move = new TranslateTransform(0, rise);
                card.RenderTransform = move;
                card.Opacity = 0;
                var begin = TimeSpan.FromMilliseconds(80 + i * (Level >= 2 ? 120 : 70));
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms)) { BeginTime = begin, EasingFunction = ease };
                var slide = new DoubleAnimation(rise, 0, TimeSpan.FromMilliseconds(ms)) { BeginTime = begin, EasingFunction = ease };
                fade.Completed += (s, e) => { card.BeginAnimation(UIElement.OpacityProperty, null); card.Opacity = 1; };
                slide.Completed += (s, e) => { move.BeginAnimation(TranslateTransform.YProperty, null); card.RenderTransform = null; };
                card.BeginAnimation(UIElement.OpacityProperty, fade);
                move.BeginAnimation(TranslateTransform.YProperty, slide);
            }
        }

        // ---------------- pointer light ----------------

        /// <summary>A soft accent glow under the pointer, per card. Wired once per visual tree.</summary>
        public static void AttachPointerLight(FrameworkElement host, IList<Border> cards)
        {
            if (Level < 2 || cards == null || cards.Count == 0) return;
            var brushes = new Dictionary<Border, RadialGradientBrush>();
            host.MouseMove += (s, e) =>
            {
                if (Level < 2) return;
                int now = Environment.TickCount;
                if (now - _lastMove < 33) return;       // ~30 fps is plenty for a glow
                _lastMove = now;
                foreach (var card in cards)
                {
                    var layer = Theme.LightLayer(card);
                    if (layer == null) continue;
                    var p = e.GetPosition(layer);
                    double w = layer.ActualWidth, h = layer.ActualHeight;
                    if (w < 4 || h < 4 || p.X < -20 || p.Y < -20 || p.X > w + 20 || p.Y > h + 20)
                    {
                        if (layer.Background != null) layer.Background = null;
                        continue;
                    }
                    RadialGradientBrush b;
                    if (!brushes.TryGetValue(card, out b))
                    {
                        var c = Theme.AccentColor;
                        b = new RadialGradientBrush { RadiusX = 0.55, RadiusY = 0.8 };
                        b.GradientStops.Add(new GradientStop(Color.FromArgb(Theme.IsLight ? (byte)0x30 : (byte)0x2E, c.R, c.G, c.B), 0));
                        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
                        brushes[card] = b;
                    }
                    var origin = new Point(p.X / w, p.Y / h);
                    b.Center = origin;
                    b.GradientOrigin = origin;
                    if (!ReferenceEquals(layer.Background, b)) layer.Background = b;
                }
            };
            host.MouseLeave += (s, e) =>
            {
                foreach (var card in cards) { var layer = Theme.LightLayer(card); if (layer != null) layer.Background = null; }
            };
        }

        // ---------------- minute glint ----------------

        /// <summary>Call every tick; sweeps a band of light across the first (clock) card each
        /// time the minute changes — the face "catches the light" as time moves on.</summary>
        public static void OnMinute(IList<Border> cards, DateTime now)
        {
            if (now.Minute == _lastMinute) return;
            bool first = _lastMinute < 0;
            _lastMinute = now.Minute;
            if (first || Level < 2 || cards == null || cards.Count == 0) return;
            var glint = Theme.GlintLayer(cards[0]);
            if (glint == null || glint.ActualWidth < 8) return;

            var c = Theme.IsLight ? Theme.AccentColor : Colors.White;
            byte a = Theme.IsLight ? (byte)0x34 : (byte)0x2C;
            var move = new TranslateTransform(-1.3, 0);
            var band = new LinearGradientBrush { StartPoint = new Point(0, 0.2), EndPoint = new Point(1, 0.8), RelativeTransform = move };
            band.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0.30));
            band.GradientStops.Add(new GradientStop(Color.FromArgb(a, c.R, c.G, c.B), 0.50));
            band.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0.70));
            glint.Background = band;
            glint.Opacity = 1;
            var sweep = new DoubleAnimation(-1.3, 1.3, TimeSpan.FromMilliseconds(1300)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            sweep.Completed += (s, e) => { glint.Opacity = 0; glint.Background = null; };
            move.BeginAnimation(TranslateTransform.XProperty, sweep);
        }
    }
}
