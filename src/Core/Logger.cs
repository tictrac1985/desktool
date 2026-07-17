using System;
using System.IO;

namespace DeskMate.Core
{
    public static class Log
    {
        static readonly object Gate = new object();

        public static string Dir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskMate");
                try { Directory.CreateDirectory(d); } catch { }
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
            Write("[" + context + "] " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
