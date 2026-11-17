using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>Speaker volume and mute, microphone mute, and the transport keys (previous,
    /// play/pause, next) for whatever player is active. Volume talks to Windows Core Audio
    /// directly; the transport buttons press the keyboard's own media keys, so Spotify, a
    /// browser tab or any other player reacts exactly as it would to a real keyboard.</summary>
    public static class MediaFeature
    {
        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "media", L.T("f_media"), Build, 320);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();

            double level; bool muted;
            bool haveSpeakers = Audio.TryGet(false, out level, out muted);

            // ---- transport ----
            var transport = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0, 2, 0, 10) };
            transport.Children.Add(Theme.MakeIconButton("\uE892", L.T("md_prev"), () => Win32.TapMediaKey(Win32.VK_MEDIA_PREV), 42, 16));
            transport.Children.Add(Theme.MakeIconButton("\uE768", L.T("md_play"), () => Win32.TapMediaKey(Win32.VK_MEDIA_PLAY_PAUSE), 52, 20));
            transport.Children.Add(Theme.MakeIconButton("\uE893", L.T("md_next"), () => Win32.TapMediaKey(Win32.VK_MEDIA_NEXT), 42, 16));
            stack.Children.Add(transport);

            // ---- speakers ----
            stack.Children.Add(Theme.SectionTitle(L.T("md_speakers")));
            if (!haveSpeakers)
            {
                var none = Theme.MakeText(L.T("md_none"), 12, Theme.TextFaint);
                none.TextWrapping = TextWrapping.Wrap;
                stack.Children.Add(none);
            }
            else
            {
                bool loading = true;
                var pct = Theme.MakeText("", 13, Theme.Text);
                pct.FlowDirection = FlowDirection.LeftToRight;
                pct.MinWidth = 42;
                pct.TextAlignment = TextAlignment.Center;
                pct.VerticalAlignment = VerticalAlignment.Center;

                var muteBtn = Theme.MakeIconButton("\uE767", L.T("md_mute"), () => { }, 36, 15);
                Action paintMute = null;
                Slider slider = null;
                bool isMuted = muted;
                paintMute = () =>
                {
                    var tb = (muteBtn.Child as TextBlock);
                    if (tb != null) tb.Text = isMuted || slider.Value < 1 ? "\uE74F" : slider.Value < 34 ? "\uE992" : slider.Value < 67 ? "\uE993" : "\uE767";
                    pct.Opacity = isMuted ? 0.45 : 1;
                };
                slider = Theme.MakeSlider(0, 100, Math.Round(level * 100), v =>
                {
                    pct.Text = ((int)Math.Round(v)).ToString() + "%";
                    if (loading) return;
                    Audio.SetLevel(v / 100.0);
                    if (v > 0.5) isMuted = false;
                    paintMute();
                });
                slider.MinWidth = 150;
                slider.VerticalAlignment = VerticalAlignment.Center;
                muteBtn.MouseLeftButtonUp += (s, e) =>
                {
                    isMuted = !isMuted;
                    Audio.SetMute(false, isMuted);
                    paintMute();
                };
                loading = false;
                pct.Text = ((int)Math.Round(level * 100)).ToString() + "%";
                paintMute();

                var row = new DockPanel { LastChildFill = true, FlowDirection = FlowDirection.LeftToRight };
                DockPanel.SetDock(muteBtn, Dock.Left);
                DockPanel.SetDock(pct, Dock.Right);
                row.Children.Add(muteBtn);
                row.Children.Add(pct);
                row.Children.Add(slider);
                stack.Children.Add(row);

                // follow changes made elsewhere (keyboard keys, the tray mixer) while open
                var poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                poll.Tick += (s, e) =>
                {
                    double lv; bool mu;
                    if (!Audio.TryGet(false, out lv, out mu)) return;
                    if (slider.IsMouseCaptureWithin) return;
                    loading = true;
                    slider.Value = Math.Round(lv * 100);
                    loading = false;
                    pct.Text = ((int)Math.Round(lv * 100)).ToString() + "%";
                    isMuted = mu;
                    paintMute();
                };
                poll.Start();
                host.Closed += (s, e) => poll.Stop();
            }

            // ---- microphone ----
            double mlevel; bool mmuted;
            if (Audio.TryGet(true, out mlevel, out mmuted))
            {
                stack.Children.Add(Theme.SectionTitle(L.T("md_mic")));
                var cb = Theme.MakeCheck(L.T("md_mic_mute"), mmuted, v => Audio.SetMute(true, v));
                stack.Children.Add(cb);
            }
            return stack;
        }
    }
}
