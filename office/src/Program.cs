using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Web.Script.Serialization;
using MatrixOffice;
[assembly: AssemblyTitle("Matrix Office Workday")]
[assembly: AssemblyVersion("2.6.0.0")]
[assembly: AssemblyFileVersion("2.6.0.0")]
namespace MatrixOfficeActor {
    internal sealed class Authorization {
        public string RunId { get; set; }
        public string Mode { get; set; }
        public string State { get; set; }
        public string Phase { get; set; }
        public string PhaseStartedUtc { get; set; }
        public string ActorSid { get; set; }
        public string UpdatedUtc { get; set; }
        public int Pid { get; set; }
        public int ActorSession { get; set; }
        public int PhaseSeconds { get; set; }
    }
    internal static class Program {
        static readonly object gate=new object();
        static readonly JavaScriptSerializer json=new JavaScriptSerializer();
        static OfficeProgress status;
        static string logPath, summaryPath;
        static DateTime actionStarted=DateTime.MaxValue;
        static Applications apps;
        static volatile bool finished;
        static void Log(string text) { lock(gate) File.AppendAllText(logPath,DateTime.UtcNow.ToString("o")+" "+text+Environment.NewLine); }
        static Authorization Read(string path) { return json.Deserialize<Authorization>(StatusFile.Read(path,true)); }
        [STAThread] static int Main(string[] args) {
            string id=null, data=null;
            try {
                if(args.Length!=4 || args[0]!="--run-id" || args[2]!="--data") throw new ArgumentException("Use --run-id GUID --data installed-data-directory.");
                id=Guid.Parse(args[1]).ToString("N"); data=Path.GetFullPath(args[3]).TrimEnd(Path.DirectorySeparatorChar);
                string installed=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"AfterSix","MatrixRainLab");
                if(!data.Equals(installed,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Office activity requires the installed shared data directory.");
                string sid; using(var identity=WindowsIdentity.GetCurrent()) {
                    sid=identity.User.Value;
                    if(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) throw new InvalidOperationException("Office activity must run unelevated as the signed-in desktop user.");
                }
                if(!Environment.UserInteractive || Process.GetCurrentProcess().SessionId==0) throw new InvalidOperationException("An interactive signed-in desktop is required.");
                string runtime=Path.Combine(data,"workloads"), manifest=Path.Combine(runtime,"run.json");
                Authorization permit=Read(manifest);
                if(permit.RunId!=id || permit.Mode!="office" || permit.State!="RUNNING" || permit.ActorSid!=sid || permit.ActorSession!=Process.GetCurrentProcess().SessionId)
                    throw new UnauthorizedAccessException("No matching administrator-approved Office run for this user/session.");
                DateTime stamp=DateTime.Parse(permit.UpdatedUtc).ToUniversalTime();
                if((DateTime.UtcNow-stamp).TotalSeconds>20) throw new UnauthorizedAccessException("Administrator worker heartbeat is stale.");
                string local=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AfterSix","MatrixRainLab","office",id);
                Directory.CreateDirectory(local); logPath=Path.Combine(local,"actions.log"); summaryPath=Path.Combine(local,"summary.json");
                status=new OfficeProgress { State="WAITING", Activity="Checking Office applications", UserSid=sid, Pid=Process.GetCurrentProcess().Id };
                bool created;
                using(var mutex=new Mutex(true,"Local\\AfterSix.MatrixOffice."+id,out created)) {
                    if(!created) throw new InvalidOperationException("An Office helper already exists for this run.");
                    using(var pipe=new NamedPipeClientStream(".","AfterSix.MatrixOffice."+id,PipeDirection.Out,PipeOptions.None)) {
                        pipe.Connect(15000); uint server;
                        if(!Native.GetNamedPipeServerProcessId(pipe.SafePipeHandle,out server) || server!=(uint)permit.Pid) throw new UnauthorizedAccessException("Office status pipe is not owned by the approved worker.");
                        using(var writer=new StreamWriter(pipe) { AutoFlush=true }) {
                            var heartbeat=new Thread(delegate() {
                                while(!finished) {
                                    try {
                                        lock(gate) { status.UpdatedUtc=DateTime.UtcNow.ToString("o"); writer.WriteLine(new JavaScriptSerializer().Serialize(status)); }
                                        if((DateTime.UtcNow-actionStarted).TotalSeconds>90) {
                                            lock(gate) { status.State="FAILED"; status.Error="Application action exceeded 90 seconds: "+status.Activity; writer.WriteLine(new JavaScriptSerializer().Serialize(status)); File.WriteAllText(summaryPath,new JavaScriptSerializer().Serialize(status)); }
                                            if(apps!=null) apps.EmergencyClose();
                                            Environment.Exit(1);
                                        }
                                    } catch { /* Main loop observes worker heartbeat/stop and performs cleanup. */ }
                                    Thread.Sleep(1000);
                                }
                            }); heartbeat.IsBackground=true; heartbeat.Start();
                            int exit=0;
                            try {
                                int seed=Guid.Parse(id).GetHashCode(); Log("Run "+id+" / schedule seed "+seed+" / user "+sid+" / session "+permit.ActorSession);
                                actionStarted=DateTime.UtcNow;
                                apps=new Applications(id,local,status,seed,Log,data,delegate(string stage) { actionStarted=DateTime.UtcNow; status.Activity=stage; Log(stage); }); actionStarted=DateTime.MaxValue;
                                Native.SetThreadExecutionState(0x80000003);
                                double paused=0; DateTime lastLoop=DateTime.UtcNow;
                                bool started=false; DateTime phaseStart=DateTime.MaxValue; int duration=0,index=0;
                                System.Collections.Generic.List<OfficeStep> plan=null; Stopwatch phaseClock=null; double initialElapsed=0, preparationSeconds=0, interactionPaused=0;
                                while(true) {
                                    double interval=(DateTime.UtcNow-lastLoop).TotalSeconds; lastLoop=DateTime.UtcNow;
                                    permit=Read(manifest);
                                    if(permit.RunId!=id || permit.State!="RUNNING" || File.Exists(Path.Combine(runtime,"stop-"+id))) { Log("Administrator stop/run termination observed."); break; }
                                    if((DateTime.UtcNow-DateTime.Parse(permit.UpdatedUtc).ToUniversalTime()).TotalSeconds>20) throw new IOException("Administrator worker heartbeat is stale; ending Office activity.");
                                    if(permit.Phase=="office") {
                                        if(!started) {
                                            phaseStart=DateTime.Parse(permit.PhaseStartedUtc).ToUniversalTime(); duration=permit.PhaseSeconds;
                                            initialElapsed=Math.Max(0,(DateTime.UtcNow-phaseStart).TotalSeconds); phaseClock=Stopwatch.StartNew();
                                            plan=OfficePlan.Create(duration,seed); File.WriteAllText(Path.Combine(local,"schedule.json"),json.Serialize(plan)); started=true;
                                        }
                                        double elapsed=initialElapsed+phaseClock.Elapsed.TotalSeconds;
                                        if(elapsed>=duration) { if(apps.OutlookPreparing) throw new TimeoutException("Workday ended before Outlook preparation completed; increase duration or complete account setup first."); Log("Office phase deadline reached."); break; }
                                        if(!Native.DesktopAvailable()) { paused+=interval; status.PausedSeconds=(int)paused; status.State="PAUSED"; status.Activity="Desktop unavailable or locked; wall-clock deadline continues"; }
                                        else if(apps.PrepareOutlook()) { preparationSeconds+=interval; status.State="PREPARING"; status.Activity=status.OutlookPreparation; }
                                        else if(Native.InputIdleSeconds()<15) { interactionPaused+=interval; status.InteractionPausedSeconds=(int)interactionPaused; status.State="PAUSED"; status.Activity="Mouse/keyboard input detected; resuming after 15 seconds idle"; }
                                        else if(index<plan.Count && elapsed-preparationSeconds>=plan[index].AtSeconds) {
                                            string action=plan[index++].Action; status.State="ACTIVE"; status.Activity=action;
                                            actionStarted=DateTime.UtcNow; apps.Execute(action); actionStarted=DateTime.MaxValue;
                                            Log("ACTION "+action+" / actions="+status.Actions+" / saves="+status.Saved);
                                            // Do not burst through missed actions after a locked desktop or slow app.
                                            while(index>=8 && index<plan.Count && plan[index].AtSeconds<elapsed-preparationSeconds) { index++; status.SkippedActions++; }
                                        } else { status.State="ACTIVE"; status.Activity="Reading / thinking; applications remain open"; }
                                        if(status.State=="ACTIVE") { actionStarted=DateTime.UtcNow; apps.RotateForeground(); actionStarted=DateTime.MaxValue; }
                                    } else { status.State="WAITING"; status.Activity="Baseline observation; Office apps not yet started"; }
                                    Thread.Sleep(250);
                                }
                            } catch(Exception e) { status.State="CLEANING"; status.Error=e.GetType().Name+": "+e.Message; Log("FAILURE "+e); exit=1; }
                            finally {
                                actionStarted=DateTime.UtcNow; status.State="CLEANING"; status.Activity="Closing test-owned documents and application windows";
                                try { if(apps!=null) apps.Dispose(); }
                                catch(Exception e) { status.State="FAILED"; status.Error=CleanupSteps.Preserve(status.Error,e); Log("CLEANUP FAILURE "+e.ToString()); exit=1; }
                                actionStarted=DateTime.MaxValue;
                                if(exit!=0) status.State="FAILED";
                                if(exit==0) { status.State="COMPLETED"; status.Activity="Cleanup completed; files retained (pre-existing/unowned app windows may remain)"; }
                                lock(gate) {
                                    status.UpdatedUtc=DateTime.UtcNow.ToString("o"); File.WriteAllText(summaryPath,json.Serialize(status));
                                    try { writer.WriteLine(json.Serialize(status)); } catch { }
                                    finished=true;
                                }
                                heartbeat.Join(2000); Native.SetThreadExecutionState(0x80000000);
                            }
                            return exit;
                        }
                    }
                }
            } catch(Exception e) {
                try {
                    string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AfterSix","MatrixRainLab");
                    Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root,"office-launch-error.txt"),DateTime.UtcNow.ToString("o")+Environment.NewLine+e);
                } catch { }
                return 1;
            }
        }
    }
}
