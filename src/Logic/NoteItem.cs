using System;
using System.Collections.Generic;
using System.Globalization;
using DeskMate.Core;

namespace DeskMate.Logic
{
    /// <summary>One sticky note: its text, whether it is pinned to the top, and an optional
    /// reminder time. Stored as a compact JSON object inside a single settings string so the
    /// whole note still travels through the existing DPAPI encryption — note text is personal
    /// and must not become readable on disk just because it gained structure.</summary>
    public class NoteItem
    {
        public string Text = "";
        public bool Pinned;
        /// <summary>Completed but kept: a finished task is struck through and sinks to the
        /// bottom of the list instead of vanishing — tapping the circle by mistake must
        /// never cost the user a task.</summary>
        public bool Done;
        /// <summary>When the task was completed (MinValue = not done / unknown). Drives the
        /// daily "done X of Y" progress line — finishing things should feel like progress,
        /// not like erasing them.</summary>
        public DateTime DoneAt = DateTime.MinValue;
        /// <summary>When the reminder should fire. DateTime.MinValue = no reminder.
        /// Cleared once it has fired, so a note never nags twice.</summary>
        public DateTime RemindAt = DateTime.MinValue;

        const string Stamp = "yyyy-MM-dd HH:mm";

        public bool HasReminder { get { return RemindAt != DateTime.MinValue; } }

        public string Write()
        {
            var d = new Dictionary<string, object>
            {
                { "t", Text ?? "" },
                { "p", Pinned },
                { "d", Done },
                { "da", Done && DoneAt != DateTime.MinValue
                        ? DoneAt.ToString(Stamp, CultureInfo.InvariantCulture) : "" },
                { "r", HasReminder ? RemindAt.ToString(Stamp, CultureInfo.InvariantCulture) : "" },
            };
            return Json.Write(d);
        }

        /// <summary>Reads a note back. Returns null for anything unparseable rather than a
        /// half-built note: a corrupt entry should disappear from the list, not show up as a
        /// blank row the user cannot explain or get rid of.</summary>
        public static NoteItem Parse(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return null;
            try
            {
                var d = Json.Parse(stored) as Dictionary<string, object>;
                if (d == null) return null;
                var n = new NoteItem
                {
                    Text = Json.GetStr(d, "t", ""),
                    Pinned = Json.GetBool(d, "p", false),
                    Done = Json.GetBool(d, "d", false),
                };
                string r = Json.GetStr(d, "r", "");
                DateTime at;
                if (!string.IsNullOrEmpty(r) && DateTime.TryParseExact(
                        r, Stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out at))
                    n.RemindAt = at;
                string da = Json.GetStr(d, "da", "");
                DateTime doneAt;
                if (n.Done && !string.IsNullOrEmpty(da) && DateTime.TryParseExact(
                        da, Stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out doneAt))
                    n.DoneAt = doneAt;
                return string.IsNullOrEmpty(n.Text) && !n.HasReminder ? null : n;
            }
            catch { return null; }
        }

        /// <summary>Pinned notes first, then open tasks, with completed ones sunk to the
        /// bottom — each group keeping the order the user arranged. A done task stays
        /// visible (struck through) rather than disappearing.</summary>
        public static List<NoteItem> Sorted(IEnumerable<NoteItem> notes)
        {
            var pinned = new List<NoteItem>();
            var rest = new List<NoteItem>();
            var done = new List<NoteItem>();
            foreach (var n in notes)
            {
                if (n == null) continue;
                if (n.Done) done.Add(n);
                else (n.Pinned ? pinned : rest).Add(n);
            }
            pinned.AddRange(rest);
            pinned.AddRange(done);
            return pinned;
        }

        /// <summary>Today's tally for the progress line: tasks completed today (by
        /// <see cref="DoneAt"/>) plus everything still open. Tasks finished on earlier
        /// days no longer count — the line answers "how is TODAY going".</summary>
        public static void DailyProgress(IEnumerable<NoteItem> notes, DateTime today, out int done, out int total)
        {
            done = 0; total = 0;
            if (notes == null) return;
            foreach (var n in notes)
            {
                if (n == null) continue;
                if (n.Done)
                {
                    if (n.DoneAt != DateTime.MinValue && n.DoneAt.Date == today.Date)
                    {
                        done++;
                        total++;
                    }
                }
                else total++;
            }
        }

        /// <summary>Indices of notes whose reminder is due at <paramref name="now"/>.
        /// "Due" means at or before now, not exactly now: the widget's tick can be delayed
        /// under load, and a reminder that is merely late must still fire rather than be
        /// skipped forever because its exact minute went by unobserved. Completed tasks
        /// stay quiet — finishing something must not make it nag afterwards.</summary>
        public static List<int> DueIndices(List<NoteItem> notes, DateTime now)
        {
            var due = new List<int>();
            if (notes == null) return due;
            for (int i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                if (n != null && !n.Done && n.HasReminder && n.RemindAt <= now) due.Add(i);
            }
            return due;
        }

        /// <summary>Single-line preview for the list row.</summary>
        public string Preview(int max = 80)
        {
            string t = (Text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            if (t.Length <= max) return t;
            return t.Substring(0, max) + "…";
        }
    }
}
