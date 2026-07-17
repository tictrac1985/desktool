using System;
using System.Collections.Generic;
using System.IO;

namespace DeskMate.Logic
{
    /// <summary>Deletes leftover files from the given directories, skipping anything locked
    /// or inaccessible. Pure file-system logic — recycle bin handling lives in the UI layer.</summary>
    public static class CleanEngine
    {
        public static IEnumerable<string> DefaultDirs()
        {
            var list = new List<string>();
            string t = Path.GetTempPath();
            if (!string.IsNullOrEmpty(t)) list.Add(t.TrimEnd('\\'));
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(win)) list.Add(Path.Combine(win, "Temp"));
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(local)) list.Add(Path.Combine(local, @"Microsoft\Windows\INetCache"));
            return list;
        }

        /// <summary>Deletes contents of each directory (never the directory itself).
        /// Returns bytes freed. Locked/inaccessible files are silently skipped.</summary>
        public static long Clean(IEnumerable<string> dirs, Action<string> progress = null)
        {
            long freed = 0;
            foreach (string dir in dirs)
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    if (progress != null) progress(dir);
                    freed += CleanDirContents(dir);
                }
                catch { }
            }
            return freed;
        }

        static long CleanDirContents(string dir)
        {
            long freed = 0;
            string[] files = null, subs = null;
            try { files = Directory.GetFiles(dir); } catch { }
            if (files != null)
            {
                foreach (string f in files)
                {
                    try
                    {
                        long size = new FileInfo(f).Length;
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                        freed += size;
                    }
                    catch { }
                }
            }
            try { subs = Directory.GetDirectories(dir); } catch { }
            if (subs != null)
            {
                foreach (string sub in subs)
                {
                    freed += CleanDirContents(sub);
                    try { Directory.Delete(sub, false); } catch { }
                }
            }
            return freed;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024 * 1024)).ToString("0.##") + " GB";
            if (bytes >= 1024L * 1024) return (bytes / (1024.0 * 1024)).ToString("0.#") + " MB";
            if (bytes >= 1024) return (bytes / 1024.0).ToString("0.#") + " KB";
            return bytes + " B";
        }
    }
}
