using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MatrixLabWorkloads {
    public sealed class Options {
        public string Mode = "cpu";
        public int Seconds = 600, Intensity = 50, Threads = 2, MemoryMiB = 512, DiskMiB = 64, WriteLimitMiB = 1024, DiskMiBPerSecond = 10;
        public void Validate() {
            if (Array.IndexOf(new [] { "baseline", "visualization", "cpu", "memory", "disk", "science", "office", "windows", "rotation" }, Mode) < 0) throw new ArgumentException("Unknown workload mode.");
            if (Seconds < ((Mode == "office" || Mode == "windows") ? 60 : 1) || Seconds > ((Mode == "office" || Mode == "windows") ? MatrixOffice.OfficePlan.MaximumSeconds : 3600)) throw new ArgumentException("Duration is outside the allowed range for this mode.");
            if (Intensity < 1 || Intensity > 95) throw new ArgumentException("Intensity must be 1..95.");
            if (Threads < 1 || Threads > 32) throw new ArgumentException("Threads must be 1..32.");
            if (MemoryMiB < 8 || MemoryMiB > 4096) throw new ArgumentException("MemoryMiB must be 8..4096.");
            if (DiskMiB < 4 || DiskMiB > 256) throw new ArgumentException("DiskMiB must be 4..256.");
            if (DiskMiBPerSecond < 1 || DiskMiBPerSecond > 256) throw new ArgumentException("DiskMiBPerSecond must be 1..256.");
            if (WriteLimitMiB < DiskMiB || WriteLimitMiB > 4096) throw new ArgumentException("WriteLimitMiB must be DiskMiB..4096.");
        }
    }
    public sealed class MemoryInfo { public long Total, Available; }
    public sealed class Result {
        public string Reason;
        public MatrixOffice.OfficeProgress Office;
        public bool IsScience;
        public double ElapsedSeconds;
        public long Iterations, Hits, BytesWritten, BytesRead;
        public int AllocatedMiB;
        public double? Pi { get { return !IsScience || Iterations == 0 ? (double?)null : 4.0 * Hits / Iterations; } }
    }
    public sealed class Engine {
        const long MiB = 1048576;
        readonly Options options;
        readonly string scratchDirectory;
        readonly Func<MemoryInfo> memory;
        readonly Func<long> freeDisk;
        readonly Func<bool> stop;
        readonly Action<Result> heartbeat;
        readonly Result result = new Result();
        readonly Stopwatch elapsed = new Stopwatch();
        readonly ManualResetEvent cancel = new ManualResetEvent(false);
        readonly List<Thread> workers = new List<Thread>();
        readonly List<byte[]> blocks = new List<byte[]>();
        Exception workerError;
        readonly object errorLock = new object();
        string reason = "duration reached";
        public Engine(Options value, string scratch, Func<MemoryInfo> memoryProvider, Func<long> diskProvider, Func<bool> stopped, Action<Result> pulse) {
            options = value; result.IsScience = value.Mode == "science"; scratchDirectory = scratch; memory = memoryProvider; freeDisk = diskProvider; stop = stopped; heartbeat = pulse;
        }
        void MemoryGuard(long additional) {
            MemoryInfo info = memory();
            long reserve = Math.Max(1024 * MiB, info.Total / 5);
            if (info.Total <= 0 || info.Available - additional < reserve) throw new InvalidOperationException("Memory reserve reached: leaving at least 1 GiB or 20% of physical RAM available.");
        }
        void DiskGuard() { if (freeDisk() < (2L * 1024 + 1) * MiB) throw new IOException("Disk reserve reached: less than 2 GiB free."); }
        bool Ended() {
            if (cancel.WaitOne(0)) return true;
            if (stop()) { reason = "stopped by operator"; cancel.Set(); return true; }
            if (elapsed.Elapsed.TotalSeconds >= options.Seconds) { cancel.Set(); return true; }
            return false;
        }
        void AddWorker(ThreadStart action) {
            var t = new Thread(delegate() { try { action(); } catch (Exception e) { lock (errorLock) { workerError = e; } cancel.Set(); } });
            t.IsBackground = true; workers.Add(t); t.Start();
        }
        static uint Next(ref uint seed) { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return seed; }
        void Calculate(int id) {
            uint seed = (uint)(123456789 + id * 7919); double sink = 0;
            while (!cancel.WaitOne(0)) {
                var slice = Stopwatch.StartNew(); long count = 0, hits = 0;
                while (slice.ElapsedMilliseconds < options.Intensity && !cancel.WaitOne(0)) {
                    for (int j = 0; j < 256; j++) {
                        double x = Next(ref seed) / 4294967296.0;
                        if (options.Mode == "science") {
                            double y = Next(ref seed) / 4294967296.0;
                            if (x * x + y * y <= 1) hits++;
                        } else { sink += Math.Sqrt(x + 0.01); }
                        count++;
                    }
                }
                Interlocked.Add(ref result.Iterations, count); Interlocked.Add(ref result.Hits, hits);
                cancel.WaitOne(Math.Max(1, 100 - (int)slice.ElapsedMilliseconds));
            }
            GC.KeepAlive(sink);
        }
        void Allocate() {
            MemoryGuard(options.MemoryMiB * MiB);
            for (int remaining = options.MemoryMiB; remaining > 0 && !Ended();) {
                int size = Math.Min(8, remaining); MemoryGuard(size * MiB);
                var block = new byte[size * MiB];
                for (int i = 0; i < block.Length; i += 4096) block[i] = 1;
                blocks.Add(block); remaining -= size; result.AllocatedMiB += size;
            }
        }
        void TouchMemory() {
            while (!cancel.WaitOne(0)) {
                var slice = Stopwatch.StartNew();
                foreach (byte[] block in blocks) {
                    for (int i = 0; i < block.Length; i += 4096) block[i]++;
                    if (cancel.WaitOne(0)) return;
                }
                // Intensity controls page-touch frequency, while the allocated amount remains fixed.
                cancel.WaitOne(Math.Max(20, (100 - options.Intensity) * 10 - (int)slice.ElapsedMilliseconds));
            }
        }
        void DiskLoop() {
            DiskGuard(); Directory.CreateDirectory(scratchDirectory);
            string file = Path.Combine(scratchDirectory, "io-" + Guid.NewGuid().ToString("N") + ".tmp");
            byte[] buffer = new byte[MiB]; new Random(741).NextBytes(buffer);
            try {
                using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, buffer.Length, FileOptions.WriteThrough | FileOptions.SequentialScan)) {
                    while (!Ended()) {
                        stream.Position = 0;
                        for (int i = 0; i < options.DiskMiB && !Ended(); i++) {
                            if (result.BytesWritten >= options.WriteLimitMiB * MiB) { reason = "write budget reached"; return; }
                            DiskGuard(); var slice = Stopwatch.StartNew();
                            stream.Write(buffer, 0, buffer.Length); result.BytesWritten += buffer.Length;
                            cancel.WaitOne(Math.Max(1, (int)(1000.0 / (options.DiskMiBPerSecond * options.Intensity / 100.0) - slice.ElapsedMilliseconds)));
                        }
                        stream.Flush(true); stream.Position = 0;
                        while (!Ended()) {
                            int read = stream.Read(buffer, 0, buffer.Length); if (read == 0) break;
                            result.BytesRead += read;
                        }
                    }
                }
            } finally { if (File.Exists(file)) File.Delete(file); }
        }
        public Result Run() {
            options.Validate(); elapsed.Start();
            try {
                if (options.Mode == "memory") { Allocate(); if (!Ended()) AddWorker(TouchMemory); }
                if (options.Mode == "cpu" || options.Mode == "science")
                    for (int i = 0; i < Math.Min(options.Threads, Environment.ProcessorCount); i++) { int id = i; AddWorker(delegate { Calculate(id); }); }
                if (options.Mode == "disk") {
                    // Disk work is on a worker too, so the coordinator can enforce deadline/stop and emit heartbeats.
                    AddWorker(delegate { DiskLoop(); cancel.Set(); });
                }
                double nextPulse = 0;
                while (!Ended()) {
                    if (options.Mode == "memory") MemoryGuard(0);
                    if (elapsed.Elapsed.TotalSeconds >= nextPulse) { result.ElapsedSeconds = elapsed.Elapsed.TotalSeconds; heartbeat(result); nextPulse += 2; }
                    cancel.WaitOne(100);
                }
            } finally {
                cancel.Set(); bool joined = true;
                foreach (Thread t in workers) if (!t.Join(5000)) joined = false;
                if (joined) { blocks.Clear(); cancel.Dispose(); }
                else workerError = new TimeoutException("Worker did not stop within five seconds; process exit will release its resources.");
                elapsed.Stop(); result.ElapsedSeconds = elapsed.Elapsed.TotalSeconds;
            }
            if (workerError != null) throw new InvalidOperationException("Workload failed: " + workerError.Message, workerError);
            result.Reason = reason;
            return result;
        }
    }
}
