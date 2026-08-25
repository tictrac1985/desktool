using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>A to-do list: each entry can be completed, postponed, pinned or deleted, and
    /// may carry a reminder that alerts when it comes due.
    /// Shaped as a task list rather than a notepad because that is what the entries are — the
    /// earlier version offered a bare day box and two unlabelled number boxes, which told the
    /// user nothing about what they were setting.</summary>
    public static class StickyNoteFeature
    {
        /// <summary>Raised after a background reminder changes the list, so an open panel
        /// redraws instead of showing a reminder that has already fired.</summary>
        public static event Action Changed;

        /// <summary>Redraws any open tasks panel after an out-of-band change (undo from a
        /// toast).</summary>
        static void RaiseChanged()
        {
            var handler = Changed;
            if (handler != null) { try { handler(); } catch (Exception ex) { Log.Error("note changed", ex); } }
        }

        static CultureInfo Ci { get { return L.IsAr ? new CultureInfo("ar-EG") : new CultureInfo("en-US"); } }

        // ---------------- storage ----------------

        /// <summary>Reads the tasks out of settings. Migrates the old single note on the way:
        /// it is moved into the list the first time this runs and the old field is cleared, so
        /// nobody loses what they had written. A sealed (undecryptable) old note is left alone
        /// — it is not ours to convert or discard.</summary>
        public static List<NoteItem> Load()
        {
            var list = new List<NoteItem>();
            foreach (string raw in Settings.I.Notes)
            {
                if (Secret.IsSealed(raw)) continue;   // another profile's note: keep, don't show
                var n = NoteItem.Parse(raw);
                if (n != null) list.Add(n);
            }

            string old = Settings.I.StickyNote;
            if (!string.IsNullOrWhiteSpace(old) && !Secret.IsSealed(old))
            {
                list.Insert(0, new NoteItem { Text = old });
                Settings.I.StickyNote = "";
                Save(list);
            }
            return NoteItem.Sorted(list);
        }

        /// <summary>Writes the list back, preserving any sealed entries belonging to another
        /// Windows profile — rewriting the list must not be a way to silently drop them.</summary>
        public static void Save(List<NoteItem> notes)
        {
            var sealedOnes = new List<string>();
            foreach (string raw in Settings.I.Notes)
                if (Secret.IsSealed(raw)) sealedOnes.Add(raw);

            Settings.I.Notes.Clear();
            foreach (var n in notes)
                if (n != null && !string.IsNullOrWhiteSpace(n.Text)) Settings.I.Notes.Add(n.Write());
            Settings.I.Notes.AddRange(sealedOnes);
            Settings.I.Save();
        }

        // ---------------- reminders ----------------

        static DateTime _lastCheck = DateTime.MinValue;

        /// <summary>Driven by the widget's one-second clock. Fires any due reminder and clears
        /// it, so a task nags once. Checked once a minute rather than every tick — the list is
        /// read from settings each time and that is not free.</summary>
        public static void BackgroundTick(DateTime now)
        {
            var minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            if (minute == _lastCheck) return;
            _lastCheck = minute;

            var notes = Load();
            var due = NoteItem.DueIndices(notes, now);
            if (due.Count == 0) return;

            foreach (int i in due)
            {
                var n = notes[i];
                n.RemindAt = DateTime.MinValue;   // cleared first: a failed toast must not re-fire
                Toast.Show(n.Preview(60), Glyphs.Note);
            }
            AlertSound.Play();
            Save(notes);
            RaiseChanged();
        }

        // ---------------- panel ----------------

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "note", L.T("f_note"), Build, 360);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();
            var listHost = new StackPanel();

            Action rebuild = null;
            rebuild = () =>
            {
                listHost.Children.Clear();
                var notes = Load();
                if (notes.Count == 0)
                {
                    var empty = Theme.MakeText(L.T("nt_empty"), 12.5, Theme.TextFaint);
                    empty.TextWrapping = TextWrapping.Wrap;
                    empty.Margin = new Thickness(4, 10, 4, 10);
                    listHost.Children.Add(empty);
                }
                else
                {
                    foreach (var n in notes) listHost.Children.Add(MakeRow(host, n, notes, rebuild));
                }
            };
            rebuild();

            Action onChanged = () => host.Dispatcher.BeginInvoke(new Action(rebuild));
            Changed += onChanged;
            host.Closed += (s, e) => Changed -= onChanged;

            // daily progress: a slim accent bar + "done X of Y today" above the list —
            // finishing tasks should feel like forward motion, not like erasing them
            int doneToday, totalToday;
            NoteItem.DailyProgress(Load(), DateTime.Now, out doneToday, out totalToday);
            if (totalToday > 0)
            {
                var barBack = new Border
                {
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = Theme.ButtonBg,
                    Margin = new Thickness(2, 0, 2, 4),
                };
                var fill = new Border
                {
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = Theme.Accent,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 0,
                };
                var barGrid = new Grid();
                barGrid.Children.Add(barBack);
                barGrid.Children.Add(fill);
                // fill width resolves once the bar has been measured (it starts at 0/NaN)
                barGrid.SizeChanged += (s, e) =>
                    fill.Width = barGrid.ActualWidth * doneToday / Math.Max(1, totalToday);
                stack.Children.Add(barGrid);
                var tally = Theme.MakeText(
                    string.Format(L.T("nt_progress"), doneToday, totalToday), 11, Theme.TextDim);
                tally.HorizontalAlignment = HorizontalAlignment.Center;
                tally.Margin = new Thickness(0, 0, 0, 6);
                stack.Children.Add(tally);
            }

            stack.Children.Add(new ScrollViewer
            {
                Content = listHost,
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            });

            var add = Theme.MakeButton(L.T("nt_add"), () => OpenEditor(host, null, rebuild), accent: true);
            add.HorizontalAlignment = HorizontalAlignment.Stretch;
            add.Margin = new Thickness(3, 10, 3, 0);
            stack.Children.Add(add);
            return stack;
        }

        /// <summary>One task row: a completion circle at the start, the text, its reminder in
        /// words, then postpone / pin / delete. The leading circle is the whole point of the
        /// layout — a to-do list is read as "things to tick off", and a checkbox says that
        /// without needing a label. Completing marks the task done (struck through, sunk to
        /// the bottom, restorable); only the trash button deletes for good.</summary>
        static FrameworkElement MakeRow(PanelWindow host, NoteItem note, List<NoteItem> all, Action rebuild)
        {
            var done = MakeDoneCircle(note.Done, () =>
            {
                note.Done = !note.Done;
                note.DoneAt = note.Done ? DateTime.Now : DateTime.MinValue;
                Save(all);
                rebuild();
            });

            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var body = Theme.MakeText(note.Preview(), 12.5, note.Done ? Theme.TextFaint : Theme.Text);
            body.TextWrapping = TextWrapping.Wrap;
            body.MaxHeight = 34;
            if (note.Done) body.TextDecorations = TextDecorations.Strikethrough;
            texts.Children.Add(body);

            if (note.HasReminder && !note.Done)
            {
                bool overdue = note.RemindAt <= DateTime.Now;
                var when = Theme.MakeText(DescribeWhen(note.RemindAt), 10.5,
                    overdue ? Theme.TextFaint : Theme.Accent);
                when.Margin = new Thickness(0, 3, 0, 0);
                texts.Children.Add(when);
            }

            var snooze = Theme.MakeIconButton("\uE823", L.T("nt_snooze"),
                () => OpenEditor(host, note, rebuild), 26, 11);
            var pin = Theme.MakeIconButton(note.Pinned ? "\uE77A" : "\uE718",
                L.T(note.Pinned ? "nt_unpin" : "nt_pin"), () =>
                {
                    note.Pinned = !note.Pinned;
                    Save(all);
                    rebuild();
                }, 26, 11);
            var del = Theme.MakeIconButton("\uE74D", L.T("nt_delete"), () =>
            {
                int idx = all.IndexOf(note);
                var backup = note;
                all.Remove(note);
                Save(all);
                rebuild();
                // deletion is the one destructive act left — make it refundable: the toast
                // offers Undo, which re-inserts the task where it was and redraws the panel
                Toast.Show(L.T("nt_deleted"), Glyphs.Note, L.T("nt_undo"), () =>
                {
                    var cur = Load();
                    cur.Insert(Math.Max(0, Math.Min(idx, cur.Count)), backup);
                    Save(cur);
                    RaiseChanged();
                });
            }, 26, 11);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btns.Children.Add(snooze);
            btns.Children.Add(pin);
            btns.Children.Add(del);

            var dock = new DockPanel();
            DockPanel.SetDock(done, Dock.Left);
            dock.Children.Add(done);
            DockPanel.SetDock(btns, Dock.Right);
            dock.Children.Add(btns);
            dock.Children.Add(texts);

            var row = new Border
            {
                Background = note.Pinned && !note.Done ? Theme.AccentSoft : Theme.ButtonBg,
                BorderBrush = note.Pinned && !note.Done ? Theme.Accent : Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(9, 7, 9, 7),
                Margin = new Thickness(0, 2.5, 0, 2.5),
                Cursor = Cursors.Hand,
                Child = dock,
                ToolTip = L.T("nt_edit"),
                Opacity = note.Done ? 0.62 : 1.0,
                SnapsToDevicePixels = true,
            };
            Theme.AttachHover(row, note.Pinned && !note.Done ? Theme.AccentSoft : Theme.ButtonBg, Theme.HoverBg);
            // Handle Down too — otherwise the panel window's DragMove captures the press and
            // the Up below never fires, which made clicking a task look like it did nothing
            row.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            row.MouseLeftButtonUp += (s, e) => { e.Handled = true; OpenEditor(host, note, rebuild); };
            return row;
        }

        /// <summary>Completion circle. Open task: empty ring that fills with a tick on
        /// hover — "click to complete". Done task: solid accent ring with the tick always
        /// showing — "click to restore". Toggling never deletes the task.</summary>
        static FrameworkElement MakeDoneCircle(bool isDone, Action onToggle)
        {
            var tick = Theme.MakeGlyph("\uE73E", 11,
                isDone ? (Theme.IsLight ? Brushes.White : Brushes.Black) : Theme.Accent);
            tick.Opacity = isDone ? 1 : 0;
            var circle = new Border
            {
                Width = 21,
                Height = 21,
                CornerRadius = new CornerRadius(11),
                Background = isDone ? Theme.Accent : Brushes.Transparent,
                BorderBrush = isDone ? Theme.Accent : Theme.TextFaint,
                BorderThickness = new Thickness(1.4),
                Child = tick,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 9, 0),
                ToolTip = L.T(isDone ? "nt_restore" : "nt_done"),
            };
            circle.MouseEnter += (s, e) => { tick.Opacity = 1; if (!isDone) circle.BorderBrush = Theme.Accent; };
            circle.MouseLeave += (s, e) => { if (!isDone) { tick.Opacity = 0; circle.BorderBrush = Theme.TextFaint; } };
            circle.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            circle.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;   // must not fall through to the row's "open editor"
                try { onToggle(); } catch (Exception ex) { Log.Error("note done", ex); }
            };
            return circle;
        }

        /// <summary>The reminder written out the way a person would say it.</summary>
        static string DescribeWhen(DateTime at)
        {
            var today = DateTime.Now.Date;
            string time = at.ToString("HH:mm", CultureInfo.InvariantCulture);
            int days = (int)(at.Date - today).TotalDays;
            if (days == 0) return L.T("nt_today") + " · " + time;
            if (days == 1) return L.T("nt_tomorrow") + " · " + time;
            return at.ToString("dddd d MMMM", Ci) + " · " + time;
        }

        // ---------------- editor ----------------

        /// <summary>Editor for one task. <paramref name="existing"/> null = a new one.</summary>
        static void OpenEditor(PanelWindow host, NoteItem existing, Action rebuild)
        {
            var all = Load();
            var note = existing;
            if (note == null) { note = new NoteItem(); all.Insert(0, note); }
            else
            {
                // Load() returns fresh objects, so edit the copy that is actually in the list
                var match = all.Find(n => n.Text == existing.Text && n.Pinned == existing.Pinned
                                          && n.Done == existing.Done
                                          && n.RemindAt == existing.RemindAt);
                if (match != null) note = match;
                else all.Insert(0, note);
            }

            PanelWindow.Toggle(MainWindow.Instance, "note_edit", L.T("nt_edit"), h =>
                BuildEditor(note, all, rebuild), 360);
        }

        /// <summary>Opens the editor on the first stored task. Exists so the UI smoke run can
        /// capture the editor, which is otherwise only reachable by clicking a row.</summary>
        public static void OpenEditorForSmoke()
        {
            var all = Load();
            // prefer one that already has a reminder, so the capture shows the date and time
            // controls rather than the collapsed "no reminder" state
            var note = all.Find(n => n.HasReminder) ?? (all.Count > 0 ? all[0] : null);
            if (note == null) { note = new NoteItem { Text = "" }; all.Add(note); }
            PanelWindow.Toggle(MainWindow.Instance, "note_edit", L.T("nt_edit"),
                h => BuildEditor(note, all, () => { }), 360);
        }

        static FrameworkElement BuildEditor(NoteItem note, List<NoteItem> all, Action rebuild)
        {
            var stack = new StackPanel();
            var today = DateTime.Now.Date;

            var box = Theme.MakeInput(note.Text);
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.MinHeight = 90;
            box.VerticalContentAlignment = VerticalAlignment.Top;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            stack.Children.Add(box);

            stack.Children.Add(Theme.SectionTitle(L.T("nt_remind")));

            // working state: the date being edited, or MinValue for "no reminder"
            DateTime chosen = note.HasReminder ? note.RemindAt : DateTime.MinValue;

            // ---- the sentence that says what will actually happen ----
            var summary = Theme.MakeText("", 12.5, Theme.Accent);
            summary.TextWrapping = TextWrapping.Wrap;
            summary.Margin = new Thickness(2, 8, 2, 2);

            // ---- date row: labelled, with a full written date ----
            var dateCombo = Theme.MakeCombo();
            dateCombo.MinWidth = 0;
            dateCombo.Width = 210;
            for (int d = 0; d < 60; d++)
            {
                var day = today.AddDays(d);
                dateCombo.Items.Add(d == 0 ? L.T("nt_today") + " · " + day.ToString("d MMMM", Ci)
                                  : d == 1 ? L.T("nt_tomorrow") + " · " + day.ToString("d MMMM", Ci)
                                           : day.ToString("dddd d MMMM", Ci));
            }

            var hourCombo = Theme.MakeCombo(); hourCombo.MinWidth = 0; hourCombo.Width = 68;
            var minCombo = Theme.MakeCombo(); minCombo.MinWidth = 0; minCombo.Width = 68;
            for (int hh = 0; hh < 24; hh++) hourCombo.Items.Add(hh.ToString("00", CultureInfo.InvariantCulture));
            for (int mm = 0; mm < 60; mm += 5) minCombo.Items.Add(mm.ToString("00", CultureInfo.InvariantCulture));

            var whenRows = new StackPanel();

            Action refresh = null;
            Action readControls = () =>
            {
                if (chosen == DateTime.MinValue) return;
                chosen = today.AddDays(Math.Max(0, dateCombo.SelectedIndex))
                              .AddHours(Math.Max(0, hourCombo.SelectedIndex))
                              .AddMinutes(Math.Max(0, minCombo.SelectedIndex) * 5);
            };
            Action<DateTime> setChosen = value =>
            {
                chosen = value;
                if (value != DateTime.MinValue)
                {
                    int idx = (int)(value.Date - today).TotalDays;
                    dateCombo.SelectedIndex = Math.Max(0, Math.Min(59, idx));
                    hourCombo.SelectedIndex = value.Hour;
                    minCombo.SelectedIndex = Math.Min(11, value.Minute / 5);
                }
                refresh();
            };

            dateCombo.SelectionChanged += (s, e) => { readControls(); refresh(); };
            hourCombo.SelectionChanged += (s, e) => { readControls(); refresh(); };
            minCombo.SelectionChanged += (s, e) => { readControls(); refresh(); };

            // ---- "when" mode chips ----
            var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            Border noneChip = null, todayChip = null, tomorrowChip = null, pickChip = null;

            Action<Border, bool> mark = (chip, on) =>
            {
                var tb = chip.Tag as TextBlock;
                if (tb != null) tb.Foreground = on ? Theme.Accent : Theme.TextDim;
                chip.BorderBrush = on ? Theme.Accent : Theme.ButtonBorder;
            };

            refresh = () =>
            {
                bool has = chosen != DateTime.MinValue;
                whenRows.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
                mark(noneChip, !has);
                mark(todayChip, has && chosen.Date == today);
                mark(tomorrowChip, has && chosen.Date == today.AddDays(1));
                mark(pickChip, has && chosen.Date > today.AddDays(1));
                summary.Text = has
                    ? string.Format(L.T("nt_will_alert"), DescribeWhen(chosen))
                    : L.T("nt_no_remind");
                summary.Foreground = has ? Theme.Accent : Theme.TextFaint;
            };

            noneChip = Theme.MakeButton(L.T("nt_no_remind"), () => setChosen(DateTime.MinValue),
                padding: new Thickness(11, 4, 11, 4));
            todayChip = Theme.MakeButton(L.T("nt_today"),
                () => setChosen(NextSensibleTime(today)), padding: new Thickness(11, 4, 11, 4));
            tomorrowChip = Theme.MakeButton(L.T("nt_tomorrow"),
                () => setChosen(today.AddDays(1).AddHours(9)), padding: new Thickness(11, 4, 11, 4));
            pickChip = Theme.MakeButton(L.T("nt_pick_date"),
                () => setChosen(today.AddDays(2).AddHours(9)), padding: new Thickness(11, 4, 11, 4));
            foreach (var c in new[] { noneChip, todayChip, tomorrowChip, pickChip }) modeRow.Children.Add(c);
            stack.Children.Add(modeRow);

            // ---- labelled date + time ----
            var dateRow = new DockPanel { Margin = new Thickness(0, 2, 0, 4) };
            var dateLbl = Theme.MakeText(L.T("nt_date"), 12, Theme.TextDim);
            dateLbl.VerticalAlignment = VerticalAlignment.Center;
            dateLbl.MinWidth = 54;
            dateRow.Children.Add(dateLbl);
            DockPanel.SetDock(dateCombo, Dock.Right);
            dateRow.Children.Add(dateCombo);
            whenRows.Children.Add(dateRow);

            var timeRow = new DockPanel { Margin = new Thickness(0, 2, 0, 4) };
            var timeLbl = Theme.MakeText(L.T("nt_time"), 12, Theme.TextDim);
            timeLbl.VerticalAlignment = VerticalAlignment.Center;
            timeLbl.MinWidth = 54;
            timeRow.Children.Add(timeLbl);
            // hour : minute, left-to-right and with a visible colon — two bare boxes side by
            // side gave no clue which was which or that they were a clock time at all
            var clock = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.LeftToRight,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var hLbl = Theme.MakeText(L.T("nt_hour"), 10.5, Theme.TextFaint);
            hLbl.HorizontalAlignment = HorizontalAlignment.Center;
            var mLbl = Theme.MakeText(L.T("nt_minute"), 10.5, Theme.TextFaint);
            mLbl.HorizontalAlignment = HorizontalAlignment.Center;
            var hCol = new StackPanel();
            hCol.Children.Add(hourCombo);
            hCol.Children.Add(hLbl);
            var mCol = new StackPanel();
            mCol.Children.Add(minCombo);
            mCol.Children.Add(mLbl);
            var colon = Theme.MakeText(":", 16, Theme.TextDim);
            colon.Margin = new Thickness(6, 0, 6, 0);
            colon.VerticalAlignment = VerticalAlignment.Top;
            colon.Padding = new Thickness(0, 4, 0, 0);
            clock.Children.Add(hCol);
            clock.Children.Add(colon);
            clock.Children.Add(mCol);
            DockPanel.SetDock(clock, Dock.Right);
            timeRow.Children.Add(clock);
            whenRows.Children.Add(timeRow);

            // ---- quick postpone ----
            var quickTitle = Theme.MakeText(L.T("nt_quick"), 11, Theme.TextFaint);
            quickTitle.Margin = new Thickness(2, 6, 2, 2);
            whenRows.Children.Add(quickTitle);
            var quickRow = new StackPanel { Orientation = Orientation.Horizontal };
            quickRow.Children.Add(Theme.MakeButton(L.T("nt_p10"),
                () => setChosen(DateTime.Now.AddMinutes(10)), padding: new Thickness(9, 3, 9, 3)));
            quickRow.Children.Add(Theme.MakeButton(L.T("nt_p1h"),
                () => setChosen(DateTime.Now.AddHours(1)), padding: new Thickness(9, 3, 9, 3)));
            quickRow.Children.Add(Theme.MakeButton(L.T("nt_p3h"),
                () => setChosen(DateTime.Now.AddHours(3)), padding: new Thickness(9, 3, 9, 3)));
            quickRow.Children.Add(Theme.MakeButton(L.T("nt_pmorning"),
                () => setChosen(today.AddDays(1).AddHours(9)), padding: new Thickness(9, 3, 9, 3)));
            whenRows.Children.Add(quickRow);
            stack.Children.Add(whenRows);
            stack.Children.Add(summary);

            setChosen(chosen);

            // ---- actions ----
            var feedback = Theme.MakeText("", 11.5, Theme.Accent);
            feedback.HorizontalAlignment = HorizontalAlignment.Center;
            feedback.TextWrapping = TextWrapping.Wrap;
            feedback.Margin = new Thickness(0, 6, 0, 0);

            var save = Theme.MakeButton(L.T("nt_save"), () =>
            {
                string text = box.Text.Trim();
                if (text.Length == 0)
                {
                    all.Remove(note);              // an emptied task is a delete, not an error
                    Save(all);
                    PanelWindow.CloseCurrent();
                    rebuild();
                    return;
                }
                if (chosen != DateTime.MinValue && chosen <= DateTime.Now)
                {
                    feedback.Text = L.T("nt_past");
                    return;
                }
                note.Text = text;
                note.RemindAt = chosen;
                Save(all);
                PanelWindow.CloseCurrent();
                rebuild();
            }, accent: true);
            save.HorizontalAlignment = HorizontalAlignment.Stretch;

            var doneBtn = Theme.MakeButton(L.T(note.Done ? "nt_restore" : "nt_done"), () =>
            {
                string text = box.Text.Trim();
                if (text.Length == 0)
                {
                    all.Remove(note);          // an emptied task is a delete, not an error
                }
                else
                {
                    note.Text = text;
                    note.Done = !note.Done;
                    note.DoneAt = note.Done ? DateTime.Now : DateTime.MinValue;
                    if (note.Done) note.RemindAt = DateTime.MinValue;   // a finished task stops nagging
                    else note.RemindAt = chosen != DateTime.MinValue && chosen <= DateTime.Now
                        ? DateTime.MinValue : chosen;                   // no past reminders on restore
                }
                Save(all);
                PanelWindow.CloseCurrent();
                rebuild();
            });
            doneBtn.HorizontalAlignment = HorizontalAlignment.Stretch;

            var actions = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 2,
                Margin = new Thickness(0, 12, 0, 0),
            };
            actions.Children.Add(save);
            actions.Children.Add(doneBtn);
            stack.Children.Add(actions);
            stack.Children.Add(feedback);
            return stack;
        }

        /// <summary>A sensible default time when "today" is chosen: the next whole hour, or
        /// half past if that is already close. Defaulting to 09:00 today would be in the past
        /// for most of the day and immediately rejected as invalid.</summary>
        static DateTime NextSensibleTime(DateTime today)
        {
            var now = DateTime.Now;
            var t = now.AddMinutes(30);
            t = t.AddMinutes(-(t.Minute % 5)).AddSeconds(-t.Second).AddMilliseconds(-t.Millisecond);
            return t.Date == today ? t : today.AddHours(23).AddMinutes(55);
        }
    }
}
