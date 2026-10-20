using System;
using System.IO;
using System.Linq;

namespace FocusTime
{
    internal static class Tests
    {
        private static int count;
        private static void Check(bool ok, string name)
        { if (!ok) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
        private static void Near(double a, double b, string name) { Check(Math.Abs(a - b) < .001, name); }
        private static FocusEngine New() { return new FocusEngine(AppData.Create()); }
        public static int Main()
        {
            string temp = Path.Combine(DataStore.TemporaryDirectory, "FocusTime-Tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                DateTime t = new DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Local).ToUniversalTime();
                var e = New();
                e.Start(e.Data.Projects[0], "up", 25, t, 0);
                e.Tick(t.AddSeconds(3.5), 3.5); Near(e.Active.Seconds, 3.5, "count up uses elapsed time");
                e.Pause(t.AddSeconds(5), 5); e.Tick(t.AddSeconds(95), 95); Near(e.Active.Seconds, 5, "paused time excluded");
                e.Resume(t.AddSeconds(95), 95); e.Tick(t.AddSeconds(99), 99); Near(e.Active.Seconds, 9, "resume does not add paused time");
                var s = e.Finish(t.AddSeconds(100), 100); Near(s.Seconds, 10, "manual finish includes final fraction");
                Check(e.Data.Sessions.Count == 1 && e.Active == null && !e.Running, "finish persists once");
                e.Finish(t.AddSeconds(102), 102); Check(e.Data.Sessions.Count == 1, "duplicate finish is harmless");
                e = New(); e.Start(e.Data.Projects[0], "down", 1, t, 0);
                for (int i = 1; i <= 9; i++) e.Tick(t.AddSeconds(i * 6.5), i * 6.5);
                Check(e.Tick(t.AddSeconds(65), 65) == TickOutcome.Completed, "countdown completes");
                Near(e.Active.Seconds, 60, "countdown capped at target"); Near(e.Remaining, 0, "no negative remaining time");
                s = e.Finish(t.AddSeconds(66), 66); Check(s.Completed && e.Data.Sessions.Count == 1, "countdown record complete");
                e = New(); e.Start(e.Data.Projects[0], "up", 25, t, 0); e.Tick(t.AddSeconds(2), 2);
                Check(e.Tick(t.AddHours(2), 7200) == TickOutcome.Interrupted && !e.Running, "sleep gap auto pauses");
                Near(e.Active.Seconds, 2, "sleep time excluded");
                e.Resume(t.AddHours(2), 7200); Check(e.Tick(t.AddHours(3), 7201) == TickOutcome.Interrupted, "clock change safely pauses");
                e = New(); var midnight = new DateTime(2026, 10, 19, 23, 59, 58, DateTimeKind.Local).ToUniversalTime();
                e.Start(e.Data.Projects[0], "up", 25, midnight, 0); e.Tick(midnight.AddSeconds(5), 5);
                Check(e.Active.Days.Count == 2, "midnight split creates two dates");
                Near(e.Active.Days[0].Seconds, 2, "before midnight split"); Near(e.Active.Days[1].Seconds, 3, "after midnight split");
                Near(Stats.Total(e.Data, null, new DateTime(2026,10,19), new DateTime(2026,10,19)), 2, "daily totals include active session");
                var store = new DataStore(temp); store.Save(e.Data); var restored = new FocusEngine(store.Load());
                Check(!restored.Active.EndedUtc.HasValue, "unfinished session has no end timestamp");
                Check(!restored.Running && restored.Active != null, "restart restores paused draft"); Near(restored.Active.Seconds, 5, "draft seconds preserved");
                restored.Resume(midnight.AddSeconds(100), 100); restored.Tick(midnight.AddSeconds(102), 102);
                Near(restored.Active.Seconds, 7, "restart downtime not counted"); store.Save(restored.Data);
                Check(File.Exists(store.FilePath + ".bak"), "atomic save keeps backup");
                File.WriteAllText(store.FilePath, "broken-json"); var recovered = store.Load();
                Check(store.Recovered, "backup recovery signaled"); Near(recovered.Active.Seconds, 5, "backup restored");
                Check(Directory.GetFiles(temp, "*.corrupt-*").Length == 1, "corrupt data preserved");
                recovered.Projects[0].Name = "=example,\"项目\""; string csv = Path.Combine(temp, "export.csv"); Stats.ExportCsv(recovered, csv);
                var export = File.ReadAllText(csv); Check(export.Contains("'=example,\"\"项目\"\""), "CSV escapes names and formulas");
                Check(Stats.Clock(59.1, true) == "01:00" && Stats.Clock(59.1, false) == "00:59", "display rounding by mode");
                Check(Stats.Clock(3601, false) == "01:00:01", "long sessions display hours");
                e = New(); e.Start(e.Data.Projects[0], "up", 1, t, 0); e.Finish(t, 0);
                Check(e.Data.Sessions.Count == 0, "zero duration not recorded");
                e.Data.Projects[0].Archived = true; bool rejected = false;
                try { e.Start(e.Data.Projects[0], "up", 1, t, 0); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "archived project cannot start");
                e = New(); e.Start(e.Data.Projects[0], "up", 1, t, 0); e.Finish(t.AddSeconds(4), 4);
                var completedStore = new DataStore(Path.Combine(temp, "completed")); completedStore.Save(e.Data);
                var loaded = completedStore.Load();
                Check(loaded.Sessions[0].EndedUtc.HasValue && loaded.Sessions[0].EndedUtc.Value == t.AddSeconds(4), "finished end timestamp round-trips");
                loaded.Projects[0].Archived = true;
                Near(Stats.Total(loaded, null, null, null), 4, "archiving retains historical totals");
                completedStore.Save(loaded);
                File.WriteAllText(completedStore.FilePath, "bad"); File.WriteAllText(completedStore.FilePath + ".bak", "also-bad");
                bool blocked = false;
                try { completedStore.Load(); } catch { blocked = true; }
                Check(blocked && File.ReadAllText(completedStore.FilePath) == "bad", "two corrupt files never reset user data");
                e = New();
                var first = e.Data.Projects[0];
                var second = new FocusProject { Id = Guid.NewGuid().ToString("N"), Name = "保留项目", Color = "#F5A025" };
                e.Data.Projects.Add(second);
                e.Start(first, "up", 1, t, 0); e.Finish(t.AddSeconds(4), 4);
                e.Start(second, "up", 1, t.AddSeconds(10), 10); e.Finish(t.AddSeconds(15), 15);
                var deletionStore = new DataStore(Path.Combine(temp, "deletion")); deletionStore.Save(e.Data);
                string backup = deletionStore.BackupBeforeProjectDeletion(e.Data);
                e.Data.DeleteProject(first.Id); e.Data.Validate();
                Check(e.Data.Projects.Count == 1 && e.Data.Sessions.Count == 1 && e.Data.Sessions[0].ProjectId == second.Id, "delete removes only selected project and its records");
                Check(e.Data.SelectedProjectId == second.Id, "delete selected project selects another available project");
                Near(Stats.Total(e.Data, null, null, null), 5, "remaining project totals preserved after deletion");
                deletionStore.Save(e.Data);
                Check(DataStore.Read(backup).Projects.Count == 2 && DataStore.Read(backup).Sessions.Count == 2, "independent deletion backup retains all original records");
                Check(deletionStore.Load().Sessions.Count == 1, "deleted records remain deleted after reload");
                e.Start(second, "up", 1, t, 0); e.Pause(t.AddSeconds(1), 1);
                bool activeBlocked = false;
                try { e.Data.DeleteProject(second.Id); } catch (InvalidOperationException) { activeBlocked = true; }
                Check(activeBlocked && e.Data.Projects.Count == 1 && e.Active != null, "unfinished session blocks project deletion even while paused");
                e.Finish(t.AddSeconds(2), 2); e.Data.DeleteProject(second.Id); e.Data.Validate();
                Check(e.Data.Projects.Count == 0 && e.Data.Sessions.Count == 0 && e.Data.SelectedProjectId == null, "deleting last project leaves a valid empty state");
                deletionStore.Save(e.Data); Check(deletionStore.Load().Projects.Count == 0, "empty project list saves and reloads");
                Console.WriteLine("All " + count + " checks passed."); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
            finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
        }
    }
}
