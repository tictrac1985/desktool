using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskMate.Core;

namespace DeskMate.UI
{
    /// <summary>MainWindow corner grip: drag to scale the whole widget.</summary>
    public partial class MainWindow
    {
        Point _gripStart;
        double _gripStartScale;
        bool _scaling;

        Border BuildScaleGrip()
        {
            var lines = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0,9 L 9,0 M 4.5,9 L 9,4.5"),
                Stroke = Theme.TextFaint,
                StrokeThickness = 1.6,
                Width = 10,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var grip = new Border
            {
                Width = 22,
                Height = 22,
                Background = Brushes.Transparent,
                Child = lines,
                Cursor = Cursors.SizeNWSE,
                VerticalAlignment = VerticalAlignment.Bottom,
                // keep the grip at the VISUAL bottom-right in both flow directions
                HorizontalAlignment = L.IsAr ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Thickness(2),
            };
            grip.MouseEnter += (s, e) => lines.Stroke = Theme.Accent;
            grip.MouseLeave += (s, e) => lines.Stroke = Theme.TextFaint;
            grip.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                _scaling = true;
                _gripStartScale = Settings.I.Scale;
                _gripStart = PointToScreen(e.GetPosition(this));
                grip.CaptureMouse();
            };
            grip.MouseMove += (s, e) =>
            {
                if (!_scaling) return;
                var cur = PointToScreen(e.GetPosition(this));
                double delta = (cur.X - _gripStart.X) + (cur.Y - _gripStart.Y);
                double ns = Math.Max(0.6, Math.Min(1.8, _gripStartScale + delta / 280.0));
                Settings.I.Scale = ns;
                if (_rootEl != null) _rootEl.LayoutTransform = new ScaleTransform(ns, ns);
            };
            grip.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (!_scaling) return;
                _scaling = false;
                grip.ReleaseMouseCapture();
                Settings.I.Save();
            };
            grip.LostMouseCapture += (s, e) =>
            {
                if (_scaling) { _scaling = false; Settings.I.Save(); }
            };
            return grip;
        }
    }
}
