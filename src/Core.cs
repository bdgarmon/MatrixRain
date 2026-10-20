using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MatrixRainLab {
    public sealed class Settings {
        public string LabTitle { get; set; }
        public string NodeLabel { get; set; }
        public string ExperimentLabel { get; set; }
        public string WorkloadLabel { get; set; }
        public string WatchProcess { get; set; }
        public string StateFile { get; set; }
        public int FramesPerSecond { get; set; }
        public int SampleSeconds { get; set; }
        public int Monitor { get; set; }
        public bool Fullscreen { get; set; }
        public bool RainEnabled { get; set; }
        public bool KeepAwake { get; set; }
        public bool EnableCsv { get; set; }
        public bool EnableGpu { get; set; }
        public double RainSpeed { get; set; }
        public double RainSpacing { get; set; }
        public Settings() {
            LabTitle = "AFTER SIX LAB"; NodeLabel = "AUTO";
            ExperimentLabel = "DEX EXPERIENCE LAB"; WorkloadLabel = "MATRIX DISPLAY";
            WatchProcess = ""; StateFile = "state.json";
            FramesPerSecond = 15; SampleSeconds = 5; Monitor = 0;
            Fullscreen = true; RainEnabled = true; KeepAwake = true; EnableGpu = true;
            RainSpeed=2.0; RainSpacing=1.25;
        }
        public static void ValidateKeys(IEnumerable<string> names) {
            var allowed = new HashSet<string>();
            foreach (var property in typeof(Settings).GetProperties()) allowed.Add(property.Name);
            foreach (string name in names) if (!allowed.Contains(name)) throw new ArgumentException("Unknown config setting: " + name);
        }
        public void Validate() {
            CheckText(LabTitle, "LabTitle", 48); CheckText(NodeLabel, "NodeLabel", 40);
            CheckText(ExperimentLabel, "ExperimentLabel", 70); CheckText(WorkloadLabel, "WorkloadLabel", 70);
            if (FramesPerSecond < 1 || FramesPerSecond > 30) throw new ArgumentException("FramesPerSecond must be 1..30.");
            if (SampleSeconds < 1 || SampleSeconds > 60) throw new ArgumentException("SampleSeconds must be 1..60.");
            if (Monitor < 0 || Monitor > 15) throw new ArgumentException("Monitor must be 0..15.");
            if(Double.IsNaN(RainSpeed) || Double.IsInfinity(RainSpeed) || RainSpeed<0.25 || RainSpeed>6) throw new ArgumentException("RainSpeed must be 0.25..6.");
            if(Double.IsNaN(RainSpacing) || Double.IsInfinity(RainSpacing) || RainSpacing<0.75 || RainSpacing>4) throw new ArgumentException("RainSpacing must be 0.75..4.");
            if (!String.IsNullOrEmpty(WatchProcess) && !Regex.IsMatch(WatchProcess, @"^[\w .-]{1,100}$"))
                throw new ArgumentException("WatchProcess must be a process name, without a path.");
            if (StateFile == null) StateFile = "";
        }
        public static void CheckText(string value, string name, int max) {
            if (String.IsNullOrWhiteSpace(value) || value.Length > max || Regex.IsMatch(value, @"[\x00-\x1f]"))
                throw new ArgumentException(name + " must be 1.." + max + " characters without control characters.");
        }
    }
    public sealed class WorkloadState {
        public string State { get; set; }
        public string ExperimentStartedUtc { get; set; }
        public string WorkloadLabel { get; set; }
        public string Detail { get; set; }
        public string HeartbeatUtc { get; set; }
        public string RunId { get; set; }
        public bool? RainEnabled { get; set; }
        public bool IsStale(DateTime now) {
            if (State != "RUNNING" || String.IsNullOrEmpty(HeartbeatUtc)) return false;
            DateTimeOffset parsed;
            if (!DateTimeOffset.TryParse(HeartbeatUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed)) return true;
            return (now - parsed.UtcDateTime).TotalSeconds > 20;
        }
        public DateTime? StartUtc() {
            if (String.IsNullOrEmpty(ExperimentStartedUtc)) return null;
            DateTimeOffset result;
            if (!Regex.IsMatch(ExperimentStartedUtc, @"(Z|[+-]00:00)$") ||
                !DateTimeOffset.TryParse(ExperimentStartedUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                throw new ArgumentException("ExperimentStartedUtc must be an ISO 8601 UTC timestamp ending in Z or +00:00.");
            if (result.UtcDateTime > DateTime.UtcNow.AddSeconds(5)) throw new ArgumentException("Experiment start is in the future.");
            return result.UtcDateTime;
        }
        public void Validate() {
            if (Array.IndexOf(new [] { "IDLE", "RUNNING", "PAUSED", "COMPLETED", "FAILED" }, State) < 0)
                throw new ArgumentException("State must be IDLE, RUNNING, PAUSED, COMPLETED or FAILED.");
            if (!String.IsNullOrEmpty(WorkloadLabel)) Settings.CheckText(WorkloadLabel, "WorkloadLabel", 70);
            StartUtc();
        }
    }
    public sealed class Sample {
        public DateTime Utc = DateTime.UtcNow;
        public double? Cpu, RamUsed, RamTotal, Gpu, Dedicated, Shared, AppCpu, AppRam;
        public double UptimeSeconds;
        public bool RainEnabled;
        public int FrameCap;
        public string RunId;
        public bool? RainOverride;
        public string State = "SAMPLING", StateSource = "display", Workload = "", Warning = "";
        public DateTime ExperimentStart;
    }
    public static class Metrics {
        public static string ProcessName(string value) {
            return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value.Substring(0, value.Length - 4) : value;
        }
        public static double Clamp(double value) { return Math.Max(0, Math.Min(100, value)); }
        public static double? CpuPercent(ulong oldIdle, ulong oldKernel, ulong oldUser, ulong idle, ulong kernel, ulong user) {
            if (idle < oldIdle || kernel < oldKernel || user < oldUser) return null;
            double total = (kernel - oldKernel) + (double)(user - oldUser);
            return total <= 0 ? (double?)null : Clamp(100 * (total - (idle - oldIdle)) / total);
        }
        // Windows reports one instance per process/physical engine. Sum each engine, then choose the busiest.
        public static double? BusiestEngine(IDictionary<string, double> counters) {
            var engines = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in counters) {
                int index = kv.Key.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
                if (index < 0 || Double.IsNaN(kv.Value) || Double.IsInfinity(kv.Value) || kv.Value < 0) continue;
                string key = Regex.Replace(kv.Key.Substring(index), @"#\d+$", "");
                double existing; engines.TryGetValue(key, out existing); engines[key] = existing + kv.Value;
            }
            if (engines.Count == 0) return null;
            double maximum = 0; foreach (double v in engines.Values) maximum = Math.Max(maximum, v);
            return Clamp(maximum);
        }
        public static string Duration(double seconds) {
            var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ((long)t.TotalHours).ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
        }
        public static string Number(double? value, string format) {
            return value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) : "N/A";
        }
        public static string Csv(string value) { return "\"" + (value ?? "").Replace("\"", "\"\"") + "\""; }
    }
}
