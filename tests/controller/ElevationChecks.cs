using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using MatrixRainLab;
static class ElevationChecks {
    sealed class FakeProcess : IElevatedProcess {
        public bool DenyQuery, Exited, Disposed; public int Code;
        public bool TryGetExitCode(out int code) { code=Code; if(DenyQuery) { DenyQuery=false; throw new Win32Exception(5); } return Exited; }
        public void Dispose() { Disposed=true; }
    }
    static void Wait(Func<bool> done, Action poll) {
        var clock=Stopwatch.StartNew();
        while(!done()) { if(clock.Elapsed.TotalSeconds>5) throw new Exception("Timed out waiting for launch fixture"); Thread.Sleep(10); poll(); }
    }
    public static void Run(Action<bool,string> assert) {
        string root=Path.Combine(Path.GetTempPath(),"Matrix elevation "+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); string exe=Path.Combine(root,"worker.exe"); File.WriteAllText(exe,"");
        Directory.CreateDirectory(Path.Combine(root,"workloads"));
        string run=Path.Combine(root,"workloads","run.json");
        var json=new System.Web.Script.Serialization.JavaScriptSerializer();
        try {
            var release=new ManualResetEventSlim(false); string id=null; int launches=0;
            var process=new FakeProcess { DenyQuery=true };
            using(var session=new WorkloadSession(root,exe,delegate(string f,string args,IntPtr owner) {
                Interlocked.Increment(ref launches); id=Regex.Match(args,@"--run-id ([a-f0-9]+)").Groups[1].Value;
                release.Wait(5000); return process;
            },root)) {
                var clock=Stopwatch.StartNew(); session.StartRun(new WorkloadRequest());
                assert(clock.Elapsed.TotalSeconds<0.5 && session.Busy && session.AwaitingElevation,"Go returns without waiting for the Windows credential dialog");
                session.Poll(); session.StartRun(new WorkloadRequest());
                assert(!session.CanStart && !session.CanStop && session.Message.Contains("administrator approval"),"Pending approval has visible status and blocks duplicate commands");
                release.Set(); Wait(()=>!session.AwaitingElevation,session.Poll); session.Poll();
                assert(launches==1 && session.Busy && !session.AwaitingElevation,"Exactly one launch survives the delayed credential prompt");
                assert(session.Busy && File.ReadAllText(Path.Combine(root,"elevation.log")).Contains("Process status unavailable"),"Denied process queries are logged without treating a running worker as failed");
                File.WriteAllText(run,json.Serialize(new RunStatus { RunId=id,Mode="cpu",State="RUNNING",UpdatedUtc=DateTime.UtcNow.ToString("o"),Detail="Active" })); session.Poll();
                assert(!session.Busy && session.CanStop && !session.CanStart && process.Disposed,"Worker heartbeat acknowledges launch and releases only the observer handle");
                File.Delete(run); session.Poll();
            }
            foreach(int error in new[]{1223,5}) {
                using(var session=new WorkloadSession(root,exe,delegate { throw new Win32Exception(error); },root)) {
                    session.StartRun(new WorkloadRequest()); Wait(()=>!session.Busy,session.Poll);
                    assert(session.CanStart && !session.CanStop && session.Message.Contains(error==1223 ? "cancelled" : "could not start"),"Launch error "+error+" restores controls with a specific explanation");
                }
            }
            var failed=new FakeProcess { Exited=true,Code=1 };
            using(var session=new WorkloadSession(root,exe,delegate { return failed; },root)) {
                session.StartRun(new WorkloadRequest()); Wait(()=>!session.Busy,session.Poll);
                assert(session.CanStart && session.Message.Contains("code 1"),"Early worker exit is distinct from Windows elevation failure");
            }
            var late=new FakeProcess(); var gate=new ManualResetEventSlim(false);
            var operation=new ElevationOperation(delegate { gate.Wait(5000); return late; }); operation.Dispose(); gate.Set();
            Wait(()=>operation.Complete,delegate {});
            assert(late.Disposed,"Closing an observer during pending UAC closes a late handle without terminating the worker");
            var log=File.ReadAllText(Path.Combine(root,"elevation.log"));
            assert(log.Contains("Request elevation") && log.Contains("Shell launch returned") && log.Contains("Worker acknowledged"),"Diagnostic log records launch timing, acknowledgement and errors");
        } finally { Directory.Delete(root,true); }
    }
}
