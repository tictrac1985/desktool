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

        /// <summary>Hard stop on recursion depth. Real temp trees are shallow; anything
        /// deeper is either pathological or an attempt to exhaust the stack.</summary>
        const int MaxDepth = 32;

        /// <summary>True when the path is a junction, symlink or mount point.
        /// Unreadable paths report true so that "unknown" is treated as "do not touch".</summary>
        public static bool IsReparsePoint(string path)
        {
            try { return (new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) != 0; }
            catch { return true; }
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
                    if (IsReparsePoint(dir)) continue;
                    if (progress != null) progress(dir);
                    freed += CleanDirContents(dir, 0);
                }
                catch { }
            }
            return freed;
        }

        static long CleanDirContents(string dir, int depth)
        {
            if (depth > MaxDepth) return 0;
            // re-verify at recursion entry (TOCTOU): a hostile process sharing the temp dir
            // could swap a checked directory for a junction between the parent-level check
            // and the descent. Whatever has become a reparse point is unlinked, never followed.
            if (depth > 0 && IsReparsePoint(dir))
            {
                try { Directory.Delete(dir, false); } catch { }
                return 0;
            }
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
                    // A junction inside Temp can point anywhere — Documents, another user's
                    // profile, a network share. Recursing would delete the *target's* contents,
                    // so remove the link itself and never follow it.
                    if (IsReparsePoint(sub))
                    {
                        try { Directory.Delete(sub, false); } catch { }
                        continue;
                    }
                    freed += CleanDirContents(sub, depth + 1);
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
