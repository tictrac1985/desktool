using System;
using System.Threading;
using System.Windows;
using DeskMate.Core;

namespace DeskMate
{
    public static class Program
    {
        static EventWaitHandle _showEvent;

        [STAThread]
        public static int Main(string[] args)
        {
            bool afterUpdate = args != null && Array.IndexOf(args, "--updated") >= 0;
            bool createdNew;
            using (var mutex = new Mutex(true, "DeskMate.SingleInstance", out createdNew))
            {
                if (!createdNew && afterUpdate)
                {
                    // This launch is the restart after a self-update: the previous version
                    // started us and is on its way out. Wait for it to let go of the
                    // single-instance mutex instead of bowing out as a "second launch".
                    try { createdNew = mutex.WaitOne(TimeSpan.FromSeconds(15)); }
                    catch (AbandonedMutexException) { createdNew = true; }
                }
                if (!createdNew)
                {
                    // second launch (e.g. desktop icon): tell the running instance to show itself
                    try
                    {
                        var ev = EventWaitHandle.OpenExisting("DeskMate.ShowSignal");
                        ev.Set();
                    }
                    catch { }
                    return 0;
                }

                _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "DeskMate.ShowSignal");
                Settings.Load();
                UI.Theme.Apply(Logic.AutoThemeCore.EffectiveIndex(Settings.I.ThemeIndex, Settings.I.LightThemeIndex, Settings.I.AutoTheme, DateTime.Now.Hour));

                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.DispatcherUnhandledException += (s, e) =>
                {
                    Log.Error("Unhandled", e.Exception);
                    e.Handled = true;
                };
                app.SessionEnding += (s, e) =>
                {
                    // OS shutdown/logoff: let windows close and flush settings one last time
                    UI.MainWindow.OsSessionEnding = true;
                    try { Settings.I.Save(); } catch { }
                };

                // A value that could not be encrypted is dropped rather than written in the
                // clear. Say so once: silently losing what the user just typed is the kind of
                // failure that gets blamed on the app "eating" notes.
                bool warned = false;
                Secret.ProtectFailed += ctx =>
                {
                    if (warned) return;
                    warned = true;
                    app.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { UI.Toast.Show(L.T("sec_protect_err")); } catch { }
                    }));
                };

                var win = new UI.MainWindow();
                StartShowListener(app);
                win.Show();
                Features.UpdateFeature.OnStartup(args);
                app.Run();
                return 0;
            }
        }

        static void StartShowListener(Application app)
        {
            var t = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        _showEvent.WaitOne();
                        app.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (UI.MainWindow.Instance != null)
                                    UI.MainWindow.Instance.ShowFromTray();
                            }
                            catch (Exception ex) { Log.Error("ShowSignal", ex); }
                        }));
                    }
                    catch { break; }
                }
            });
            t.IsBackground = true;
            t.Start();
        }
    }
}
