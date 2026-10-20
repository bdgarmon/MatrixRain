using System;
using System.Globalization;
using System.Text;

namespace MatrixRainLab {
    public sealed class TestRotation {
        readonly Random random;
        readonly System.Collections.Generic.List<string> remaining=new System.Collections.Generic.List<string>();
        string previous;
        public TestRotation(int seed, string lastMode) { random=new Random(seed); previous=lastMode; }
        public string Next() {
            if(remaining.Count==0) remaining.AddRange(new[] { "visualization", "cpu", "memory", "disk", "science", "windows" });
            var candidates=remaining.FindAll(m => m!=previous);
            // With six unique modes, the last member of a bag cannot equal its predecessor.
            string next=candidates[random.Next(candidates.Count)];
            remaining.Remove(next); previous=next; return next;
        }
    }
    public sealed class WorkloadRequest {
        public string Mode { get; set; }
        public int Seconds { get; set; }
        public int BaselineSeconds { get; set; }
        public int Intensity { get; set; }
        public int Threads { get; set; }
        public int MemoryMiB { get; set; }
        public int DiskMiB { get; set; }
        public int WriteLimitMiB { get; set; }
        public int DiskMiBPerSecond { get; set; }
        public WorkloadRequest() { Mode="cpu"; Seconds=600; BaselineSeconds=60; Intensity=50; Threads=2; MemoryMiB=512; DiskMiB=64; WriteLimitMiB=1024; DiskMiBPerSecond=10; }
        public static readonly string[] Modes = { "baseline", "visualization", "cpu", "memory", "disk", "science", "office", "windows", "rotation" };
        public static string Label(string mode) {
            if(mode=="office") return "Microsoft 365 Testing";
            if(mode=="windows") return "Windows Workday";
            if(mode=="rotation") return "Random Rotation";
            if(mode=="visualization") return "Screensaver / rest";
            return mode ?? "";
        }
        public void Validate() {
            if (Array.IndexOf(Modes, Mode)<0) throw new ArgumentException("Choose a workload.");
            Range(Seconds,(Mode=="office" || Mode=="windows") ? 60 : 1,(Mode=="office" || Mode=="windows") ? MatrixOffice.OfficePlan.MaximumSeconds : 3600,"Duration"); Range(BaselineSeconds,0,600,"Baseline");
            if (Mode != "baseline" && Seconds + BaselineSeconds > ((Mode=="office" || Mode=="windows") ? MatrixOffice.OfficePlan.MaximumSeconds+600 : 3600)) throw new ArgumentException("Baseline plus workload must be at most 3600 seconds."); Range(Intensity,1,95,"Intensity"); Range(Threads,1,32,"Threads");
            Range(MemoryMiB,8,4096,"Memory"); Range(DiskMiB,4,256,"Scratch size");
            Range(WriteLimitMiB,DiskMiB,4096,"Write budget"); Range(DiskMiBPerSecond,1,256,"Disk rate");
        }
        static void Range(int value,int low,int high,string name) { if(value<low || value>high) throw new ArgumentException(name+" must be "+low+".."+high+"."); }
        public static string Quote(string value) {
            if (value==null || value.IndexOf('\0')>=0) throw new ArgumentException("Invalid argument.");
            var b=new StringBuilder("\""); int slashes=0;
            foreach(char c in value) {
                if(c=='\\') { slashes++; continue; }
                if(c=='"') { b.Append('\\',slashes*2+1); b.Append(c); }
                else { b.Append('\\',slashes); b.Append(c); }
                slashes=0;
            }
            b.Append('\\',slashes*2); b.Append('"'); return b.ToString();
        }
        public string Arguments(string data,Guid id) {
            Validate();
            return String.Format(CultureInfo.InvariantCulture,"--mode {0} --seconds {1} --intensity {2} --threads {3} --memory-mib {4} --disk-mib {5} --write-limit-mib {6} --disk-rate-mib {7} --data {8} --run-id {9} --baseline-seconds {10}",Mode,Seconds,Intensity,Threads,MemoryMiB,DiskMiB,WriteLimitMiB,DiskMiBPerSecond,Quote(data),id.ToString("N"),Mode == "baseline" ? 0 : BaselineSeconds);
        }
    }
    // A policy shared by production worker and portable contract tests.
    public sealed class RotationPlan {
        readonly TestRotation modes;
        readonly Random random;
        readonly int processors;
        int phase;
        public RotationPlan(int seed,int cpuCount) { modes=new TestRotation(seed,null); random=new Random(seed); processors=cpuCount; }
        public WorkloadRequest Next() {
            return new WorkloadRequest { Mode=phase++%2==1 ? "visualization" : modes.Next(),
                Seconds=random.Next(180,721), BaselineSeconds=0, Intensity=random.Next(15,56),
                Threads=Math.Max(1,Math.Min(2,processors/2)), MemoryMiB=random.Next(128,513),
                DiskMiB=32, WriteLimitMiB=128, DiskMiBPerSecond=2 };
        }
    }
}
