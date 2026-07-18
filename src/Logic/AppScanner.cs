using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeskMate.Logic
{
    public class AppEntry
    {
        public string Name;
        public string LnkPath;          // classic Start Menu shortcut (null for Store apps)
        public string AppsFolderPath;   // shell:AppsFolder path / AUMID (Store & modern apps)
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

        /// <summary>Everything the Start menu knows: classic .lnk shortcuts plus Store/UWP apps
        /// from shell:AppsFolder (Claude, ChatGPT, etc. install as Store apps with no .lnk).</summary>
        public static List<AppEntry> ScanAll()
        {
            var list = Scan();
            var have = new HashSet<string>(list.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);
            try
            {
                var t = Type.GetTypeFromProgID("Shell.Application");
                object shell = Activator.CreateInstance(t);
                object folder = t.InvokeMember("NameSpace", System.Reflection.BindingFlags.InvokeMethod,
                    null, shell, new object[] { "shell:AppsFolder" });
                if (folder != null)
                {
                    object items = folder.GetType().InvokeMember("Items",
                        System.Reflection.BindingFlags.InvokeMethod, null, folder, null);
                    int count = Convert.ToInt32(items.GetType().InvokeMember("Count",
                        System.Reflection.BindingFlags.GetProperty, null, items, null));
                    for (int i = 0; i < count; i++)
                    {
                        object item = items.GetType().InvokeMember("Item",
                            System.Reflection.BindingFlags.InvokeMethod, null, items, new object[] { i });
                        if (item == null) continue;
                        Type it = item.GetType();
                        string name = it.InvokeMember("Name", System.Reflection.BindingFlags.GetProperty, null, item, null) as string;
                        string path = it.InvokeMember("Path", System.Reflection.BindingFlags.GetProperty, null, item, null) as string;
                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) continue;
                        if (have.Contains(name)) continue;
                        string low = name.ToLowerInvariant();
                        bool skip = false;
                        foreach (string w in ExcludeWords)
                            if (low.Contains(w)) { skip = true; break; }
                        if (skip) continue;
                        have.Add(name);
                        list.Add(new AppEntry { Name = name, AppsFolderPath = path });
                    }
                }
            }
            catch (Exception ex) { DeskMate.Core.Log.Error("appsfolder", ex); }
            return list.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
