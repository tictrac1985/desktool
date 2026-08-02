using System;
using System.IO;

namespace DeskMate.Core
{
    public static class Log
    {
        static readonly object Gate = new object();
        static string _dir;

        /// <summary>%AppData%\DeskMate, created once. This is read on every log write and on
        /// every settings save, so the directory probe is cached rather than repeated.</summary>
        public static string Dir
        {
            get
            {
                if (_dir != null) return _dir;
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskMate");
                try { Directory.CreateDirectory(d); } catch { }
                _dir = d;
                return d;
            }
        }

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    string file = Path.Combine(Dir, "log.txt");
                    // keep the log from growing forever
                    try
                    {
                        var fi = new FileInfo(file);
                        if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
                    }
                    catch { }
                    File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
                }
            }
            catch { }
        }

        public static void Error(string context, Exception ex)
        {
            // ex.ToString() keeps the stack trace and inner exceptions — type+message alone
            // made production failures nearly impossible to diagnose
            Write("[" + context + "] " + ex);
        }
    }
}
