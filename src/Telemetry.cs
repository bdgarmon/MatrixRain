using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace MatrixRainLab {
    internal static class Native {
        [DllImport("kernel32.dll")] internal static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
        [DllImport("kernel32.dll")] internal static extern ulong GetTickCount64();
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool GlobalMemoryStatusEx(ref Memory status);
        [DllImport("kernel32.dll")] internal static extern uint SetThreadExecutionState(uint flags);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [StructLayout(LayoutKind.Sequential)] internal struct Memory {
            public uint Length, Load;
            public ulong TotalPhys, AvailPhys, TotalPage, AvailPage, TotalVirtual, AvailVirtual, AvailExtended;
        }
    }
    internal sealed class GpuCounters : IDisposable {
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhOpenQuery(string source, IntPtr data, out IntPtr query);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr data, out IntPtr counter);
        [DllImport("pdh.dll")] static extern uint PdhCollectQueryData(IntPtr query);
        [DllImport("pdh.dll")] static extern uint PdhCloseQuery(IntPtr query);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)] static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bytes, out uint count, IntPtr buffer);
        [StructLayout(LayoutKind.Explicit, Size = 16)] struct Value {
            [FieldOffset(0)] public uint Status;
            [FieldOffset(8)] public double Number;
        }
        [StructLayout(LayoutKind.Sequential)] struct Item { public IntPtr Name; public Value Value; }
        IntPtr query, engine, dedicated, shared;
        DateTime retryAt;
        public GpuCounters() { Open(); }
        void Open() {
            Dispose(); retryAt = DateTime.UtcNow.AddMinutes(1);
            if (PdhOpenQuery(null, IntPtr.Zero, out query) != 0) { query = IntPtr.Zero; return; }
            Add(@"\GPU Engine(*)\Utilization Percentage", out engine);
            Add(@"\GPU Adapter Memory(*)\Dedicated Usage", out dedicated);
            Add(@"\GPU Adapter Memory(*)\Shared Usage", out shared);
            PdhCollectQueryData(query);
        }
        void Add(string path, out IntPtr handle) {
            if (PdhAddEnglishCounter(query, path, IntPtr.Zero, out handle) != 0) handle = IntPtr.Zero;
        }
        static Dictionary<string, double> Read(IntPtr counter) {
            var result = new Dictionary<string, double>();
            if (counter == IntPtr.Zero) return result;
            uint bytes = 0, count;
            uint status = PdhGetFormattedCounterArray(counter, 0x200 | 0x8000, ref bytes, out count, IntPtr.Zero);
            if (status != 0x800007D2 || bytes == 0 || bytes > 32 * 1024 * 1024) return result; // PDH_MORE_DATA
            IntPtr memory = Marshal.AllocHGlobal((int)bytes);
            try {
                if (PdhGetFormattedCounterArray(counter, 0x200 | 0x8000, ref bytes, out count, memory) != 0) return result;
                int stride = Marshal.SizeOf(typeof(Item));
                if ((long)count * stride > bytes) return result;
                for (int i = 0; i < count; i++) {
                    var item = (Item)Marshal.PtrToStructure(IntPtr.Add(memory, i * stride), typeof(Item));
                    if (item.Value.Status > 1 || Double.IsNaN(item.Value.Number) || Double.IsInfinity(item.Value.Number)) continue;
                    string name = Marshal.PtrToStringUni(item.Name);
                    if (!String.IsNullOrEmpty(name) && name != "_Total") result[name] = item.Value.Number;
                }
            } finally { Marshal.FreeHGlobal(memory); }
            return result;
        }
        static double? Sum(Dictionary<string, double> data) {
            if (data.Count == 0) return null;
            double value = 0; foreach (double v in data.Values) value += Math.Max(0, v);
            return value / 1073741824.0;
        }
        public void Fill(Sample sample) {
            if ((query == IntPtr.Zero || engine == IntPtr.Zero) && DateTime.UtcNow >= retryAt) Open();
            if (query == IntPtr.Zero || PdhCollectQueryData(query) != 0) return;
            sample.Gpu = Metrics.BusiestEngine(Read(engine));
            sample.Dedicated = Sum(Read(dedicated)); sample.Shared = Sum(Read(shared));
        }
        public void Dispose() {
            if (query != IntPtr.Zero) PdhCloseQuery(query);
            query = engine = dedicated = shared = IntPtr.Zero;
        }
    }
    internal sealed class Telemetry : IDisposable {
        readonly Settings settings;
        readonly string statePath;
        readonly DateTime launch = DateTime.UtcNow;
        readonly Stopwatch interval = Stopwatch.StartNew();
        readonly Process self = Process.GetCurrentProcess();
        readonly GpuCounters gpu;
        ulong oldIdle, oldKernel, oldUser;
        bool primed;
        double oldAppMs, oldWall;
        public Telemetry(Settings value, string configDirectory) {
            settings = value;
            statePath = String.IsNullOrEmpty(value.StateFile) ? "" : Path.GetFullPath(Path.Combine(configDirectory, value.StateFile));
            if (settings.EnableGpu) gpu = new GpuCounters();
            primed = Native.GetSystemTimes(out oldIdle, out oldKernel, out oldUser);
            oldAppMs = self.TotalProcessorTime.TotalMilliseconds; oldWall = interval.Elapsed.TotalMilliseconds;
        }
        public Sample Collect() {
            var s = new Sample { ExperimentStart = launch, Workload = settings.WorkloadLabel };
            ulong idle, kernel, user;
            if (Native.GetSystemTimes(out idle, out kernel, out user)) {
                if (primed) s.Cpu = Metrics.CpuPercent(oldIdle, oldKernel, oldUser, idle, kernel, user);
                oldIdle = idle; oldKernel = kernel; oldUser = user; primed = true;
            } else { primed = false; s.Warning += "CPU unavailable. "; }
            var memory = new Native.Memory { Length = (uint)Marshal.SizeOf(typeof(Native.Memory)) };
            if (Native.GlobalMemoryStatusEx(ref memory)) {
                s.RamTotal = memory.TotalPhys / 1073741824.0;
                s.RamUsed = (memory.TotalPhys - memory.AvailPhys) / 1073741824.0;
            }
            s.UptimeSeconds = Native.GetTickCount64() / 1000.0;
            self.Refresh(); double app = self.TotalProcessorTime.TotalMilliseconds, wall = interval.Elapsed.TotalMilliseconds;
            if (wall > oldWall) s.AppCpu = Metrics.Clamp(100 * (app - oldAppMs) / (wall - oldWall) / Environment.ProcessorCount);
            oldAppMs = app; oldWall = wall; s.AppRam = self.WorkingSet64 / 1048576.0;
            if (gpu != null) { try { gpu.Fill(s); } catch { s.Warning += "GPU counters unavailable. "; } }
            s.State = "DISPLAY ONLY";
            if (!String.IsNullOrWhiteSpace(settings.WatchProcess)) {
                s.StateSource = "process check";
                var processes = Process.GetProcessesByName(Metrics.ProcessName(settings.WatchProcess));
                s.State = processes.Length > 0 ? "PROCESS RUNNING" : "NOT DETECTED";
                foreach (var p in processes) p.Dispose();
            }
            // Read afresh so missing or malformed state never silently presents an old success state.
            if (statePath.Length > 0 && File.Exists(statePath)) {
                try {
                    if (new FileInfo(statePath).Length > 16384) throw new ArgumentException("State file is too large.");
                    var state = new JavaScriptSerializer().Deserialize<WorkloadState>(File.ReadAllText(statePath));
                    if (state == null) throw new ArgumentException("Empty state.");
                    state.Validate(); s.State = state.State; s.StateSource = "state file (reported)";
                    s.ExperimentStart = state.StartUtc() ?? launch;
                    s.RunId = state.RunId; s.RainOverride = state.RainEnabled;
                    if (!String.IsNullOrEmpty(state.HeartbeatUtc)) s.StateSource = "workload runner";
                    if (state.IsStale(DateTime.UtcNow)) { s.State = "WORKLOAD UNRESPONSIVE"; s.Warning += "Runner heartbeat is older than 20s. "; }
                    if (!String.IsNullOrEmpty(state.Detail)) s.Warning += state.Detail;
                    if (!String.IsNullOrEmpty(state.WorkloadLabel)) s.Workload = state.WorkloadLabel;
                } catch (Exception e) { s.State = "STATE FILE ERROR"; s.StateSource = "state file"; s.Warning += e.Message; }
            }
            return s;
        }
        public void Dispose() { if (gpu != null) gpu.Dispose(); self.Dispose(); }
    }
}
