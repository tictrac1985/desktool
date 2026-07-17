using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeskMate.Setup
{
    /// <summary>Installer / uninstaller for DeskMate. The app exe is embedded as the
    /// "DeskMate.Payload" resource. Copying this exe as Uninstall.exe turns it into
    /// the uninstaller (detected by file name or the --uninstall switch).</summary>
    static class SetupProgram
    {
        const string AppName = "DeskMate";
        const string Version = "1.0.0";
        static bool _ar = true;

        static string T(string ar, string en) { return _ar ? ar : en; }

        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool silent = args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
            bool uninstall = args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)) ||
                Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)
                    .ToLowerInvariant().Contains("uninstall");
            if (uninstall) { RunUninstall(silent); return 0; }
            if (silent)
            {
                try { SilentInstall(); return 0; }
                catch (Exception ex) { Console.WriteLine("install failed: " + ex.Message); return 1; }
            }
            Application.Run(new InstallForm());
            return 0;
        }

        // ---------------- install ----------------

        /// <summary>Unattended install with default options (no shortcuts prompt, no launch).</summary>
        static void SilentInstall()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
            Directory.CreateDirectory(dir);
            string exePath = Path.Combine(dir, "DeskMate.exe");
            KillRunning(dir);
            ExtractPayload(exePath);
            File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(dir, "Uninstall.exe"), true);
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"), exePath);
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName + ".lnk"), exePath);
            using (var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                run.SetValue(AppName, "\"" + exePath + "\"");
            WriteUninstallKey(dir, exePath);
        }

        class InstallForm : Form
        {
            TextBox _path;
            CheckBox _desktop, _startMenu, _autostart, _runAfter;
            Button _install;
            Label _title, _pathLbl, _status;
            LinkLabel _lang;

            public InstallForm()
            {
                Text = "DeskMate Setup";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new Size(460, 330);
                BackColor = Color.FromArgb(18, 22, 32);
                ForeColor = Color.White;
                Font = new Font("Segoe UI", 9.5f);
                try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }

                _lang = new LinkLabel { AutoSize = true, LinkColor = Color.FromArgb(255, 198, 110), Text = "English" };
                _lang.Click += (s, e) => { _ar = !_ar; ApplyLang(); };
                Controls.Add(_lang);

                _title = new Label { AutoSize = true, Font = new Font("Segoe UI", 16f, FontStyle.Bold) };
                Controls.Add(_title);

                _pathLbl = new Label { AutoSize = true };
                Controls.Add(_pathLbl);
                _path = new TextBox
                {
                    Width = 330,
                    BackColor = Color.FromArgb(30, 36, 48),
                    ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName),
                };
                Controls.Add(_path);
                var browse = new Button
                {
                    Text = "…", Width = 34, Height = 26,
                    FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
                };
                browse.FlatAppearance.BorderColor = Color.FromArgb(70, 80, 100);
                browse.Click += (s, e) =>
                {
                    using (var dlg = new FolderBrowserDialog())
                    {
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                            _path.Text = Path.Combine(dlg.SelectedPath, AppName);
                    }
                };
                Controls.Add(browse);
                _browse = browse;

                _desktop = MakeCheck(true);
                _startMenu = MakeCheck(true);
                _autostart = MakeCheck(true);
                _runAfter = MakeCheck(true);

                _install = new Button
                {
                    Width = 200, Height = 38,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(255, 198, 110),
                    ForeColor = Color.Black,
                    Font = new Font("Segoe UI Semibold", 11f),
                };
                _install.FlatAppearance.BorderSize = 0;
                _install.Click += (s, e) => DoInstall();
                Controls.Add(_install);

                _status = new Label { AutoSize = true, ForeColor = Color.FromArgb(255, 198, 110) };
                Controls.Add(_status);

                ApplyLang();
            }

            Button _browse;

            CheckBox MakeCheck(bool state)
            {
                var cb = new CheckBox { AutoSize = true, Checked = state, ForeColor = Color.White };
                Controls.Add(cb);
                return cb;
            }

            void ApplyLang()
            {
                RightToLeft = _ar ? RightToLeft.Yes : RightToLeft.No;
                RightToLeftLayout = _ar;
                _lang.Text = _ar ? "English" : "العربية";
                _title.Text = T("تثبيت ديسك ميت", "Install DeskMate");
                _pathLbl.Text = T("مجلد التثبيت:", "Install folder:");
                _desktop.Text = T("إنشاء أيقونة على سطح المكتب", "Create desktop shortcut");
                _startMenu.Text = T("إضافة لقائمة ابدأ", "Add to Start Menu");
                _autostart.Text = T("التشغيل مع بدء ويندوز", "Start with Windows");
                _runAfter.Text = T("تشغيل البرنامج بعد التثبيت", "Launch after install");
                _install.Text = T("تثبيت", "Install");
                Layout2();
            }

            void Layout2()
            {
                int x = 24, w = ClientSize.Width;
                _lang.Location = new Point(w - _lang.Width - 12, 12);
                _title.Location = new Point(x, 18);
                _pathLbl.Location = new Point(x, 66);
                _path.Location = new Point(x, 90);
                _browse.Location = new Point(x + _path.Width + 8, 89);
                int y = 128;
                foreach (var cb in new[] { _desktop, _startMenu, _autostart, _runAfter })
                {
                    cb.Location = new Point(x, y);
                    y += 28;
                }
                _install.Location = new Point((w - _install.Width) / 2, y + 12);
                _status.Location = new Point(x, y + 60);
            }

            void DoInstall()
            {
                try
                {
                    _install.Enabled = false;
                    string dir = _path.Text.Trim();
                    if (dir.Length == 0) return;
                    Directory.CreateDirectory(dir);
                    string exePath = Path.Combine(dir, "DeskMate.exe");

                    // stop a running instance so the file can be replaced
                    KillRunning(dir);

                    _status.Text = T("جارٍ نسخ الملفات…", "Copying files…");
                    Refresh();
                    ExtractPayload(exePath);
                    File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(dir, "Uninstall.exe"), true);

                    if (_desktop.Checked)
                        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"), exePath);
                    if (_startMenu.Checked)
                        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName + ".lnk"), exePath);
                    using (var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        if (_autostart.Checked) run.SetValue(AppName, "\"" + exePath + "\"");
                        else run.DeleteValue(AppName, false);
                    }
                    WriteUninstallKey(dir, exePath);

                    _status.Text = T("تم التثبيت بنجاح ✓", "Installed successfully ✓");
                    if (_runAfter.Checked) Process.Start(exePath);
                    var timer = new Timer { Interval = 1400 };
                    timer.Tick += (s, e) => { timer.Stop(); Close(); };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    _install.Enabled = true;
                    _status.Text = T("خطأ: ", "Error: ") + ex.Message;
                }
            }
        }

        static void ExtractPayload(string destPath)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("DeskMate.Payload"))
            {
                if (s == null) throw new InvalidOperationException("payload missing");
                using (var f = File.Create(destPath)) s.CopyTo(f);
            }
        }

        static void CreateShortcut(string lnkPath, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnkPath));
            var t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(target) });
            st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        }

        static void WriteUninstallKey(string dir, string exePath)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
            {
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", AppName);
                k.SetValue("DisplayIcon", exePath);
                k.SetValue("InstallLocation", dir);
                k.SetValue("UninstallString", "\"" + Path.Combine(dir, "Uninstall.exe") + "\" --uninstall");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                try
                {
                    long size = new FileInfo(exePath).Length / 1024;
                    k.SetValue("EstimatedSize", (int)size, RegistryValueKind.DWord);
                }
                catch { }
            }
        }

        static void KillRunning(string dir)
        {
            foreach (var p in Process.GetProcessesByName("DeskMate"))
            {
                try
                {
                    string path = p.MainModule.FileName;
                    if (string.IsNullOrEmpty(dir) || path.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                }
                catch { }
            }
        }

        // ---------------- uninstall ----------------

        static void RunUninstall(bool silent)
        {
            var opts = _ar ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : 0;
            if (!silent && MessageBox.Show(
                T("هل تريد إزالة ديسك ميت من هذا الجهاز؟", "Remove DeskMate from this PC?"),
                "DeskMate", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1, opts) != DialogResult.Yes) return;

            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            KillRunning(null);

            // shortcuts
            TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"));
            TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName + ".lnk"));
            // registry
            try
            {
                using (var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    if (run != null) run.DeleteValue(AppName, false);
            }
            catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName, false); } catch { }

            // settings
            if (!silent && MessageBox.Show(
                T("هل تريد حذف الإعدادات المحفوظة أيضاً؟", "Delete saved settings too?"),
                "DeskMate", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2, opts) == DialogResult.Yes)
            {
                try
                {
                    string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
                    if (Directory.Exists(appData)) Directory.Delete(appData, true);
                }
                catch { }
            }

            // delete files in the install dir now; schedule removal of the dir (this exe is inside it)
            foreach (var f in Directory.GetFiles(dir))
            {
                if (!f.EndsWith("Uninstall.exe", StringComparison.OrdinalIgnoreCase)) TryDelete(f);
            }
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping 127.0.0.1 -n 3 >nul & rd /s /q \"" + dir + "\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            try { Process.Start(psi); } catch { }
            if (!silent) MessageBox.Show(
                T("تمت إزالة ديسك ميت بنجاح", "DeskMate was removed successfully"),
                "DeskMate", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, opts);
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
