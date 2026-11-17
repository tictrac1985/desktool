using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>Self-update from the project's GitHub repository.
    ///
    /// The channel is a branch ("updates") that the build workflow rewrites after every
    /// successful build of main: latest.json plus the two executables. The app reads the
    /// manifest, and — only when the user presses the button — downloads the new exe next
    /// to itself, verifies it (exact size, SHA-256, real executable, and a version that is
    /// genuinely newer), swaps the files and restarts.
    ///
    /// Windows lets a running exe be renamed but not overwritten, so the swap is two renames:
    /// DeskMate.exe → DeskMate.exe.old, DeskMate.exe.new → DeskMate.exe. If the second fails
    /// the first is undone, so a failed update always leaves a working install behind.
    /// Nothing is ever installed silently: checking is automatic, replacing is a click.</summary>
    public static class UpdateFeature
    {
        public enum Phase { Idle, Checking, UpToDate, Available, Downloading, Installing, Failed }

        public static Phase State { get; private set; }
        public static UpdateManifest Latest { get; private set; }
        /// <summary>Localization key describing the last failure ("" when none).</summary>
        public static string ErrorKey { get; private set; }
        /// <summary>Download progress 0..1 while <see cref="State"/> is Downloading.</summary>
        public static double Progress { get; private set; }

        /// <summary>Raised on the UI thread whenever any of the above changes.</summary>
        public static event Action Changed;

        static int _busy;
        static string _announced = "";            // version already toasted this session
        static DateTime _startedUtc = DateTime.UtcNow;
        static bool _startupChecked;

        /// <summary>How often the background check runs while the app stays open.</summary>
        const double CheckEveryHours = 4;

        static UpdateFeature() { ErrorKey = ""; }

        /// <summary>True while a newer build than this one is known to be on the channel.
        /// The widget shows its update tile for exactly as long as this holds.</summary>
        public static bool IsAvailable
        {
            get { return Latest != null && UpdateCore.IsNewer(Latest.Version, AppVersion.FourPart); }
        }

        static string ExePath { get { return Assembly.GetExecutingAssembly().Location; } }

        static void Post(Action a)
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(a);
        }

        static void Set(Phase p, string errorKey = "")
        {
            State = p;
            ErrorKey = errorKey ?? "";
            var h = Changed;
            if (h != null) { try { h(); } catch (Exception ex) { Log.Error("update changed", ex); } }
        }

        // ---------------- startup ----------------

        /// <summary>Called once from Program.Main after the window is up. Sweeps the files a
        /// previous update left behind and, when this very launch IS the restart after an
        /// update, says so.</summary>
        public static void OnStartup(string[] args)
        {
            string from = null;
            if (args != null)
                for (int i = 0; i < args.Length; i++)
                    if (args[i] == "--updated") from = i + 1 < args.Length ? args[i + 1] : "";

            // the old process may still be unwinding and holding its image open — try now,
            // and once more a few seconds later
            SweepLeftovers();
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            t.Tick += (s, e) => { t.Stop(); SweepLeftovers(); };
            t.Start();

            if (from != null)
            {
                Log.Write("updated " + from + " -> " + AppVersion.FourPart);
                Post(() =>
                {
                    try { Toast.Show(string.Format(L.T("upd_done"), UpdateCore.Display(AppVersion.FourPart)), "\uE930"); }
                    catch { }
                });
            }
        }

        static void SweepLeftovers()
        {
            try
            {
                string exe = ExePath;
                string dir = Path.GetDirectoryName(exe);
                string name = Path.GetFileName(exe);
                foreach (var f in Directory.GetFiles(dir, name + ".*"))
                {
                    string lower = f.ToLowerInvariant();
                    if (lower.EndsWith(".old") || lower.EndsWith(".new"))
                    {
                        try { File.Delete(f); } catch { /* still in use — the next launch gets it */ }
                    }
                }
            }
            catch { }
        }

        // ---------------- checking ----------------

        /// <summary>Called from the widget's minute tick. Checks shortly after startup and
        /// then every few hours — one request of a few hundred bytes — unless the user has
        /// turned automatic checking off.</summary>
        public static void BackgroundTick()
        {
            if (!Settings.I.AutoUpdateCheck) return;
            if (State == Phase.Checking || State == Phase.Downloading || State == Phase.Installing) return;
            var now = DateTime.UtcNow;
            if (!_startupChecked)
            {
                // let the widget settle (and the network come up after a cold boot) first
                if ((now - _startedUtc).TotalSeconds < 25) return;
                _startupChecked = true;
                Check(false);
                return;
            }
            if (UpdateCore.DueForCheck(Settings.I.LastUpdateCheck, now, CheckEveryHours)) Check(false);
        }

        /// <summary>Reads the channel manifest in the background. <paramref name="manual"/>
        /// marks a check the user asked for: its failures are shown, a background one's are
        /// only logged.</summary>
        public static void Check(bool manual)
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
            Set(Phase.Checking);
            Task.Run(() =>
            {
                UpdateManifest m = null;
                string err = "";
                try
                {
                    m = UpdateCore.ParseManifest(Http.Get(UpdateCore.ManifestUrl, 10000, UpdateCore.IsTrustedHost));
                    if (m == null) err = "upd_err_manifest";
                }
                catch (Exception ex)
                {
                    err = "upd_err_net";
                    if (manual) Log.Write("update check: " + ex.Message);
                }
                Post(() =>
                {
                    Interlocked.Exchange(ref _busy, 0);
                    Settings.I.LastUpdateCheck = UpdateCore.Stamp(DateTime.UtcNow);
                    Settings.I.Save();
                    if (m == null)
                    {
                        // a background failure must not wipe out an update already found
                        Set(IsAvailable ? Phase.Available : (manual ? Phase.Failed : Phase.Idle), manual ? err : "");
                        return;
                    }
                    bool was = IsAvailable;
                    Latest = m;
                    bool now = IsAvailable;
                    Set(now ? Phase.Available : Phase.UpToDate);
                    if (now != was && MainWindow.Instance != null) MainWindow.Instance.Reload();   // the tile appears / goes
                    if (now && _announced != m.Version)
                    {
                        _announced = m.Version;
                        if (!manual)
                            Toast.Show(string.Format(L.T("upd_found"), UpdateCore.Display(m.Version)), "\uE896",
                                L.T("upd_now"), () => Install());
                    }
                });
            });
        }

        // ---------------- installing ----------------

        /// <summary>Downloads, verifies and applies <see cref="Latest"/>, then restarts.</summary>
        public static void Install()
        {
            var m = Latest;
            if (m == null || !IsAvailable) return;
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
            Progress = 0;
            Set(Phase.Downloading);

            Task.Run(() =>
            {
                string exe = ExePath;
                string tmp = exe + ".new";
                try
                {
                    Fetch(m, tmp);
                    string why = UpdateCore.VerifyPackage(tmp, m.Size, m.Sha256);
                    if (why != null)
                    {
                        // The channel may have been republished between reading the manifest
                        // and fetching the file. Read it once more and try the newer build.
                        Log.Write("update verify: " + why + " — re-reading the channel");
                        var m2 = UpdateCore.ParseManifest(Http.Get(UpdateCore.ManifestUrl, 10000, UpdateCore.IsTrustedHost));
                        if (m2 == null || UpdateCore.HashEquals(m2.Sha256, m.Sha256)
                            || !UpdateCore.IsNewer(m2.Version, AppVersion.FourPart))
                            throw new InvalidDataException(why);
                        m = m2;
                        Fetch(m, tmp);
                        why = UpdateCore.VerifyPackage(tmp, m.Size, m.Sha256);
                        if (why != null) throw new InvalidDataException(why);
                    }

                    // the file must BE the build the manifest announced, and newer than this one
                    string fileVer = FileVersionInfo.GetVersionInfo(tmp).FileVersion;
                    if (!UpdateCore.IsNewer(fileVer, AppVersion.FourPart))
                        throw new InvalidDataException("package version " + fileVer + " is not newer");

                    var final = m;
                    Post(() => ApplyAndRestart(exe, tmp, final));
                }
                catch (UnauthorizedAccessException ex)
                {
                    // installed somewhere this user cannot write (e.g. Program Files)
                    Log.Write("update: folder not writable — " + ex.Message);
                    Fail("upd_err_folder", tmp, true);
                }
                catch (InvalidDataException ex)
                {
                    Log.Write("update rejected: " + ex.Message);
                    Fail("upd_err_verify", tmp, false);
                }
                catch (Exception ex)
                {
                    Log.Error("update download", ex);
                    Fail("upd_err_net", tmp, false);
                }
            });
        }

        static void Fetch(UpdateManifest m, string target)
        {
            string url = UpdateCore.FileUrl(m.File);
            if (url == null) throw new InvalidDataException("bad file name");
            long size = Math.Max(1, m.Size);
            int lastPct = -1;
            Http.Download(url, target, UpdateCore.MaxPackageBytes, UpdateCore.IsTrustedHost, (got, total) =>
            {
                int pct = (int)Math.Min(100, got * 100 / size);
                if (pct == lastPct) return;
                lastPct = pct;
                Post(() => { Progress = pct / 100.0; Set(Phase.Downloading); });
            }, null);
        }

        static void Fail(string key, string tmp, bool openPage)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            Post(() =>
            {
                Interlocked.Exchange(ref _busy, 0);
                Set(Phase.Failed, key);
                if (openPage) OpenProjectPage();
            });
        }

        static void ApplyAndRestart(string exe, string tmp, UpdateManifest m)
        {
            Set(Phase.Installing);
            string old = exe + ".old";
            try
            {
                try { if (File.Exists(old)) File.Delete(old); }
                catch { old = exe + "." + DateTime.UtcNow.Ticks.ToString("x") + ".old"; }

                File.Move(exe, old);                 // a running image may be renamed…
                try { File.Move(tmp, exe); }         // …and the new one takes its name
                catch
                {
                    try { File.Move(old, exe); } catch { }
                    throw;
                }

                RefreshUninstallEntry(Path.GetDirectoryName(exe), m.Version);
                try { Settings.I.Save(); } catch { }

                Process.Start(new ProcessStartInfo(exe, "--updated " + AppVersion.FourPart)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exe),
                });
                if (MainWindow.Instance != null) MainWindow.Instance.ExitApp();
                else Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error("update apply", ex);
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                Interlocked.Exchange(ref _busy, 0);
                Set(Phase.Failed, "upd_err_apply");
            }
        }

        /// <summary>An installed copy lists its version under "Apps & features". Keep that
        /// line honest after a self-update — but only for the folder the installer recorded,
        /// never for a portable copy that happens to sit elsewhere.</summary>
        static void RefreshUninstallEntry(string dir, string fourPart)
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DeskMate", true))
                {
                    if (k == null) return;
                    string recorded = k.GetValue("InstallLocation") as string;
                    if (string.IsNullOrEmpty(recorded)) return;
                    if (!string.Equals(Path.GetFullPath(recorded).TrimEnd('\\'),
                            Path.GetFullPath(dir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
                    Version v;
                    if (UpdateCore.TryParseVersion(fourPart, out v))
                        k.SetValue("DisplayVersion", v.Major + "." + v.Minor + "." + v.Build);
                }
            }
            catch (Exception ex) { Log.Error("update registry", ex); }
        }

        public static void OpenProjectPage()
        {
            try { Process.Start(new ProcessStartInfo(UpdateCore.ReleasesPage) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Error("open releases", ex); }
        }

        // ---------------- panel ----------------

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "update", L.T("f_update"), Build, 320);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();

            var headline = Theme.MakeText("", 15, Theme.Text);
            headline.FontWeight = FontWeights.Bold;
            headline.HorizontalAlignment = HorizontalAlignment.Center;
            headline.TextAlignment = TextAlignment.Center;
            headline.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(headline);

            // current → new, as two chips with an arrow between them
            var chips = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 4),
                FlowDirection = FlowDirection.LeftToRight,
            };
            var curChip = VersionChip(false);
            var arrow = Theme.MakeGlyph("\uE72A", 12, Theme.TextFaint);     // E72A forward arrow
            arrow.Margin = new Thickness(10, 0, 10, 0);
            var newChip = VersionChip(true);
            chips.Children.Add(curChip);
            chips.Children.Add(arrow);
            chips.Children.Add(newChip);
            stack.Children.Add(chips);

            var notes = Theme.MakeText("", 12.5, Theme.TextDim);
            notes.TextWrapping = TextWrapping.Wrap;
            notes.TextAlignment = TextAlignment.Center;
            notes.HorizontalAlignment = HorizontalAlignment.Center;
            notes.Margin = new Thickness(4, 6, 4, 0);
            stack.Children.Add(notes);

            // progress track
            var fill = new Border
            {
                Background = Theme.AccentGradient ?? Theme.Accent,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0,
            };
            var track = new Border
            {
                Background = Theme.InputBg,
                CornerRadius = new CornerRadius(3),
                Height = 6,
                Margin = new Thickness(2, 12, 2, 0),
                Child = fill,
                FlowDirection = FlowDirection.LeftToRight,
                Visibility = Visibility.Collapsed,
            };
            stack.Children.Add(track);

            var status = Theme.MakeText("", 12, Theme.TextFaint);
            status.HorizontalAlignment = HorizontalAlignment.Center;
            status.TextAlignment = TextAlignment.Center;
            status.TextWrapping = TextWrapping.Wrap;
            status.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(status);

            var action = Theme.MakeButton("", () =>
            {
                if (IsAvailable && State != Phase.Downloading && State != Phase.Installing) Install();
                else if (State != Phase.Checking && State != Phase.Downloading && State != Phase.Installing) Check(true);
            }, true, new Thickness(22, 9, 22, 9));
            action.HorizontalAlignment = HorizontalAlignment.Center;
            action.Margin = new Thickness(0, 12, 0, 2);
            stack.Children.Add(action);

            var page = Theme.MakeText(L.T("upd_page"), 11.5, Theme.TextFaint);
            page.HorizontalAlignment = HorizontalAlignment.Center;
            page.Margin = new Thickness(0, 8, 0, 0);
            page.Cursor = System.Windows.Input.Cursors.Hand;
            page.TextDecorations = TextDecorations.Underline;
            page.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            page.MouseLeftButtonUp += (s, e) => { e.Handled = true; OpenProjectPage(); };
            stack.Children.Add(page);

            Action render = () =>
            {
                bool avail = IsAvailable;
                ((TextBlock)curChip.Tag).Text = UpdateCore.Display(AppVersion.FourPart);
                ((TextBlock)newChip.Tag).Text = avail ? UpdateCore.Display(Latest.Version) : "";
                arrow.Visibility = newChip.Visibility = avail ? Visibility.Visible : Visibility.Collapsed;
                notes.Text = avail ? Latest.Notes : "";
                notes.Visibility = string.IsNullOrEmpty(notes.Text) ? Visibility.Collapsed : Visibility.Visible;

                headline.Text = avail ? L.T("upd_available")
                    : State == Phase.UpToDate ? L.T("upd_uptodate")
                    : L.T("upd_title");

                bool working = State == Phase.Downloading || State == Phase.Installing;
                track.Visibility = working ? Visibility.Visible : Visibility.Collapsed;
                if (working)
                {
                    double w = track.ActualWidth > 0 ? track.ActualWidth : 280;
                    fill.Width = Math.Max(0, Math.Min(1, State == Phase.Installing ? 1 : Progress)) * w;
                }

                switch (State)
                {
                    case Phase.Checking: status.Text = L.T("upd_checking"); break;
                    case Phase.Downloading: status.Text = string.Format(L.T("upd_downloading"), (int)Math.Round(Progress * 100)); break;
                    case Phase.Installing: status.Text = L.T("upd_installing"); break;
                    case Phase.Failed: status.Text = L.T(string.IsNullOrEmpty(ErrorKey) ? "upd_err_net" : ErrorKey); break;
                    default:
                        status.Text = avail ? L.T("upd_hint") : LastCheckedText();
                        break;
                }
                status.Foreground = State == Phase.Failed ? Theme.Accent : Theme.TextFaint;
                status.Visibility = string.IsNullOrEmpty(status.Text) ? Visibility.Collapsed : Visibility.Visible;

                Theme.SetButtonText(action, avail ? L.T("upd_now") : L.T("upd_check"));
                action.Opacity = State == Phase.Checking || working ? 0.5 : 1.0;
                action.IsHitTestVisible = !(State == Phase.Checking || working);
            };

            Action onChanged = () => { if (host.IsVisible) render(); };
            Changed += onChanged;
            host.Closed += (s, e) => Changed -= onChanged;
            host.Loaded += (s, e) =>
            {
                render();
                // opening the panel is itself a request for fresh information
                if (!IsAvailable && State != Phase.Checking) Check(true);
            };
            render();
            return stack;
        }

        static string LastCheckedText()
        {
            DateTime last;
            if (!DateTime.TryParse(Settings.I.LastUpdateCheck, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out last))
                return "";
            return string.Format(L.T("upd_last"), last.ToLocalTime().ToString("yyyy-MM-dd  HH:mm"));
        }

        /// <summary>Small pill showing a version number; the accent one marks the new build.
        /// The TextBlock is kept in Tag so the panel can refill it.</summary>
        static Border VersionChip(bool accent)
        {
            var tb = Theme.MakeText("", 13.5, accent ? Theme.Accent : Theme.TextDim);
            tb.FontWeight = FontWeights.SemiBold;
            tb.FlowDirection = FlowDirection.LeftToRight;
            return new Border
            {
                Background = accent ? Theme.AccentSoft : Theme.ButtonBg,
                BorderBrush = accent ? Theme.Accent : Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(12, 4, 12, 4),
                Child = tb,
                Tag = tb,
            };
        }
    }
}
