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
        public static int Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "DeskMate.SingleInstance", out createdNew))
            {
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
                UI.Theme.Apply(Logic.AutoThemeCore.EffectiveIndex(Settings.I.ThemeIndex, Settings.I.AutoTheme, DateTime.Now.Hour));

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

                var win = new UI.MainWindow();
                StartShowListener(app);
                win.Show();
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
