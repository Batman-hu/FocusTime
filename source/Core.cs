using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace FocusTime
{
    [DataContract]
    public class FocusProject
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Color { get; set; }
        [DataMember] public bool Archived { get; set; }
        public override string ToString() { return Name; }
    }

    [DataContract]
    public class DaySlice
    {
        [DataMember] public string Day { get; set; }
        [DataMember] public double Seconds { get; set; }
    }

    [DataContract]
    public class FocusSession
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string ProjectId { get; set; }
        [DataMember] public string ProjectName { get; set; }
        [DataMember] public string Mode { get; set; }
        [DataMember] public DateTime StartedUtc { get; set; }
        [DataMember(EmitDefaultValue = false)] public DateTime? EndedUtc { get; set; }
        [DataMember] public double TargetSeconds { get; set; }
        [DataMember] public double Seconds { get; set; }
        [DataMember] public bool Completed { get; set; }
        [DataMember] public List<DaySlice> Days { get; set; }
    }

    [DataContract]
    public class AppData
    {
        [DataMember] public int Version { get; set; }
        [DataMember] public int DailyGoalMinutes { get; set; }
        [DataMember] public int CountdownMinutes { get; set; }
        [DataMember] public string SelectedProjectId { get; set; }
        [DataMember] public string SelectedMode { get; set; }
        [DataMember] public bool Sound { get; set; }
        [DataMember] public List<FocusProject> Projects { get; set; }
        [DataMember] public List<FocusSession> Sessions { get; set; }
        [DataMember] public FocusSession Active { get; set; }

        public static AppData Create()
        {
            var p = new FocusProject { Id = Guid.NewGuid().ToString("N"), Name = "日常学习", Color = "#436B80" };
            return new AppData { Version = 1, DailyGoalMinutes = 120, CountdownMinutes = 25,
                Sound = true, SelectedMode = "down", SelectedProjectId = p.Id,
                Projects = new List<FocusProject> { p }, Sessions = new List<FocusSession>() };
        }

        public void Validate()
        {
            if (Version != 1 || Projects == null || Sessions == null)
                throw new InvalidDataException("数据格式或版本不受支持。");
            if (DailyGoalMinutes < 1 || DailyGoalMinutes > 1440 || CountdownMinutes < 1 || CountdownMinutes > 1440)
                throw new InvalidDataException("计时设置无效。");
            if (Projects.Any(p => p == null || String.IsNullOrWhiteSpace(p.Id) || String.IsNullOrWhiteSpace(p.Name)) ||
                Projects.Select(p => p.Id).Distinct().Count() != Projects.Count)
                throw new InvalidDataException("项目数据无效。");
            foreach (var s in Sessions.Concat(Active == null ? new FocusSession[0] : new[] { Active }))
            {
                if (s == null || String.IsNullOrEmpty(s.Id) || s.Days == null || s.Seconds < 0 ||
                    Double.IsNaN(s.Seconds) || Double.IsInfinity(s.Seconds) ||
                    (s.Mode != "up" && s.Mode != "down") || !Projects.Any(p => p.Id == s.ProjectId))
                    throw new InvalidDataException("计时记录无效。");
                if (s.Mode == "down" && (s.TargetSeconds < 1 || s.TargetSeconds > 86400 || s.Seconds > s.TargetSeconds + .01))
                    throw new InvalidDataException("倒计时时长无效。");
                foreach (var d in s.Days)
                {
                    DateTime day;
                    if (d == null || !DateTime.TryParseExact(d.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out day) || d.Seconds < 0 || Double.IsNaN(d.Seconds) || Double.IsInfinity(d.Seconds))
                        throw new InvalidDataException("每日记录无效。");
                }
                if (Math.Abs(s.Days.Sum(d => d.Seconds) - s.Seconds) > .05)
                    throw new InvalidDataException("累计时长与每日记录不一致。");
            }
            if (Sessions.Select(s => s.Id).Distinct().Count() != Sessions.Count ||
                (Active != null && Sessions.Any(s => s.Id == Active.Id)))
                throw new InvalidDataException("发现重复记录。");
        }
    }

    public sealed class DataStore
    {
        public const string DefaultDirectory = @"D:\FocusTime\Data";
        public const string TemporaryDirectory = @"D:\FocusTime\Temp";
        public readonly string DirectoryPath;
        public string FilePath { get { return Path.Combine(DirectoryPath, "data.json"); } }
        public bool Recovered { get; private set; }
        public DataStore(string directory) { DirectoryPath = directory; }

        public AppData Load()
        {
            Directory.CreateDirectory(DirectoryPath);
            if (!File.Exists(FilePath))
            {
                if (File.Exists(FilePath + ".bak")) { Recovered = true; return Read(FilePath + ".bak"); }
                return AppData.Create();
            }
            try { return Read(FilePath); }
            catch (Exception primaryError)
            {
                if (!File.Exists(FilePath + ".bak"))
                    throw new InvalidDataException("无法读取记录，原文件已保留：" + FilePath, primaryError);
                var backup = Read(FilePath + ".bak");
                File.Copy(FilePath, FilePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
                File.Copy(FilePath + ".bak", FilePath, true);
                Recovered = true;
                return backup;
            }
        }

        public static AppData Read(string path)
        {
            using (var file = File.OpenRead(path))
            {
                var data = (AppData)new DataContractJsonSerializer(typeof(AppData)).ReadObject(file);
                if (data == null) throw new InvalidDataException("数据为空。");
                data.Validate();
                return data;
            }
        }

        public void Save(AppData data)
        {
            data.Validate();
            Directory.CreateDirectory(DirectoryPath);
            string temp = FilePath + ".tmp";
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                new DataContractJsonSerializer(typeof(AppData)).WriteObject(file, data);
                file.Flush(true);
            }
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
            else File.Move(temp, FilePath);
        }
    }

    public enum TickOutcome { Normal, Completed, Interrupted }

    public sealed class FocusEngine
    {
        public AppData Data { get; private set; }
        public bool Running { get; private set; }
        private double lastMonotonic;
        private DateTime lastUtc;
        public FocusSession Active { get { return Data.Active; } }
        public double Remaining { get { return Active == null ? 0 : Math.Max(0, Active.TargetSeconds - Active.Seconds); } }
        public FocusEngine(AppData data) { Data = data; Running = false; }

        public void Start(FocusProject project, string mode, int minutes, DateTime utc, double monotonic)
        {
            if (Active != null) throw new InvalidOperationException("请先结束当前专注。");
            if (project == null || project.Archived || !Data.Projects.Contains(project)) throw new ArgumentException("请选择有效项目。");
            if ((mode != "up" && mode != "down") || minutes < 1 || minutes > 1440) throw new ArgumentException("计时设置无效。");
            Data.Active = new FocusSession { Id = Guid.NewGuid().ToString("N"), ProjectId = project.Id,
                ProjectName = project.Name, Mode = mode, StartedUtc = utc, TargetSeconds = mode == "down" ? minutes * 60 : 0,
                Days = new List<DaySlice>() };
            Resume(utc, monotonic);
        }

        public void Resume(DateTime utc, double monotonic)
        {
            if (Active == null) return;
            if (Active.Mode == "down" && Remaining <= 0) return;
            lastUtc = utc; lastMonotonic = monotonic; Running = true;
        }

        public TickOutcome Tick(DateTime utc, double monotonic)
        {
            if (!Running || Active == null) return TickOutcome.Normal;
            double delta = monotonic - lastMonotonic;
            double wallDelta = (utc - lastUtc).TotalSeconds;
            var intervalStart = lastUtc;
            lastMonotonic = monotonic; lastUtc = utc;
            // Large callback gaps are treated as suspension, never credited as focus.
            if (delta < 0 || delta > 10 || Math.Abs(wallDelta - delta) > 2)
            { Running = false; return TickOutcome.Interrupted; }
            double credit = Active.Mode == "down" ? Math.Min(delta, Remaining) : delta;
            AddTime(Active, intervalStart, credit);
            if (Active.Mode == "down" && Remaining < .000001)
            { Running = false; return TickOutcome.Completed; }
            return TickOutcome.Normal;
        }

        public TickOutcome Pause(DateTime utc, double monotonic)
        {
            var result = Tick(utc, monotonic); Running = false; return result;
        }

        public FocusSession Finish(DateTime utc, double monotonic)
        {
            Tick(utc, monotonic); Running = false;
            var session = Active;
            if (session == null) return null;
            session.EndedUtc = utc;
            session.Completed = session.Mode == "down" && Remaining < .000001;
            Data.Active = null;
            if (session.Seconds > .000001) Data.Sessions.Add(session);
            return session;
        }

        private static void AddTime(FocusSession session, DateTime utcStart, double seconds)
        {
            double remaining = seconds;
            var cursor = DateTime.SpecifyKind(utcStart, DateTimeKind.Utc);
            while (remaining > .0000001)
            {
                var local = cursor.ToLocalTime();
                string key = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var midnightUtc = local.Date.AddDays(1).ToUniversalTime();
                double part = Math.Min(remaining, (midnightUtc - cursor).TotalSeconds);
                if (part <= 0) part = remaining;
                var slice = session.Days.FirstOrDefault(d => d.Day == key);
                if (slice == null) { slice = new DaySlice { Day = key }; session.Days.Add(slice); }
                slice.Seconds += part;
                session.Seconds += part;
                remaining -= part;
                cursor = cursor.AddSeconds(part);
            }
        }
    }

    public static class Stats
    {
        public static IEnumerable<FocusSession> All(AppData data)
        { return data.Sessions.Concat(data.Active == null ? new FocusSession[0] : new[] { data.Active }); }
        public static double Total(AppData data, string projectId, DateTime? start, DateTime? end)
        {
            string a = start.HasValue ? start.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            string b = end.HasValue ? end.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            return All(data).Where(s => projectId == null || s.ProjectId == projectId).SelectMany(s => s.Days)
                .Where(d => (a == null || String.CompareOrdinal(d.Day, a) >= 0) && (b == null || String.CompareOrdinal(d.Day, b) <= 0))
                .Sum(d => d.Seconds);
        }
        public static int Streak(AppData data, DateTime today)
        {
            var day = today.Date;
            if (Total(data, null, day, day) < data.DailyGoalMinutes * 60) day = day.AddDays(-1);
            int count = 0;
            while (Total(data, null, day, day) >= data.DailyGoalMinutes * 60) { count++; day = day.AddDays(-1); }
            return count;
        }
        public static string Duration(double seconds)
        {
            long n = (long)Math.Floor(Math.Max(0, seconds) + .000001);
            if (n < 60) return n + " 秒";
            if (n < 3600) return (n / 60) + " 分 " + (n % 60) + " 秒";
            return (n / 3600) + " 小时 " + ((n / 60) % 60) + " 分";
        }
        public static string Clock(double seconds, bool ceiling)
        {
            long n = (long)(ceiling ? Math.Ceiling(Math.Max(0, seconds) - .000001) : Math.Floor(Math.Max(0, seconds) + .000001));
            return n >= 3600 ? String.Format("{0:00}:{1:00}:{2:00}", n / 3600, n / 60 % 60, n % 60)
                : String.Format("{0:00}:{1:00}", n / 60, n % 60);
        }
        private static string CsvCell(string text) { return "\"" + (text ?? "").Replace("\"", "\"\"") + "\""; }
        public static void ExportCsv(AppData data, string path)
        {
            using (var file = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                file.WriteLine("日期,项目,模式,专注秒数,专注分钟,开始时间,结束时间,状态");
                foreach (var s in All(data).OrderByDescending(s => s.StartedUtc))
                {
                    var p = data.Projects.FirstOrDefault(x => x.Id == s.ProjectId);
                    string name = p == null ? s.ProjectName : p.Name;
                    // Prevent spreadsheet formula interpretation of user-entered names.
                    if (name.Length > 0 && "=+-@\t\r\n".IndexOf(name[0]) >= 0) name = "'" + name;
                    foreach (var d in s.Days)
                        file.WriteLine(String.Join(",", new[] { CsvCell(d.Day), CsvCell(name), CsvCell(s.Mode == "down" ? "倒计时" : "正计时"),
                            d.Seconds.ToString("0.000", CultureInfo.InvariantCulture), (d.Seconds / 60).ToString("0.000", CultureInfo.InvariantCulture),
                            CsvCell(s.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                            CsvCell(s.EndedUtc.HasValue ? s.EndedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : ""),
                            CsvCell(s == data.Active ? "未结束" : s.Completed ? "倒计时完成" : "手动结束") }));
                }
            }
        }
    }
}
