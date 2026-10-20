using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
[assembly: AssemblyTitle("Matrix Lab Worker")]
[assembly: AssemblyVersion("3.2.0.0")]
[assembly: AssemblyFileVersion("3.2.0.0")]

namespace MatrixLabWorkloads {
    internal static class Program {
        [StructLayout(LayoutKind.Sequential)] struct MemoryStatus { public uint Size, Load; public ulong Total, Available, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, Extended; }
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        static MemoryInfo Memory() {
#if PORTABLE_TEST
            return new MemoryInfo { Total = 8L * 1073741824, Available = 6L * 1073741824 };
#else
            var m = new MemoryStatus { Size = (uint)Marshal.SizeOf(typeof(MemoryStatus)) };
            if (!GlobalMemoryStatusEx(ref m)) throw new InvalidOperationException("Cannot read Windows memory availability.");
            return new MemoryInfo { Total = (long)m.Total, Available = (long)m.Available };
#endif
        }
        internal static void Write(string path, object value) {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temp, Json.Serialize(value));
            try {
                for (int i = 0; ; i++) {
                    try { if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); break; }
                    catch (IOException) { if (i == 4) throw; Thread.Sleep(50); }
                }
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        static string StatePath(string data) {
            string config = Path.Combine(data, "config.json");
            if (!File.Exists(config)) return Path.Combine(data, "state.json");
            var values = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(config));
            object path;
            if (!values.TryGetValue("StateFile", out path) || String.IsNullOrWhiteSpace(path as string)) throw new ArgumentException("Enable StateFile in the display config before starting a workload.");
            return Path.GetFullPath(Path.Combine(data, (string)path));
        }
        static int Main(string[] args) {
            string errorPath = null;
            string stage="administrator check";
            try {
                #if !PORTABLE_TEST
                if (Environment.OSVersion.Platform != PlatformID.Win32NT) throw new PlatformNotSupportedException("The packaged worker requires Windows x64.");
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent()) {
                    if (!new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                        throw new UnauthorizedAccessException("Administrator elevation is required to control workloads.");
                }
#endif
                stage="parse arguments";
                var values = new Dictionary<string, string>();
                for (int i = 0; i < args.Length; i += 2) {
                    if (i + 1 >= args.Length || values.ContainsKey(args[i])) throw new ArgumentException("Arguments require unique --name value pairs.");
                    values.Add(args[i], args[i + 1]);
                }
                var allowed = new [] { "--mode", "--seconds", "--intensity", "--threads", "--memory-mib", "--disk-mib", "--write-limit-mib", "--disk-rate-mib", "--data", "--run-id", "--stop", "--baseline-seconds", "--power-report", "--actor-sid", "--actor-session" };
                foreach (string key in values.Keys) if (!allowed.Contains(key)) throw new ArgumentException("Unknown option: " + key);
                var options = new Options();
                if (values.ContainsKey("--mode")) options.Mode = values["--mode"].ToLowerInvariant();
                if (values.ContainsKey("--seconds")) options.Seconds = Int32.Parse(values["--seconds"]);
                if (values.ContainsKey("--intensity")) options.Intensity = Int32.Parse(values["--intensity"]);
                if (values.ContainsKey("--threads")) options.Threads = Int32.Parse(values["--threads"]);
                if (values.ContainsKey("--memory-mib")) options.MemoryMiB = Int32.Parse(values["--memory-mib"]);
                if (values.ContainsKey("--disk-mib")) options.DiskMiB = Int32.Parse(values["--disk-mib"]);
                if (values.ContainsKey("--write-limit-mib")) options.WriteLimitMiB = Int32.Parse(values["--write-limit-mib"]);
                if (values.ContainsKey("--disk-rate-mib")) options.DiskMiBPerSecond = Int32.Parse(values["--disk-rate-mib"]);
                options.Validate();
                int baselineSeconds = values.ContainsKey("--baseline-seconds") ? Int32.Parse(values["--baseline-seconds"]) : 0;
                if (baselineSeconds < 0 || baselineSeconds > 600 || (options.Mode != "baseline" && options.Seconds + baselineSeconds > ((options.Mode=="office" || options.Mode=="windows") ? MatrixOffice.OfficePlan.MaximumSeconds+600 : 3600))) throw new ArgumentException("Baseline must be 0..600 seconds and total duration at most 3600 seconds.");
                if (options.Mode == "baseline") baselineSeconds = 0;
                string actorSid=null; int actorSession=0;
                if(options.Mode=="office" || options.Mode=="windows" || options.Mode=="rotation") {
                    actorSid=values.ContainsKey("--actor-sid") ? values["--actor-sid"] : null;
                    if(String.IsNullOrEmpty(actorSid) || !values.ContainsKey("--actor-session") || !Int32.TryParse(values["--actor-session"],out actorSession) || actorSession<=0)
                        throw new ArgumentException("Microsoft 365 Testing must be started from the signed-in user's Matrix display.");
#if !PORTABLE_TEST
                    new System.Security.Principal.SecurityIdentifier(actorSid);
#endif
                }
                string data = Path.GetFullPath(values.ContainsKey("--data") ? values["--data"] : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AfterSix", "MatrixRainLab"));
                Guid id = values.ContainsKey("--run-id") ? Guid.Parse(values["--run-id"]) : Guid.NewGuid();
                stage="create shared runtime directory: "+data;
                string runtime = Path.Combine(data, "workloads"); Directory.CreateDirectory(runtime);
                errorPath = Path.Combine(runtime, "error-" + id.ToString("N") + ".txt");
                stage="initialize worker log";
                var logs=new DirectoryInfo(runtime).GetFiles("worker-*.txt").OrderByDescending(f=>f.LastWriteTimeUtc).ToArray();
                foreach(var log in logs.Skip(9)) { try { log.Delete(); } catch(IOException) { } }
                File.WriteAllText(Path.Combine(runtime,"worker-"+id.ToString("N")+".txt"),
                    DateTime.UtcNow.ToString("o")+" Worker 3.2.0 started as "+Environment.UserDomainName+"\\"+Environment.UserName+
                    "\r\nData: "+data+"\r\nAction: "+(values.ContainsKey("--power-report") ? "power report" : values.ContainsKey("--stop") ? "stop" : options.Mode));
                stage="prune retained errors";
                var errors = new DirectoryInfo(runtime).GetFiles("error-*.txt").OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
                foreach (var error in errors.Skip(19)) { try { error.Delete(); } catch (IOException) { } }
                if (values.ContainsKey("--power-report")) {
                    if(values["--power-report"]!="true" || values.ContainsKey("--stop")) throw new ArgumentException("Invalid power-report request.");
                    MatrixRainLab.PowerDiagnostics.Save(runtime,id.ToString("N")); return 0;
                }
                if (values.ContainsKey("--stop")) return Stop(runtime, Guid.Parse(values["--stop"]).ToString("N"));
                stage="acquire workload lock and run";
                // Cross-session, cross-process lease. The OS releases it after a crash; no stale PID killing.
                using (var lease = new FileStream(Path.Combine(runtime, "run.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
                    return Run(options, data, runtime, id.ToString("N"), baselineSeconds,actorSid,actorSession);
                }
            } catch (Exception e) {
                Console.Error.WriteLine(e.ToString());
                if (errorPath != null) { try { File.WriteAllText(errorPath, "Stage: "+stage+Environment.NewLine+e.ToString()); } catch { } }
                return 1;
            }
        }
        static int Stop(string runtime, string id) {
            string manifest = Path.Combine(runtime, "run.json");
            if (!File.Exists(manifest)) return 0;
            var run = Json.Deserialize<Dictionary<string, object>>(MatrixOffice.StatusFile.Read(manifest));
            if ((string)run["RunId"] != id || (string)run["State"] != "RUNNING") return 0;
            string request = Path.Combine(runtime, "stop-" + id);
            File.WriteAllText(request, "stop");
            int polls=Array.IndexOf(new[]{"office","windows","rotation"},(string)run["Mode"])>=0 ? 1000 : 150;
            for (int i=0; i<polls; i++) {
                Thread.Sleep(100);
                run = Json.Deserialize<Dictionary<string, object>>(MatrixOffice.StatusFile.Read(manifest));
                if ((string)run["RunId"] != id || (string)run["State"] != "RUNNING") return 0;
            }
            throw new TimeoutException("Stop requested, but the worker did not acknowledge within the stop window. Check its heartbeat and logs.");
        }
        static int Run(Options options, string data, string runtime, string id, int baselineSeconds,string actorSid=null,int actorSession=0) {
            string statePath = StatePath(data), runPath = Path.Combine(runtime, "run.json"), stopPath = Path.Combine(runtime, "stop-" + id);
            string started = DateTime.UtcNow.ToString("o"), phaseStarted = started;
            var total = Stopwatch.StartNew();
            var phases = new List<object>();
            int index = 0, count = baselineSeconds > 0 ? 2 : 1;
            Options current = baselineSeconds > 0 ? new Options { Mode="baseline", Seconds=baselineSeconds } : options;
            var phaseClock = Stopwatch.StartNew();
            object lastSnapshot = null;
            OfficeBridge office=null;
            bool rotating=options.Mode=="rotation";
            var rotation=new MatrixRainLab.RotationPlan(Guid.Parse(id).GetHashCode(),Environment.ProcessorCount);
            string helperId=null;
            try {
            Action<string,string,Result> publish = delegate(string state,string detail,Result result) {
                string now=DateTime.UtcNow.ToString("o");
                double phaseElapsed=phaseClock.Elapsed.TotalSeconds;
                var snapshot = new { RunId=id, Hostname=Environment.MachineName, Pid=Process.GetCurrentProcess().Id,
                    ActorSid=actorSid, ActorSession=actorSession, HelperId=helperId, Office=office==null ? result==null ? null : result.Office : office.Progress,
                    State=state, Mode=options.Mode, Phase=current.Mode, PhaseIndex=rotating ? index+1 : Math.Min(index+1,count), PhaseCount=rotating ? 0 : count,
                    StartedUtc=started, UpdatedUtc=now, PhaseStartedUtc=phaseStarted, PhaseSeconds=current.Seconds,
                    PlannedSeconds=rotating ? 0 : options.Seconds+baselineSeconds, ElapsedSeconds=total.Elapsed.TotalSeconds,
                    PhaseElapsedSeconds=phaseElapsed, Detail=detail, Options=current, BaselineSeconds=baselineSeconds, Result=result, Phases=phases };
                Write(statePath, new { State=state, ExperimentStartedUtc=started, WorkloadLabel=MatrixRainLab.WorkloadRequest.Label(current.Mode).ToUpperInvariant()+" / "+(index+1)+(rotating ? " / CONTINUOUS" : " OF "+count),
                    Detail=detail, HeartbeatUtc=now, RunId=id, Phase=current.Mode, RainEnabled=(bool?)(current.Mode!="baseline") });
                Write(runPath,snapshot); lastSnapshot=snapshot;
            };
            string scratch=Path.Combine(runtime,"scratch");
            if(Directory.Exists(scratch)) foreach(string path in Directory.GetFiles(scratch,"io-*.tmp"))
                if(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path),@"^io-[0-9a-f]{32}\.tmp$")) File.Delete(path);
            var old=new DirectoryInfo(runtime).GetFiles("result-*.json").OrderByDescending(f=>f.LastWriteTimeUtc).ToArray();
            for(int i=19;i<old.Length;i++) old[i].Delete();
            try {
                for(index=0;rotating || index<count;index++) {
                    if(rotating) {
                        // Alternate randomly selected activity with genuine screensaver-only rest.
                        // Rotation never includes Microsoft 365; pressure is intentionally capped.
                        var selected=rotation.Next();
                        current=new Options { Mode=selected.Mode, Seconds=selected.Seconds, Intensity=selected.Intensity,
                            Threads=selected.Threads, MemoryMiB=selected.MemoryMiB, DiskMiB=selected.DiskMiB,
                            WriteLimitMiB=selected.WriteLimitMiB, DiskMiBPerSecond=selected.DiskMiBPerSecond };
#if PORTABLE_TEST
                        // Accelerated transitions only in the portable test host, never shipped EXEs.
                        current.Seconds=1;
#endif
                    } else current=baselineSeconds>0 && index==0 ? new Options { Mode="baseline", Seconds=baselineSeconds } : options;
                    helperId=Guid.NewGuid().ToString("N");
                    if(current.Mode=="office" || current.Mode=="windows") office=new OfficeBridge(helperId,actorSid);
                    phaseStarted=DateTime.UtcNow.ToString("o"); phaseClock.Restart();
                    // Check between phases as well as inside the engine. A stop during baseline cancels the whole experiment.
                    if(File.Exists(stopPath)) { publish("IDLE","stopped by operator before "+current.Mode,null); break; }
                    publish("RUNNING","Starting "+current.Mode,null);
                    var engine=new Engine(current,scratch,Memory,
                        delegate { return new DriveInfo(Path.GetPathRoot(runtime)).AvailableFreeSpace; },
                        delegate { return File.Exists(stopPath); },
                        delegate(Result result) { if(office!=null) office.Check(); publish("RUNNING","Active / "+current.Mode,result); });
                    Result final;
                    if(current.Mode=="office" || current.Mode=="windows") final=office.Run(current,delegate { return File.Exists(stopPath); },delegate(Result result) {
                        var progress=office.Progress; publish("RUNNING",progress==null ? "Waiting for signed-in Office helper" : "Office / "+progress.Activity,result);
                    });
                    else final=engine.Run();
                    phases.Add(new { Phase=current.Mode, StartedUtc=phaseStarted, EndedUtc=DateTime.UtcNow.ToString("o"), Result=final });
                    Write(Path.Combine(runtime,"last-phase.json"),new { RunId=id,Phase=current.Mode,Result=final,EndedUtc=DateTime.UtcNow.ToString("o") });
                    if(office!=null) { office.Dispose(); office=null; }
                    // Bound status size during indefinite runs; last-phase.json retains the latest result.
                    if(phases.Count>32) phases.RemoveAt(0);
                    if(final.Reason=="stopped by operator" || File.Exists(stopPath)) { publish("IDLE","stopped by operator during "+current.Mode,final); break; }
                    if(!rotating && index==count-1) {
                        string detail=final.Reason;
                        if(current.Mode=="science" && final.Pi.HasValue) detail+=" / pi ~ "+final.Pi.Value.ToString("F6",System.Globalization.CultureInfo.InvariantCulture);
                        publish("COMPLETED",detail,final);
                    }
                }
                Write(Path.Combine(runtime,"result-"+id+".json"),lastSnapshot);
                Write(Path.Combine(runtime,"last-result.json"),lastSnapshot);
                return 0;
            } catch(Exception e) {
                string message=e.Message;
                phases.Add(new { Phase=current.Mode, StartedUtc=phaseStarted, EndedUtc=DateTime.UtcNow.ToString("o"), Error=message });
                publish("FAILED",message,null);
                Write(Path.Combine(runtime,"result-"+id+".json"),lastSnapshot);
                Write(Path.Combine(runtime,"last-result.json"),lastSnapshot);
                Console.Error.WriteLine(e.ToString()); return 1;
            } finally { if(File.Exists(stopPath)) File.Delete(stopPath); }
            } finally { if(office!=null) office.Dispose(); }
        }
    }
}
