using System;
using System.ComponentModel;

using System.IO;
using System.Text;
using System.Web.Script.Serialization;
namespace MatrixRainLab {
    internal sealed class RunStatus {
        public string RunId { get; set; }
        public string ActorSid { get; set; }
        public int ActorSession { get; set; }
        public MatrixOffice.OfficeProgress Office { get; set; }
        public string State { get; set; }
        public string Mode { get; set; }
        public string Phase { get; set; }
        public int PhaseIndex { get; set; }
        public int PhaseCount { get; set; }
        public int PhaseSeconds { get; set; }
        public int PlannedSeconds { get; set; }
        public double ElapsedSeconds { get; set; }
        public double PhaseElapsedSeconds { get; set; }
        public string PhaseStartedUtc { get; set; }
        public double? ScientificPi { get { return Mode=="science" && Result!=null ? Result.Pi : null; } }
        public string ActivePhase { get { return String.IsNullOrEmpty(Phase) ? Mode : Phase; } }
        public double PhaseElapsed(DateTime utc) {
            DateTimeOffset stamp;
            double age=State=="RUNNING" && !Stale && DateTimeOffset.TryParse(UpdatedUtc,out stamp) ? Math.Max(0,(utc-stamp.UtcDateTime).TotalSeconds) : 0;
            return Math.Max(0,PhaseElapsedSeconds+age);
        }
        public double Remaining(DateTime utc) { return State=="RUNNING" ? Math.Max(0,PhaseSeconds-PhaseElapsed(utc)) : 0; }
        public double TotalRemaining(DateTime utc) {
            DateTimeOffset stamp;
            double age=State=="RUNNING" && !Stale && DateTimeOffset.TryParse(UpdatedUtc,out stamp) ? Math.Max(0,(utc-stamp.UtcDateTime).TotalSeconds) : 0;
            return State=="RUNNING" ? Math.Max(0,PlannedSeconds-ElapsedSeconds-age) : 0;
        }
        public double Progress(DateTime utc) { return State=="COMPLETED" ? 1 : PhaseSeconds>0 ? Math.Max(0,Math.Min(1,PhaseElapsed(utc)/PhaseSeconds)) : 0; }
        public string Summary { get { return (Office==null ? "" : "Office: "+Office.Created+" files / "+Office.Saved+" saves / "+Office.Actions+" actions / ")+Mode+" / "+State+" / "+Detail+(Result!=null && Result.BytesWritten>0 ? " / "+(Result.BytesWritten/1048576.0).ToString("F0")+" MiB written" : ""); } }
        public string Detail { get; set; }
        public string StartedUtc { get; set; }
        public string UpdatedUtc { get; set; }
        public RunResult Result { get; set; }
        public PhaseResult[] Phases { get; set; }
        public bool Stale {
            get { DateTimeOffset utc; return State=="RUNNING" && (!DateTimeOffset.TryParse(UpdatedUtc,out utc) || (DateTime.UtcNow-utc.UtcDateTime).TotalSeconds>20); }
        }
    }
    internal sealed class PhaseResult {
        public string Phase { get; set; }
        public string StartedUtc { get; set; }
        public string EndedUtc { get; set; }
        public string Error { get; set; }
        public RunResult Result { get; set; }
    }
    internal sealed class RunResult {
        public string Reason { get; set; }
        public double ElapsedSeconds { get; set; }
        public long BytesWritten { get; set; }
        public long BytesRead { get; set; }
        public int AllocatedMiB { get; set; }
        public double? Pi { get; set; }
    }
    internal sealed class WorkloadSession : IDisposable {
        readonly string directory, worker;
        readonly JavaScriptSerializer json=new JavaScriptSerializer();
        ElevationOperation pending;
        readonly Func<string,string,IntPtr,IElevatedProcess> launcher;
        readonly string logDirectory;
        bool launchObserved;
        public IntPtr OwnerWindow { get; set; }
        public bool AwaitingElevation { get { return busy && pending!=null && !pending.Complete; } }
        string pendingId, stopTarget;
        DateTime pendingSince;
        RunStatus latest;
        bool busy;
        public string Message { get; private set; }
        public string StatusText { get; private set; }
        public bool CanStart { get; private set; }
        public bool CanStop { get; private set; }
        public bool Busy { get { return busy; } }
        public RunStatus Latest { get { return latest; } }
        public RunStatus LastResult { get; private set; }
        public bool StopAcknowledged { get; private set; }
        public bool ExitNeedsChoice { get { return busy || !CanStart || (latest!=null && latest.State=="RUNNING"); } }
        public WorkloadSession(string data) : this(data,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MatrixLabWorker.exe"),ElevatedProcess.Start,LaunchDiagnostics.DirectoryPath) { }
        internal WorkloadSession(string data,string workerPath,Func<string,string,IntPtr,IElevatedProcess> launch,string logs) {
            directory=Path.GetFullPath(data); worker=workerPath; launcher=launch; logDirectory=logs;
            Message="Choose a test, then Go. Administrator approval required."; RefreshRun();
        }
        void Log(string message) { LaunchDiagnostics.Write(logDirectory,"Run "+pendingId+" / "+message); }
        public void StartRun(WorkloadRequest request) {
            RefreshRun(); if(!CanStart) return;
            try {
                request.Validate(); string id=Guid.NewGuid().ToString("N"); string args=request.Arguments(directory,Guid.Parse(id));
                if(request.Mode=="office") {
#if !PORTABLE_TEST
                    using(var identity=System.Security.Principal.WindowsIdentity.GetCurrent()) {
                        if(new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                            throw new InvalidOperationException("Open the display normally, without Run as administrator, for Office workday.");
                        args+=" --actor-sid "+identity.User.Value+" --actor-session "+System.Diagnostics.Process.GetCurrentProcess().SessionId;
                    }
#endif
                }
                Launch(args,id,null);
            }
            catch(Exception e) { Message=e.Message; }
        }
        static string ReadOptionalStatus(string path) {
            try { return MatrixOffice.StatusFile.Read(path); }
            catch(FileNotFoundException) { return null; }
            catch(DirectoryNotFoundException) { return null; }
            // Access denied must disable controls; it does not mean no worker exists.
        }
        public void Poll() { RefreshRun(); }
        public void StopRun() {
            StopAcknowledged=false;
            if(latest==null) return;
            Guid target; if(!Guid.TryParse(latest.RunId,out target)) { Message="Current run ID is invalid."; return; }
            string id=Guid.NewGuid().ToString("N");
            Launch("--stop "+target.ToString("N")+" --data "+WorkloadRequest.Quote(directory)+" --run-id "+id,id,target.ToString("N"));
        }
        void Launch(string args,string id,string target) {
            if(busy) return;
            try {
                if(!File.Exists(worker)) throw new FileNotFoundException("Install version 2.5.0 of the workload runner first.");
                busy=true; CanStart=CanStop=false;
                pendingId=id; stopTarget=target; pendingSince=DateTime.UtcNow; launchObserved=false;
                Message="Requesting administrator approval... Windows may take time to show the credential prompt.";
                Log("Request elevation; worker="+worker+"; data="+directory+"; action="+(target==null ? "start" : "stop"));
                IntPtr owner=OwnerWindow;
                pending=new ElevationOperation(delegate { return launcher(worker,args,owner); });
            } catch(Win32Exception e) { ResetPending(); Message=e.NativeErrorCode==1223 ? "Administrator approval cancelled. No command was started." : e.Message; }
            catch(Exception e) { Log(e.ToString()); ResetPending(); Message=e.Message; }
        }
        void ResetPending() { busy=false; pendingId=stopTarget=null; if(pending!=null) pending.Dispose(); pending=null; }
        void RefreshRun() {
            try {
                string previousState = latest == null ? null : latest.RunId + latest.State;
                string path=Path.Combine(directory,"workloads","run.json");
                string content=ReadOptionalStatus(path);
                latest=content==null ? null : json.Deserialize<RunStatus>(content);
                if (latest != null) {
                    Guid parsed;
                    if (!Guid.TryParse(latest.RunId, out parsed) || Array.IndexOf(WorkloadRequest.Modes, latest.Mode) < 0 ||
                        Array.IndexOf(new[] { "RUNNING", "IDLE", "COMPLETED", "FAILED" }, latest.State) < 0)
                        throw new InvalidDataException("Worker status has an invalid run ID, mode or state.");
                }
                if(latest==null) StatusText="No workload has been recorded on this node.";
                else {
                    var b=new StringBuilder(); b.AppendLine((latest.Stale ? "HEARTBEAT STALE / " : "")+latest.State+"  |  "+latest.Mode);
                    b.AppendLine(latest.Detail); b.AppendLine("Started: "+latest.StartedUtc); b.AppendLine("Updated: "+latest.UpdatedUtc); b.AppendLine("Run ID: "+latest.RunId);
                    if(latest.Result!=null) b.AppendLine("Elapsed: "+latest.Result.ElapsedSeconds.ToString("F1")+"s  |  RAM: "+latest.Result.AllocatedMiB+" MiB  |  Written: "+(latest.Result.BytesWritten/1048576.0).ToString("F1")+" MiB"+(latest.ScientificPi.HasValue ? "  |  Pi: "+latest.ScientificPi.Value.ToString("F6") : ""));
                    StatusText=b.ToString();
                }
                if (!busy && latest != null && previousState != latest.RunId + latest.State)
                    Message = latest.Detail + (latest.Result != null && latest.ScientificPi.HasValue ? " / Pi " + latest.ScientificPi.Value.ToString("F6") : "");
                if(busy) {
                    string error=Path.Combine(directory,"workloads","error-"+pendingId+".txt");
                    if(File.Exists(error)) { string text=File.ReadAllText(error); Log("Worker error: "+text); ResetPending(); Message="Worker error: "+text; }
                    else if(stopTarget==null && latest!=null && latest.RunId==pendingId) { string state=latest.State; Log("Worker acknowledged "+state+" after "+(DateTime.UtcNow-pendingSince).TotalSeconds.ToString("F1")+"s."); ResetPending(); Message=state=="RUNNING" ? "Workload started. Keep this display open to keep the screen awake." : "Workload reported "+state+". See status below."; }
                    else if(stopTarget!=null && latest!=null && (latest.RunId!=stopTarget || latest.State!="RUNNING")) { ResetPending(); StopAcknowledged=true; Message="The requested run is no longer running."; }
                    else if(pending!=null && !pending.Complete) {
                        Message="Waiting for Windows administrator approval ("+(int)(DateTime.UtcNow-pendingSince).TotalSeconds+"s). No second request sent.";
                    } else if(pending!=null) {
                        if(pending.Error!=null) {
                            Exception failure=pending.Error; var native=failure as Win32Exception;
                            Log("Elevation returned after "+(DateTime.UtcNow-pendingSince).TotalSeconds.ToString("F1")+"s: "+failure);
                            ResetPending();
                            Message=native!=null && native.NativeErrorCode==1223 ? "Administrator approval cancelled. No command was started." :
                                "Windows could not start the elevated worker: "+failure.Message+". Export logs for details.";
                        } else {
                            if(!launchObserved) {
                                launchObserved=true;
                                Log("Shell launch returned after "+(DateTime.UtcNow-pendingSince).TotalSeconds.ToString("F1")+"s; awaiting worker acknowledgement.");
                                pendingSince=DateTime.UtcNow;
                                Message="Administrator approval received. Waiting for worker acknowledgement...";
                            }
                            int code;
                            try {
                                if(pending.TryGetExitCode(out code)) {
                                    Log("Worker exited before acknowledgement; exit="+code);
                                    ResetPending(); Message=code==0 ? "Command completed. Refreshing status..." :
                                        "Worker exited (code "+code+") before acknowledgement. Export logs to check permissions and startup errors.";
                                }
                            } catch(Win32Exception e) {
                                Log("Process status unavailable; continuing to read worker status files. "+e);
                            }
                            if(busy && (DateTime.UtcNow-pendingSince).TotalSeconds>30)
                                Message="Waiting for worker acknowledgement. Export logs to check permissions; no second command sent.";
                        }
                    }
                }
                if(latest!=null && latest.State!="RUNNING") LastResult=latest;
                else if(LastResult==null) {
                    string last=Path.Combine(directory,"workloads","last-result.json");
                    try {
                        if(File.Exists(last)) {
                            var record=json.Deserialize<RunStatus>(MatrixOffice.StatusFile.Read(last));
                            if(record!=null && (record.State=="COMPLETED" || record.State=="IDLE" || record.State=="FAILED")) LastResult=record;
                        }
                    } catch(Exception) { /* Optional prior history cannot disable an active experiment's controls. */ }
                }
                CanStart=!busy && (latest==null || latest.State!="RUNNING" || latest.Stale);
                CanStop=!busy && latest!=null && latest.State=="RUNNING";
            } catch(Exception e) { Log("Status read failed: "+e); latest=null; Message=StatusText="Cannot read workload status: "+e.Message; CanStart=false; CanStop=false; }
        }
        public void Dispose() { if(pending!=null) pending.Dispose(); }
    }
}
