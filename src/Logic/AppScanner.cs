using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeskMate.Logic
{
    public class AppEntry
    {
        public string Name;
        public string LnkPath;
    }

    /// <summary>Enumerates installed programs from Start Menu shortcuts (user + all-users).</summary>
    public static class AppScanner
    {
        static readonly string[] ExcludeWords =
        {
            "uninstall", "uninstal", "remove", "إزالة", "إلغاء التثبيت", "readme", "release notes", "help"
        };

        public static string[] DefaultRoots()
        {
            return new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                             @"Microsoft\Windows\Start Menu\Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                             @"Microsoft\Windows\Start Menu\Programs"),
            };
        }

        public static List<AppEntry> Scan(params string[] roots)
        {
            if (roots == null || roots.Length == 0) roots = DefaultRoots();
            var byName = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (string lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                    {
                        string name = Path.GetFileNameWithoutExtension(lnk);
                        string low = name.ToLowerInvariant();
                        bool skip = false;
                        foreach (string w in ExcludeWords)
                            if (low.Contains(w)) { skip = true; break; }
                        if (skip) continue;
                        if (!byName.ContainsKey(name))
                            byName[name] = new AppEntry { Name = name, LnkPath = lnk };
                    }
                }
                catch { }
            }
            return byName.Values.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
