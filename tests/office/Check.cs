using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using MatrixOffice;
using MatrixLabWorkloads;
class Check {
    static int count;
    static void Assert(bool value,string name) { if(!value) throw new Exception(name); count++; Console.WriteLine("PASS: "+name); }
    static void Wait(Func<bool> done) { var timer=System.Diagnostics.Stopwatch.StartNew(); while(!done()) { if(timer.Elapsed.TotalSeconds>5) throw new Exception("Fixture timeout"); Thread.Sleep(10); } }
    static void Main() {
        int activationAttempts=0,activationWaits=0,activationStages=0,activationSnapshots=0; object activationToken=new object();
        Func<object> activationStartup=()=>{ activationAttempts++; if(activationAttempts==1) throw new System.Runtime.InteropServices.COMException("server",unchecked((int)0x80080005)); return activationToken; };
        object activationRecovered=MatrixOfficeActor.OfficeStartup.Create("Excel",activationStartup,ms=>{Assert(ms==2000,"Startup backoff is two seconds");activationWaits++;},_=>activationStages++,_=>{},()=>activationSnapshots++);
        Assert(Object.ReferenceEquals(activationToken,activationRecovered) && activationAttempts==2 && activationWaits==1,"Transient COM activationStartup recovers on exactly one retry");
        Assert(activationStages==2 && activationSnapshots==3,"Each activation attempt has a watchdog stage and diagnostic activationSnapshots");
        activationAttempts=0; activationWaits=0; bool activationStartupfailed=false;
        try { MatrixOfficeActor.OfficeStartup.Create("Excel",()=>{activationAttempts++;throw new System.Runtime.InteropServices.COMException("server",unchecked((int)0x80080005));},_=>activationWaits++,_=>{},_=>{},()=>{}); }
        catch(InvalidOperationException e) {activationStartupfailed=e.InnerException is System.Runtime.InteropServices.COMException && e.Message.Contains("Excel") && e.Message.Contains("80080005") && e.Message.Contains("export logs");}
        Assert(activationStartupfailed && activationAttempts==2 && activationWaits==1,"Persistent activationStartup failure is bounded and preserves actionable HRESULT/cause");
        foreach(int code in new[]{unchecked((int)0x80070005),unchecked((int)0x80040154)}) {
            activationAttempts=0;activationWaits=0;
            try {MatrixOfficeActor.OfficeStartup.Create("PowerPoint",()=>{activationAttempts++;throw new System.Runtime.InteropServices.COMException("permanent",code);},_=>activationWaits++,_=>{},_=>{},()=>{});}catch(InvalidOperationException){}
            Assert(activationAttempts==1 && activationWaits==0,"Non-transient activation errors are not retried: "+code);
        }
        activationAttempts=0;activationStages=0;activationSnapshots=0;
        Assert(Object.ReferenceEquals(activationToken,MatrixOfficeActor.OfficeStartup.Create("Excel",()=>{activationAttempts++;return activationToken;},_=>{throw new Exception("Unexpected retry");},_=>activationStages++,_=>{},()=>activationSnapshots++)) && activationAttempts==1 && activationStages==1 && activationSnapshots==1,"Healthy activation has no delay or extra activationAttempts");
        int focusCalls=0,unlocks=0,permissions=0;
        Assert(!ForegroundFallback.Run(()=>false,()=>{focusCalls++;return true;},()=>{unlocks++;return true;}) && focusCalls==0 && unlocks==0,"No focus or injected input while user/desktop guard rejects automation");
        Assert(ForegroundFallback.Run(()=>true,()=>{focusCalls++;return true;},()=>{unlocks++;return true;}) && focusCalls==1 && unlocks==0,"Normal foreground success sends no Alt fallback");
        focusCalls=0;
        Assert(ForegroundFallback.Run(()=>true,()=>++focusCalls==2,()=>{unlocks++;return true;}) && focusCalls==2 && unlocks==1,"Denied foreground request recovers after one fallback");
        focusCalls=0;unlocks=0;
        Assert(!ForegroundFallback.Run(()=>true,()=>{focusCalls++;return false;},()=>{unlocks++;return true;}) && focusCalls==2 && unlocks==1,"Persistent denial stays bounded and reports failure");
        focusCalls=0;unlocks=0;
        Assert(!ForegroundFallback.Run(()=>true,()=>{focusCalls++;return false;},()=>{unlocks++;return false;}) && focusCalls==1 && unlocks==1,"Rejected input injection does not retry activation");
        focusCalls=0;unlocks=0;
        Assert(!ForegroundFallback.Run(()=>++permissions==1,()=>{focusCalls++;return false;},()=>{unlocks++;return true;}) && focusCalls==1 && unlocks==0,"User activity arriving before fallback cancels injected input");
        permissions=0;focusCalls=0;
        Assert(!ForegroundFallback.Run(()=>++permissions<3,()=>{focusCalls++;return false;},()=>true) && focusCalls==1,"User activity arriving after fallback cancels activation");
        var inputClock=new SyntheticInputClock();
        Assert(inputClock.UserTick(50)==50,"Human input remains unchanged without synthetic events");
        inputClock.Record(100,50); Assert(inputClock.UserTick(100)==50,"Own Alt input does not trigger a false interaction pause");
        Assert(inputClock.UserTick(101)==101,"Subsequent human input still pauses automation");
        inputClock.Record(110,100); Assert(inputClock.UserTick(110)==50,"Consecutive synthetic events preserve original user idle time");
        Assert(OfficeRequirements.Met(true,true,true) && OfficeRequirements.Missing(true,true,true)=="","All Microsoft 365 desktop prerequisites are recognized");
        Assert(!OfficeRequirements.Met(false,true,false) && OfficeRequirements.Missing(false,true,false)=="Word, PowerPoint","Missing Office prerequisites are identified precisely");
        string draftSubject=OutlookDraftPlan.Subject(42),draftBody=OutlookDraftPlan.Body(42);
        Assert(draftSubject.Length>10 && draftBody.Contains("WORKING DRAFT") && draftBody.Contains("Next step:"),"Outlook draft planner creates a deterministic synthetic subject and body");
        var focusTargets=new[]{"Excel","PowerPoint","PowerPoint","Outlook","Browser","Browser"}.Select((name,i)=>new System.Collections.Generic.KeyValuePair<IntPtr,string>(new IntPtr(i+1),name));
        Assert(FocusRotation.OnePerApplication(focusTargets).Select(t=>t.Value).SequenceEqual(new[]{"Excel","PowerPoint","Outlook","Browser","Browser"}),"Multiple presentations do not get extra rotation turns; both browsers are retained");
        string testProfile=@"C:\Users\Test User\OfficeRun\EdgeProfile";
        Assert(BrowserProfile.Matches(new[]{"edge.exe","--user-data-dir="+testProfile},testProfile),"Exact isolated profile argument is accepted");
        Assert(BrowserProfile.Matches(new[]{"edge.exe","--user-data-dir",testProfile.ToUpperInvariant()+@"\"},testProfile),"Separate profile argument supports spaces, casing and trailing separator");
        Assert(!BrowserProfile.Matches(new[]{"edge.exe","https://site/?--user-data-dir="+testProfile},testProfile),"URL text cannot establish browser ownership");
        Assert(!BrowserProfile.Matches(new[]{"edge.exe","--user-data-dir="+testProfile+"-other"},testProfile),"Similar profile prefix cannot claim a personal browser");
        Assert(!BrowserProfile.Matches(new[]{"edge.exe","--user-data-dir="+testProfile,"--user-data-dir=other"},testProfile),"Ambiguous duplicate profile switches are rejected");
        Assert(!BrowserProfile.Matches(new[]{"edge.exe","--user-data-dir"},testProfile),"Missing profile argument is rejected");
        double discoveryTime=0; int discoveryCalls=0;
        var discovered=BrowserWindowWait.Run(()=>{discoveryCalls++;return discoveryCalls==4 ? new System.Collections.Generic.List<IntPtr>{new IntPtr(222)} : new System.Collections.Generic.List<IntPtr>();},()=>discoveryTime,()=>discoveryTime+=0.5);
        Assert(discovered.Single()==new IntPtr(222) && discoveryCalls==4,"Delayed replacement browser window is found without requiring a live launcher");
        discoveryTime=0;discoveryCalls=0;
        discovered=BrowserWindowWait.Run(()=>{discoveryCalls++;return new System.Collections.Generic.List<IntPtr>();},()=>discoveryTime,()=>discoveryTime+=0.5);
        Assert(discovered.Count==0 && discoveryCalls==60 && discoveryTime==30,"Missing owned browser window fails after bounded discovery");
        discoveryTime=0;discoveryCalls=0;
        discovered=BrowserWindowWait.Run(()=>{discoveryCalls++;return new System.Collections.Generic.List<IntPtr>{new IntPtr(123)};},()=>discoveryTime,()=>discoveryTime+=0.5);
        Assert(discoveryCalls==1 && discoveryTime==0,"Immediate verified browser window requires no startup delay");
        var plan=OfficePlan.Create(21600,42); var other=OfficePlan.Create(21600,43);
        var json=new JavaScriptSerializer();
        Assert(json.Serialize(plan)==json.Serialize(OfficePlan.Create(21600,42)),"Recorded seed reproduces the full six-hour action schedule");
        Assert(json.Serialize(plan)!=json.Serialize(other),"Different runs vary timing and action selection");
        Assert(plan.First().Action=="open-apps" && plan.First().AtSeconds==0,"Real app startup is the first workload action");
        Assert(plan.Zip(plan.Skip(1),(a,b)=>a.AtSeconds<b.AtSeconds).All(x=>x) && plan.Last().AtSeconds<21600,"Six-hour schedule is ordered and bounded");
        foreach(string name in new[]{"create-text","create-csv","create-excel","create-powerpoint","edit-text","edit-csv","edit-excel","edit-powerpoint","browse","view-outlook","draft-outlook","break","restart-apps"})
            Assert(plan.Any(x=>x.Action==name),"Six-hour schedule includes "+name);
        Assert(plan.Count(x=>x.Action=="restart-apps")==3,"Apps restart three times across the six-hour session");
        Assert(plan.Any(x=>x.Action=="break" && plan[plan.IndexOf(x)+1].AtSeconds-x.AtSeconds>=600),"Breaks leave applications open for at least ten minutes");
        Assert(OfficePlan.Create(60,42).Any(x=>x.Action=="edit-excel") && OfficePlan.Create(60,42).Any(x=>x.Action=="edit-powerpoint"),"Short pilot includes both Office document creation and editing");
        bool invalid=false;try{OfficePlan.Create(28801,1);}catch(ArgumentException){invalid=true;} Assert(invalid,"Office schedule rejects durations beyond eight hours");
        new MatrixRainLab.WorkloadRequest { Mode="office", Seconds=21600 }.Validate(); Assert(true,"UI request supports six hours plus baseline");
        new Options { Mode="office", Seconds=21600 }.Validate(); Assert(true,"Worker accepts the same six-hour duration");
        var rotation=new MatrixRainLab.TestRotation(1,null);Assert(Enumerable.Range(0,100).Select(_=>rotation.Next()).All(m=>m!="office"),"Long Office session never enters short random rotation");
        var rotationPolicy=new MatrixRainLab.RotationPlan(923,12);
        var phases=Enumerable.Range(0,1200).Select(_=>rotationPolicy.Next()).ToArray();
        Assert(phases.All(p=>p.Mode!="office" && p.Mode!="baseline" && p.Mode!="rotation"),"Continuous rotation excludes Microsoft 365 and dark baseline");
        Assert(phases.Where((p,i)=>i%2==1).All(p=>p.Mode=="visualization"),"Every random activity has a screensaver-only recovery phase");
        Assert(phases.Any(p=>p.Mode=="windows"),"Windows Workday participates in rotation");
        Assert(phases.All(p=>p.Seconds>=180 && p.Seconds<=720 && p.Intensity<=55 && p.Threads<=2 && p.MemoryMiB<=512 && p.WriteLimitMiB==128 && p.DiskMiBPerSecond==2),"Rotation pressure and duration caps apply across 1200 phases");
        Assert(phases.Select(p=>p.Seconds).Distinct().Count()>100 && phases.Select(p=>p.Intensity).Distinct().Count()>20,"Rotation varies duration and duty cycle");
        foreach(var p in phases) p.Validate(); Assert(true,"Every generated phase passes worker request validation");
        new MatrixRainLab.WorkloadRequest { Mode="windows",Seconds=21600 }.Validate(); Assert(true,"Windows Workday accepts a six-hour standalone run");
        var selections=Enumerable.Range(0,100).Select(seed=>BrowserPlan.Choose(true,true,seed)).ToArray();
        Assert(selections.Any(x=>x.Length==2) && selections.Any(x=>x.Length==1 && x[0].Name=="Edge") && selections.Any(x=>x.Length==1 && x[0].Name=="Chrome"),"Browser selection covers Edge, Chrome, and both");
        Assert(selections.SelectMany(x=>x).All(x=>x.Tabs>=8 && x.Tabs<=15),"Every selected browser requests eight to fifteen tabs");
        Assert(BrowserPlan.Choose(true,false,42).Single().Name=="Edge","Missing Chrome falls back to installed Edge");
        Assert(BrowserPlan.Choose(false,true,42).Single().Name=="Chrome","Missing Edge falls back to installed Chrome");
        Assert(json.Serialize(BrowserPlan.Choose(true,true,42))==json.Serialize(BrowserPlan.Choose(true,true,42)),"Browser choices reproduce from the recorded seed");
        bool noBrowser=false;try{BrowserPlan.Choose(false,false,42);}catch(InvalidOperationException){noBrowser=true;}Assert(noBrowser,"Missing both browsers fails explicitly");
        int attempts=0, waits=0;
        Assert(StatusFile.ReadWithRetry(delegate { if(++attempts<4) throw new IOException("sharing conflict"); return "complete snapshot"; },delegate { waits++; })=="complete snapshot" && attempts==4 && waits==3,"Status read recovers from transient sharing conflicts");
        attempts=0; bool bounded=false;
        try { StatusFile.ReadWithRetry(delegate { attempts++; throw new IOException("persistent conflict"); },delegate {}); } catch(IOException) { bounded=true; }
        Assert(bounded && attempts==21,"Persistent status I/O failure remains visible after bounded retries");
        attempts=0; bool denied=false;
        try { StatusFile.ReadWithRetry(delegate { attempts++; throw new UnauthorizedAccessException(); },delegate {}); } catch(UnauthorizedAccessException) { denied=true; }
        Assert(denied && attempts==1,"Access denied is not hidden or retried as a sharing conflict");
        int missingWaits=0; bool missing=false;
        try { StatusFile.ReadWithRetry(delegate { throw new FileNotFoundException(); },delegate { missingWaits++; }); } catch(FileNotFoundException) { missing=true; }
        Assert(missing && missingWaits==0,"Missing optional status returns immediately without delaying Go");
        string snapshot=Path.GetTempFileName();
        try { File.WriteAllText(snapshot,"complete snapshot"); Assert(StatusFile.Read(snapshot)=="complete snapshot","Production status reader reads the full snapshot"); }
        finally { File.Delete(snapshot); }
        var fake=new FakePowerPoint(); MatrixOfficeActor.PowerPointAutomation.Start(fake);
        Assert(fake.Visible==-1 && fake.AutomationSecurity==3,"PowerPoint startup succeeds on an object without HWND");
        MatrixOfficeActor.PowerPointAutomation.Activate(fake);
        Assert(fake.Windows.Window.Activated,"PowerPoint activates the presentation document window without HWND");
        fake.Windows.Window.WindowState=2;
        MatrixOfficeActor.PowerPointAutomation.Restore(fake);
        Assert(fake.Windows.Window.WindowState==1,"PowerPoint document restore clears minimized state without HWND");
        int switches=0; bool restored=false;
        Assert(ForegroundSwitch.Run(delegate {restored=true;},delegate {Assert(restored,"Window is restored before foreground activation");switches++;},()=>true,delegate {}) && switches==1,"Confirmed foreground switch stops retrying");
        switches=0;
        Assert(!ForegroundSwitch.Run(delegate {},delegate {switches++;},()=>false,delegate {}) && switches==3,"Windows foreground denial is reported after bounded attempts");
        attempts=0; waits=0;
        Assert(StatusFile.ReadWithRetry(delegate {if(++attempts<4) throw new FileNotFoundException("run.json"); return "fresh authorization";},delegate {waits++;},true)=="fresh authorization" && waits==3,"Required active-run status survives a transient missing snapshot");
        attempts=0; bool gone=false;
        try {StatusFile.ReadWithRetry(delegate {attempts++;throw new FileNotFoundException("run.json");},delegate {},true);} catch(FileNotFoundException) {gone=true;}
        Assert(gone && attempts==21,"Persistently missing authorization still fails after bounded retries");
        var cleanupErrors=new System.Collections.Generic.List<Exception>(); bool otherClosed=false;
        CleanupSteps.Try("Save workbook",delegate {throw new IOException("save rejected");},cleanupErrors);
        CleanupSteps.Try("Close presentation",delegate {otherClosed=true;},cleanupErrors);
        Assert(otherClosed && cleanupErrors.Count==1,"A failed cleanup step does not prevent another application cleanup");
        string detail=CleanupSteps.Preserve("Original missing run.json",new AggregateException("cleanup incomplete",cleanupErrors));
        Assert(detail.Contains("Original missing run.json") && detail.Contains("Save workbook") && detail.Contains("save rejected"),"Cleanup reports preserve original failure and named inner exceptions");
        var sites=BrowserSites.Select(BrowserSites.Defaults,15,42);
        Assert(sites.Length==15 && sites.Take(8).Distinct().Count()==8,"Live tab plan includes all eight default sites before repeats");
        Assert(sites.Contains("https://www.foxnews.com/") && sites.Contains("https://www.cnn.com/") && sites.Contains("https://gizmodo.com/") && sites.Contains("https://aftersixcomputers.com/"),"Every default browser plan includes the four requested websites");
        Assert(sites.SequenceEqual(BrowserSites.Select(BrowserSites.Defaults,15,42)),"Live website selection is reproducible from its seed");
        Assert(BrowserSites.Select(new[]{"https://example.com/"},8,1).All(x=>x=="https://example.com/"),"Short custom site lists fill requested tab count");
        foreach(string bad in new[]{"file:///C:/test","javascript:alert(1)","https://user:secret@example.com/","https://example.com/"+(char)34+" --flag","https://example.com/"+(char)10}) {
            bool rejected=false;try {BrowserSites.Select(new[]{bad},8,1);}catch(ArgumentException){rejected=true;}Assert(rejected,"Invalid browser URL rejected: "+bad.Split(':')[0]);
        }
        bool emptySites=false;try {BrowserSites.Select(new string[0],8,1);}catch(ArgumentException){emptySites=true;}Assert(emptySites,"Empty browser site configuration fails explicitly");
        Bridge(false,false); Bridge(true,false); Bridge(false,true);
        Console.WriteLine("TOTAL: "+count+" Office planner/IPC checks passed.");
    }
    static void Bridge(bool stop,bool fail) {
        // Short fixture name fits macOS Unix-domain socket path limits; Windows runs use full GUIDs.
        string id=Guid.NewGuid().ToString("N").Substring(0,8),sid="test-user";
        using(var bridge=new OfficeBridge(id,sid))
        using(var client=new NamedPipeClientStream(".","AfterSix.MatrixOffice."+id,PipeDirection.Out)) {
            client.Connect(5000);
            using(var writer=new StreamWriter(client){AutoFlush=true}) {
                var p=new OfficeProgress {UserSid=sid,Pid=123,State="ACTIVE",Activity="Editing workbook",Created=2,Saved=3,Actions=4,BytesSaved=1234};
                var json=new JavaScriptSerializer(); writer.WriteLine(json.Serialize(p)); Wait(()=>bridge.Progress!=null);
                Assert(bridge.Progress.Saved==3 && bridge.Progress.BytesSaved==1234,"User-helper activity reaches the elevated coordinator");
                p.State="CLEANING"; p.Error="Original failure pending cleanup"; writer.WriteLine(json.Serialize(p)); Wait(()=>bridge.Progress.State=="CLEANING");
                bridge.Check(); Assert(bridge.Progress.State=="CLEANING","Coordinator keeps connection open for final cleanup report");
                bool requested=stop;
                var thread=new Thread(delegate() { Thread.Sleep(250); p.State=fail ? "FAILED" : "COMPLETED"; p.Error=fail ? "Excel action failed | Cleanup: final save failed" : null; writer.WriteLine(json.Serialize(p)); });thread.Start();
                if(fail) {
                    bool rejected=false;try {bridge.Run(new Options{Mode="office",Seconds=60},()=>false,r=>{});}catch(InvalidOperationException e){rejected=e.Message.Contains("Excel action failed") && e.Message.Contains("final save failed");}
                    Assert(rejected,"Helper failures become failed workloads rather than fake success");
                } else {
                    var result=bridge.Run(new Options{Mode="office",Seconds=60},()=>requested,r=>{});
                    Assert(result.Office.Saved==3 && result.Iterations==4 && result.Reason==(stop ? "stopped by operator" : "office workday completed"),stop ? "Stop waits for the helper cleanup acknowledgement" : "Completion preserves real reported file/action counters");
                }
                thread.Join();
            }
        }
    }
}

public class FakePowerPoint {
    public int Visible {get;set;}
    public int AutomationSecurity {get;set;}
    public FakeWindows Windows {get;set;} = new FakeWindows();
}
public class FakeWindows {
    public FakeWindow Window=new FakeWindow();
    public FakeWindow this[int index] {get {if(index!=1) throw new Exception("Wrong document window"); return Window;}}
}
public class FakeWindow { public int WindowState {get;set;} public bool Activated; public void Activate() {Activated=true;} }
