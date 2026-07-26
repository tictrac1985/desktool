using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DeskMate.Core;

namespace DeskMate.UI
{
    /// <summary>MainWindow layout builders — the six widget arrangements.</summary>
    public partial class MainWindow
    {
        public void BuildUI()
        {
            FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            _cards.Clear();
            _floatIcons.Clear();
            _clockCard = null;
            _connectors = null;
            _gregText = null;
            _hijriText = null;
            _dateKey = null;

            _prayerLine = Theme.MakeText("", 12.5, Theme.Accent);
            _prayerLine.HorizontalAlignment = HorizontalAlignment.Center;
            _prayerLine.Margin = new Thickness(0, 5, 0, 0);
            _prayerLine.Visibility = Visibility.Collapsed;

            switch (Settings.I.LayoutIndex)
            {
                case 1: _rootEl = BuildConnected(); break;
                case 2: _rootEl = BuildTriple(); break;
                case 3: _rootEl = BuildOrbit(); break;
                case 4: _rootEl = BuildSidebar(); break;
                case 5: _rootEl = BuildDock(); break;
                default: _rootEl = BuildClassic(); break;
            }
            _rootEl.LayoutTransform = new ScaleTransform(Settings.I.Scale, Settings.I.Scale);

            var host = new Grid();
            host.Children.Add(_rootEl);
            host.Children.Add(BuildScaleGrip());
            host.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is FrameworkElement) { try { DragMove(); SavePosition(); } catch { } }
            };
            Content = host;

            ApplyCardOpacity();
            if (_clock != null) _clock.Tick(DateTime.Now);
            ScheduleConnectorDraw();
        }

        IClockView MakeClock(bool timeOnly)
        {
            if (timeOnly) return new TimeOnlyClock();
            switch (Settings.I.ClockStyle)
            {
                case 1: return new AnalogClock();
                case 2: return new MinimalClock();
                default: return new DigitalClock();
            }
        }

        Border MakeCardTracked(UIElement content, double radius = 24)
        {
            var c = Theme.MakeCard(content, radius);
            _cards.Add(c);
            return c;
        }

        Border Hairline(double top, double bottom = 0)
        {
            return new Border
            {
                Height = 1,
                Background = Theme.CardBorder,
                Margin = new Thickness(4, top, 4, bottom),
            };
        }

        /// <summary>Layout 0 — everything in one card (the original look).</summary>
        FrameworkElement BuildClassic()
        {
            _clock = MakeClock(false);
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(_clock.Root);
            stack.Children.Add(_prayerLine);
            stack.Children.Add(Hairline(12));
            var iconBar = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
                MaxWidth = 500,
            };
            foreach (var ic in BuildIcons(false)) iconBar.Children.Add(ic);
            stack.Children.Add(iconBar);
            return MakeCardTracked(stack);
        }

        /// <summary>Layout 1 — clock card with rays connecting to free-floating icons.</summary>
        FrameworkElement BuildConnected()
        {
            _clock = MakeClock(false);
            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            inner.Children.Add(_clock.Root);
            inner.Children.Add(_prayerLine);
            _clockCard = MakeCardTracked(inner);
            _clockCard.Margin = new Thickness(12, 12, 12, 2);

            _connectors = new Canvas { Height = 28, IsHitTestVisible = false, ClipToBounds = false };

            var iconsRow = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(14, 0, 14, 12),
                MaxWidth = 560,
            };
            foreach (var ic in BuildIcons(true)) { iconsRow.Children.Add(ic); _floatIcons.Add(ic); }

            var root = new StackPanel();
            root.Children.Add(_clockCard);
            root.Children.Add(_connectors);
            root.Children.Add(iconsRow);
            return root;
        }

        /// <summary>Layout 2 — big time in the middle, Hijri card on one side and
        /// Gregorian on the other, icon pill underneath.</summary>
        FrameworkElement BuildTriple()
        {
            _clock = MakeClock(true);
            var centerInner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            centerInner.Children.Add(_clock.Root);
            centerInner.Children.Add(_prayerLine);
            var centerCard = MakeCardTracked(centerInner, 26);
            centerCard.Margin = new Thickness(4, 12, 4, 6);

            var hijriCard = MakeDateCard(Glyphs.Prayer, out _hijriText);   // crescent
            var gregCard = MakeDateCard(Glyphs.Calendar, out _gregText);   // calendar
            if (!Settings.I.ShowHijri) hijriCard.Visibility = Visibility.Collapsed;

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            // added first = starts at the visual right in RTL → Hijri right, Gregorian left
            row.Children.Add(hijriCard);
            row.Children.Add(centerCard);
            row.Children.Add(gregCard);

            var pillContent = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 520,
            };
            foreach (var ic in BuildIcons(false)) pillContent.Children.Add(ic);
            var pill = MakeCardTracked(pillContent, 30);
            pill.Padding = new Thickness(12, 6, 12, 8);
            pill.Margin = new Thickness(12, 0, 12, 12);
            pill.HorizontalAlignment = HorizontalAlignment.Center;

            var root = new StackPanel();
            root.Children.Add(row);
            root.Children.Add(pill);
            return root;
        }

        Border MakeDateCard(string glyph, out TextBlock text)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var chip = Theme.MakeGlyphChip(glyph, 30);
            chip.Margin = new Thickness(0, 0, 0, 6);
            stack.Children.Add(chip);
            text = Theme.MakeText("", 12, Theme.TextDim);
            text.TextWrapping = TextWrapping.Wrap;
            text.TextAlignment = TextAlignment.Center;
            text.MaxWidth = 96;
            stack.Children.Add(text);
            var card = MakeCardTracked(stack, 18);
            card.Padding = new Thickness(12, 10, 12, 10);
            card.Margin = new Thickness(4, 12, 4, 6);
            card.VerticalAlignment = VerticalAlignment.Center;
            return card;
        }

        /// <summary>Layout 3 — circular clock card with icons orbiting around it.</summary>
        FrameworkElement BuildOrbit()
        {
            _clock = MakeClock(false);
            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            inner.Children.Add(_clock.Root);
            inner.Children.Add(_prayerLine);
            var card = MakeCardTracked(inner);
            card.Margin = new Thickness(0);
            card.Padding = new Thickness(26);

            // measure, then size the circle so the content rectangle is fully inscribed
            // (diameter = diagonal of the measured content)
            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double mw = card.DesiredSize.Width, mh = card.DesiredSize.Height;
            double size = Math.Sqrt(mw * mw + mh * mh);
            card.Width = size;
            card.Height = size;
            card.CornerRadius = new CornerRadius(size / 2);
            card.FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            var icons = BuildIcons(true);
            double ringR = size / 2 + 48;
            double pad = 56; // room for icon cells + their permanent labels outside the ring
            double canvasSize = 2 * (ringR + pad);
            var canvas = new Canvas
            {
                Width = canvasSize,
                Height = canvasSize,
                Background = Brushes.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Thickness(4),
            };
            double c = canvasSize / 2;

            var ringStroke = new SolidColorBrush(Color.FromArgb(0x34,
                Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            ringStroke.Freeze();
            var ring = new Ellipse
            {
                Width = 2 * ringR,
                Height = 2 * ringR,
                Stroke = ringStroke,
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection { 3, 4 },
            };
            Canvas.SetLeft(ring, c - ringR);
            Canvas.SetTop(ring, c - ringR);
            canvas.Children.Add(ring);

            Canvas.SetLeft(card, c - size / 2);
            Canvas.SetTop(card, c - size / 2);
            canvas.Children.Add(card);

            for (int i = 0; i < icons.Count; i++)
            {
                double a = (-90 + i * 360.0 / icons.Count) * Math.PI / 180.0;
                double ix = c + ringR * Math.Cos(a);
                double iy = c + ringR * Math.Sin(a);
                // tool cell is 58 wide; its 40px circle center sits at (29, 20)
                Canvas.SetLeft(icons[i], ix - 29);
                Canvas.SetTop(icons[i], iy - 20);
                canvas.Children.Add(icons[i]);
            }
            return canvas;
        }

        /// <summary>Layout 4 — vertical sidebar: compact clock on top, menu-style rows
        /// with always-visible names beneath.</summary>
        FrameworkElement BuildSidebar()
        {
            _clock = new MinimalClock();
            var stack = new StackPanel { MinWidth = 176 };
            stack.Children.Add(_clock.Root);
            stack.Children.Add(_prayerLine);
            stack.Children.Add(Hairline(10, 6));
            foreach (var def in IconDefs())
                stack.Children.Add(MakeSidebarRow(def.Item1, def.Item2, def.Item3));
            return MakeCardTracked(stack, 22);
        }

        FrameworkElement MakeSidebarRow(string glyph, string titleKey, Action onClick)
        {
            var row = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 4, 7, 4),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = System.Windows.Input.Cursors.Hand,
                RenderTransform = new TranslateTransform(0, 0),
            };
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            var circle = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = Theme.ButtonBg,
                BorderBrush = Theme.ButtonBorder,
                BorderThickness = new Thickness(1),
                Child = Theme.MakeGlyph(glyph, 13),
            };
            content.Children.Add(circle);
            var name = Theme.MakeText(L.T(titleKey), 12.5, Theme.TextDim);
            name.VerticalAlignment = VerticalAlignment.Center;
            name.Margin = new Thickness(10, 0, 4, 0);
            content.Children.Add(name);
            row.Child = content;

            var slide = (TranslateTransform)row.RenderTransform;
            row.MouseEnter += (s, e) =>
            {
                row.Background = Theme.HoverBg;
                circle.BorderBrush = Theme.Accent;
                name.Foreground = Theme.Text;
                slide.BeginAnimation(TranslateTransform.XProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(L.IsAr ? -4 : 4, TimeSpan.FromMilliseconds(130)));
            };
            row.MouseLeave += (s, e) =>
            {
                row.Background = Brushes.Transparent;
                circle.BorderBrush = Theme.ButtonBorder;
                name.Foreground = Theme.TextDim;
                slide.BeginAnimation(TranslateTransform.XProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(130)));
            };
            row.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            row.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                try { onClick(); } catch (Exception ex) { Log.Error("sidebar", ex); }
            };
            return row;
        }

        /// <summary>Layout 5 — slim horizontal dock pill: time + date on one side,
        /// labeled tool cells inline.</summary>
        FrameworkElement BuildDock()
        {
            _clock = new MinimalClock();
            var clockCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            clockCol.Children.Add(_clock.Root);
            clockCol.Children.Add(_prayerLine);
            clockCol.Margin = new Thickness(6, 0, 6, 0);

            var rowStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            rowStack.Children.Add(clockCol);
            rowStack.Children.Add(new Border
            {
                Width = 1,
                Height = 46,
                Background = Theme.CardBorder,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            foreach (var ic in BuildIcons(false))
            {
                ((FrameworkElement)ic).VerticalAlignment = VerticalAlignment.Center;
                rowStack.Children.Add(ic);
            }

            var pill = MakeCardTracked(rowStack, 40);
            pill.Padding = new Thickness(18, 10, 18, 10);
            return pill;
        }
    }
}
