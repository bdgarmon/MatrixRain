using System;
using System.IO;
using System.Web.Script.Serialization;
using MatrixRainLab;
class Check {
    static int count;
    static void Assert(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS: "+name); count++; }
    static void Main() {
        string data=Path.Combine(Path.GetTempPath(),"Matrix session "+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(data,"workloads"));
        string path=Path.Combine(data,"workloads","run.json");
        var json=new JavaScriptSerializer();
        Action<RunStatus> publish=r=>File.WriteAllText(path,json.Serialize(r));
        try {
            using(var session=new WorkloadSession(data)) {
                Assert(session.CanStart && !session.CanStop,"Absent run starts ready");
                var run=new RunStatus { RunId=Guid.NewGuid().ToString("N"),Mode="cpu",State="RUNNING",StartedUtc=DateTime.UtcNow.ToString("o"),UpdatedUtc=DateTime.UtcNow.ToString("o"),Detail="CPU active" };
                publish(run); session.Poll();
                Assert(!session.CanStart && session.CanStop && !session.Latest.Stale,"Fresh heartbeat gates start and permits stop");
                session.StartRun(new WorkloadRequest());
                Assert(!session.Busy && session.Latest.RunId==run.RunId,"Duplicate inline Go never launches another process");
                run.UpdatedUtc=DateTime.UtcNow.AddSeconds(-30).ToString("o"); publish(run);session.Poll();
                Assert(session.Latest.Stale && session.CanStart,"Stale heartbeat visible; retry remains worker-lock protected");
                run.Phase="baseline";run.PhaseIndex=1;run.PhaseCount=2;run.PhaseSeconds=60;run.PlannedSeconds=660;
                run.PhaseElapsedSeconds=10;run.ElapsedSeconds=10;run.UpdatedUtc=DateTime.UtcNow.ToString("o");publish(run);session.Poll();
                Assert(session.Latest.ActivePhase=="baseline" && session.Latest.Progress(DateTime.UtcNow)>0.16 && session.Latest.Remaining(DateTime.UtcNow)>49,"Baseline phase progress and remaining time derive from worker timestamps");
                Assert(session.Latest.TotalRemaining(DateTime.UtcNow)>649 && session.ExitNeedsChoice,"Total remaining includes the following workload and active exit requires a choice");
                run.UpdatedUtc=DateTime.UtcNow.AddSeconds(-30).ToString("o");publish(run);session.Poll();
                Assert(session.Latest.PhaseElapsed(DateTime.UtcNow)==10,"Stale heartbeat freezes progress");
                run.Mode="science";run.State="COMPLETED";run.Detail="Completed";run.Result=new RunResult { ElapsedSeconds=12,Pi=3.14 };publish(run);session.Poll();
                Assert(session.CanStart && !session.CanStop && !session.Latest.Stale && !session.ExitNeedsChoice,"Completion releases controls and is not stale");
                Assert(session.Message.Contains("Pi") && session.Latest.Result.ElapsedSeconds==12,"Completion notification contains scientific result and duration");
                Assert(session.LastResult!=null && session.Latest.Remaining(DateTime.UtcNow)==0,"Finished result retained and no remaining time shown");
                run.Mode="cpu";run.Result.Pi=0;
                Assert(!run.ScientificPi.HasValue,"Historical CPU Pi zero is hidden by mode-aware presentation");
                run.State="FAILED";run.Result=null;run.Detail="Reserve reached";publish(run);session.Poll();
                Assert(session.Message=="Reserve reached","Failure reaches inline notification");
                File.WriteAllText(path,"{broken"); session.Poll();
                Assert(!session.CanStart && !session.CanStop && session.Latest==null && session.Message.Contains("Cannot read"),"Corrupt status fails closed and removes misleading live state");
                File.Delete(path); Directory.CreateDirectory(path); session.Poll();
                Assert(!session.CanStart && session.Message.Contains("Cannot read"),"Unreadable status is not mistaken for an absent worker");
                Directory.Delete(path);
                run.State="UNKNOWN"; publish(run);session.Poll();
                Assert(!session.CanStart,"Unknown status cannot enable Go");
                run.State="IDLE";run.Detail="Stopped by operator";publish(run);session.Poll();
                Assert(session.CanStart && !session.CanStop && session.Message==run.Detail,"Valid stopped status recovers controls after read error");
            }
        } finally { Directory.Delete(data,true); }
        string fixture=Path.Combine(Path.GetTempPath(),"Matrix export "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
        try {
            string d=Path.Combine(fixture,"data"), local=Path.Combine(fixture,"local"), app=Path.Combine(fixture,"app");Directory.CreateDirectory(d);Directory.CreateDirectory(local);Directory.CreateDirectory(app);
            File.WriteAllText(Path.Combine(d,"config.json"),"{\"StateFile\":\"state.json\"}");
            Directory.CreateDirectory(Path.Combine(d,"workloads"));File.WriteAllText(Path.Combine(d,"workloads","result-test.json"),"{\"State\":\"COMPLETED\"}");
            Directory.CreateDirectory(Path.Combine(d,"workloads","scratch"));File.WriteAllText(Path.Combine(d,"workloads","scratch","private.txt"),"omit");
            File.WriteAllText(Path.Combine(local,"last-error.txt"),new string('x',3*1024*1024));
            File.WriteAllText(Path.Combine(local,"elevation.log"),"fixture launch timing");
            string zip=Path.Combine(fixture,"bundle.zip");Troubleshooting.Export(zip,d,local,app);
            using(var archive=System.IO.Compression.ZipFile.OpenRead(zip)) {
                Assert(archive.GetEntry("data/config.json")!=null && archive.GetEntry("workloads/result-test.json")!=null,"Export collects configuration and results");
                Assert(archive.GetEntry("workloads/scratch/private.txt")==null,"Export excludes workload scratch data");
                Assert(archive.GetEntry("display/last-error.txt").Length==2*1024*1024,"Oversized diagnostics are bounded");
                using(var reader=new StreamReader(archive.GetEntry("REPORT.txt").Open())) Assert(reader.ReadToEnd().Contains("TRUNCATED"),"Export reports truncation");
                Assert(archive.GetEntry("display/elevation.log")!=null,"Export includes per-user elevation diagnostics");
                Assert(archive.GetEntry("power/requests.txt")!=null,"Export includes power diagnostic availability/output");
            }
            string power=Path.Combine(fixture,"power.json");
            File.WriteAllText(power,json.Serialize(new System.Collections.Generic.Dictionary<string,string> { {"CapturedUtc","2026-09-15T00:00:00Z"},{"/requests","fixture DISPLAY and SYSTEM"} }));
            Troubleshooting.Export(zip,d,local,app,true,power,"Elevated fixture");
            using(var archive=System.IO.Compression.ZipFile.OpenRead(zip)) {
                using(var reader=new StreamReader(archive.GetEntry("power/requests.txt").Open())) Assert(reader.ReadToEnd().Contains("fixture DISPLAY"),"Elevated snapshot is included in export");
                using(var reader=new StreamReader(archive.GetEntry("REPORT.txt").Open())) Assert(reader.ReadToEnd().Contains("accepted at export start: True"),"Awake request acceptance explicitly recorded");
            }
            bool rejected=false;try { PowerDiagnostics.PowerCommand("/hibernate off"); } catch(ArgumentException) { rejected=true; }
            Assert(rejected,"Power collector rejects commands that change settings");
            Assert(Directory.GetFiles(fixture,"*.partial-*").Length==0,"Successful overwrite leaves no partial ZIP");
            string directoryTarget=Path.Combine(fixture,"existing-directory");Directory.CreateDirectory(directoryTarget);bool failed=false;
            try { Troubleshooting.Export(directoryTarget,d,local,app); } catch(IOException) { failed=true; }
            Assert(failed && Directory.Exists(directoryTarget) && Directory.GetFiles(fixture,"*.partial-*").Length==0,"Failed export preserves destination and removes partial ZIP");
        } finally { Directory.Delete(fixture,true); }
        ElevationChecks.Run(Assert);
        Console.WriteLine("TOTAL: "+count+" controller checks passed.");
    }
}
