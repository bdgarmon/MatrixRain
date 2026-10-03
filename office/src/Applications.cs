using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using MatrixOffice;
namespace MatrixOfficeActor {
    internal sealed class Applications : IDisposable {
        readonly Random random;
        readonly string desktop, documents, local, marker, edge, chrome, outlook;
        readonly BrowserChoice[] browserPlan;
        readonly string[] browserSites;
        bool browsersOpened;
        readonly OfficeProgress progress;
        readonly Action<string> log;
        readonly Action<string> startupStage;
        readonly Dictionary<string,List<string>> files=new Dictionary<string,List<string>>();
        readonly Dictionary<string,object> books=new Dictionary<string,object>(), decks=new Dictionary<string,object>();
        readonly Dictionary<int,long> ownedProcesses=new Dictionary<int,long>();
        readonly object processGate=new object();
        readonly List<IntPtr> browserWindows=new List<IntPtr>();
        IntPtr outlookWindow;
        bool outlookOwned, outlookRequested, outlookReady;
        DateTime outlookDeadline, outlookWarmUntil;
        HashSet<IntPtr> outlookBefore;
        DateTime nextFocus=DateTime.MinValue;
        int focusIndex;
        readonly List<IntPtr> presentationWindows=new List<IntPtr>();
        dynamic excel, powerpoint;
        bool quitPowerpoint, quitExcel;
        HashSet<int> powerpointBefore;
        int serial;
        public Applications(string run,string output,OfficeProgress status,int seed,Action<string> logger,string dataDirectory,Action<string> stage) {
            startupStage=stage; random=new Random(seed); progress=status; local=output; log=logger; marker="MatrixLab "+run.Substring(0,8);
            bool word=Type.GetTypeFromProgID("Word.Application")!=null, excelInstalled=Type.GetTypeFromProgID("Excel.Application")!=null, powerpointInstalled=Type.GetTypeFromProgID("PowerPoint.Application")!=null;
            if(!OfficeRequirements.Met(word,excelInstalled,powerpointInstalled)) {
                string missing=OfficeRequirements.Missing(word,excelInstalled,powerpointInstalled);
                progress.Activity="Office prerequisites missing: "+missing;
                progress.Error="Office Workday was not successful because required Microsoft 365 desktop applications are missing: "+missing+".";
                LaunchOfficeRequirementNotice(run,missing);
                throw new InvalidOperationException(progress.Error);
            }
            edge=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}
                .Select(p=>Path.Combine(p,"Microsoft","Edge","Application","msedge.exe")).FirstOrDefault(File.Exists);
            chrome=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}
                .Select(p=>Path.Combine(p,"Google","Chrome","Application","chrome.exe")).FirstOrDefault(File.Exists);
            string siteFile=Path.Combine(dataDirectory,"browser-sites.json");
            browserSites=File.Exists(siteFile) ? new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<string[]>(File.ReadAllText(siteFile)) : BrowserSites.Defaults;
            BrowserSites.Select(browserSites,8,seed); // Validate before launching any application.
            browserPlan=BrowserPlan.Choose(edge!=null,chrome!=null,seed);
            progress.Browsers=String.Join(" + ",browserPlan.Select(b=>b.Name).ToArray());
            progress.BrowserTabsRequested=browserPlan.Sum(b=>b.Tabs);
            log("Browser availability: Edge="+(edge!=null)+", Chrome="+(chrome!=null)+"; selected="+progress.Browsers+"; requested tabs="+progress.BrowserTabsRequested);
            dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
            dynamic folder=shell.NameSpace("shell:AppsFolder"); dynamic items=folder.Items();
            try {
                for(int i=0;i<(int)items.Count;i++) {
                    dynamic item=items.Item(i);
                    try { string id=(string)item.Path; if(!String.IsNullOrEmpty(id) && id.StartsWith("Microsoft.OutlookForWindows_",StringComparison.OrdinalIgnoreCase)) { outlook=id; break; } }
                    finally { Release((object)item); }
                }
            } finally { Release((object)items); Release((object)folder); Release((object)shell); }
            if(outlook==null) throw new InvalidOperationException("New Outlook is not registered for the signed-in user. Install it and complete first-run sign-in before this test.");
            desktop=Root(Environment.SpecialFolder.DesktopDirectory,run);
            documents=Root(Environment.SpecialFolder.MyDocuments,run);
            progress.Desktop=desktop; progress.Documents=documents;
            foreach(string type in new[]{"txt","csv","xlsx","pptx","html"}) files[type]=new List<string>();
            log("Resolved Desktop="+desktop+"; Documents="+documents+"; Edge="+edge+"; Outlook="+outlook);
        }
        void LaunchOfficeRequirementNotice(string run,string missing) {
            string folder=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string path=Path.Combine(folder,"MatrixRain-Office-Configuration-Required-"+run.Substring(0,8)+".txt");
            const string message="This PC is not properly configured for this test. Install Microsoft 365 apps and retry.";
            File.WriteAllText(path,message+Environment.NewLine+Environment.NewLine+"Missing: "+missing+Environment.NewLine);
            Process.Start(new ProcessStartInfo("notepad.exe","\""+path.Replace("\"","\"\"")+"\"") {UseShellExecute=true});
            log("OFFICE REQUIREMENTS MISSING: "+missing+"; opened Notepad warning at "+path);
        }
        static string Root(Environment.SpecialFolder known,string id) {
            string root=Environment.GetFolderPath(known);
            if(String.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) throw new DirectoryNotFoundException("User known folder unavailable: "+known);
            string parent=Path.Combine(root,"MatrixRainLab-Office");
            if(Directory.Exists(parent) && (File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0) throw new IOException("Test subfolder must not be a link: "+parent);
            string path=Path.Combine(parent,id); if(Directory.Exists(path)) throw new IOException("Run folder already exists; refusing to reuse it: "+path);
            Directory.CreateDirectory(path); return path;
        }
        public void Execute(string action) {
            Guard();
            if(action=="open-apps") { Browse(); OpenOffice(); OpenOutlook(); }
            else if(action=="restart-apps") { CloseOffice(); OpenOffice(); log("Browser and Outlook windows retained during Office restart."); }
            else if(action=="break") { log("Reading/break interval: applications remain open."); return; }
            else if(action=="browse") Browse();
            else if(action=="view-outlook") { OpenOutlook(); if(outlookReady && outlookWindow!=IntPtr.Zero) Focus(outlookWindow,"Outlook"); }
            else if(action=="draft-outlook") { OpenOutlook(); if(outlookReady && outlookWindow!=IntPtr.Zero && Focus(outlookWindow,"Outlook")) { if(OutlookDraft.TryCreateAndClose(outlookWindow,random.Next(),log)) progress.OutlookDrafts++; } }
            else {
                string type=action.EndsWith("excel") ? "xlsx" : action.EndsWith("powerpoint") ? "pptx" : action.EndsWith("csv") ? "csv" : "txt";
                bool create=(action.StartsWith("create-") && files[type].Count<12) || files[type].Count==0;
                string path=create ? NewPath(type) : files[type][random.Next(files[type].Count)];
                if(type=="xlsx") Spreadsheet(path,create);
                else if(type=="pptx") Presentation(path,create);
                else TextFile(path,type,create);
                Saved(path,create); if(create) files[type].Add(path);
            }
            progress.Actions++;
        }
        void Guard() {
            if(progress.BytesSaved>=8L*1024*1024*1024) throw new IOException("Office cumulative save budget reached (8 GiB).");
            foreach(string path in new[]{desktop,documents}) {
                ulong available,total,free;
                if(!Native.GetDiskFreeSpaceEx(path,out available,out total,out free)) throw new IOException("Cannot measure free space for test folder: "+path);
                if(available<2UL*1024*1024*1024) throw new IOException("Office free-space reserve reached (2 GiB): "+path);
            }
        }
        string NewPath(string type) {
            serial++;
            // Alternate locations so even a short run exercises both known folders.
            string root=serial%2==1 ? desktop : documents;
            return Path.Combine(root,marker.Replace(" ","-")+"-Project-"+serial.ToString("D3")+"-"+random.Next(1000,9999)+"."+type);
        }
        void Saved(string path,bool created) {
            long size=new FileInfo(path).Length;
            progress.BytesSaved+=size; progress.Saved++; if(created) progress.Created++;
            log((created ? "CREATE " : "UPDATE ")+path+" / "+size+" bytes");
        }
        void TextFile(string path,string type,bool create) {
            if(!create && !files[type].Contains(path)) throw new InvalidOperationException("Path is not owned by this run.");
            int[] sizes={4,32,128,512,2048,8192}; int target=sizes[random.Next(sizes.Length)]*1024;
            using(var writer=new StreamWriter(new FileStream(path,create ? FileMode.CreateNew : FileMode.Create,FileAccess.Write,FileShare.Read))) {
                int written=0,row=0;
                string header=type=="csv" ? "Item,Department,Amount,Note\r\n" : "Matrix Rain synthetic project notes / "+DateTime.UtcNow.ToString("o")+"\r\n";
                writer.Write(header); written+=header.Length;
                while(written<target) {
                    string line=type=="csv" ? (++row)+",Operations,"+random.Next(10,100000)+","+Guid.NewGuid().ToString("N")+"\r\n" :
                        "Project update "+(++row)+": review schedule, budget and next steps. Reference "+Guid.NewGuid().ToString("N")+"\r\n";
                    writer.Write(line); written+=line.Length;
                }
            }
        }
        object StartOffice(string name,string progId,string processName) {
            return OfficeStartup.Create(name,()=>Activator.CreateInstance(Type.GetTypeFromProgID(progId)),
                Thread.Sleep,startupStage,log,()=>OfficeStartupDiagnostics.Capture(progId,processName,log));
        }
        void OpenOffice() {
            if(excel==null) {
                var before=Native.ProcessIds("EXCEL"); excel=StartOffice("Excel","Excel.Application","EXCEL");
                excel.Visible=true; excel.DisplayAlerts=false; excel.AutomationSecurity=3;
                int pid=Native.WindowPid(new IntPtr((int)excel.Hwnd)); quitExcel=!before.Contains(pid); if(quitExcel) TrackProcess(pid);
                progress.AppOpens++; log("Opened Excel window / PID "+pid);
            }
            if(powerpoint==null) {
                powerpointBefore=Native.ProcessIds("POWERPNT"); powerpoint=StartOffice("PowerPoint","PowerPoint.Application","POWERPNT");
                PowerPointAutomation.Start(powerpoint);
                progress.AppOpens++; log("Opened PowerPoint through COM; process ownership deferred until a generated presentation is visible.");
            }
        }
        void Spreadsheet(string path,bool create) {
            OpenOffice(); dynamic book;
            if(!books.ContainsKey(path)) {
                book=create ? excel.Workbooks.Add() : excel.Workbooks.Open(path,0,false);
                books[path]=book;
            } else book=books[path];
            dynamic sheet=book.Worksheets[1];
            try {
                int rows=new[]{50,500,2500,10000}[random.Next(4)]; var data=new object[rows,6];
                for(int i=0;i<rows;i++) { data[i,0]="Item "+(i+1); data[i,1]="Team "+random.Next(1,10); data[i,2]=random.Next(1,500); data[i,3]=random.Next(10,5000)/10.0; data[i,4]=Guid.NewGuid().ToString("N"); data[i,5]=DateTime.UtcNow.ToString("s"); }
                sheet.Cells.Clear(); dynamic range=sheet.Range["A2","F"+(rows+1)];
                try { range.Value2=data; } finally { Release((object)range); }
                sheet.Cells[1,1]="Item"; sheet.Cells[1,2]="Team"; sheet.Cells[1,3]="Quantity"; sheet.Cells[1,4]="Unit cost"; sheet.Cells[1,5]="Reference"; sheet.Cells[1,6]="Updated UTC";
                sheet.Cells[1,8]="Total quantity"; sheet.Cells[2,8].Formula="=SUM(C2:C"+(rows+1)+")";
                sheet.Columns["A:H"].AutoFit(); book.Activate(); excel.Calculate();
                if(create) book.SaveAs(path,51); else book.Save();
                Focus(new IntPtr((int)excel.Hwnd),"Excel");
                // Keep at most two generated workbooks open concurrently.
                foreach(string key in books.Keys.Where(k=>k!=path).Take(Math.Max(0,books.Count-2)).ToArray()) { dynamic old=books[key]; old.Close(false); Release((object)old); books.Remove(key); }
            } finally { Release((object)sheet); }
        }
        void Presentation(string path,bool create) {
            OpenOffice(); dynamic deck;
            if(!decks.ContainsKey(path)) { deck=create ? powerpoint.Presentations.Add(-1) : powerpoint.Presentations.Open(path,0,0,-1); decks[path]=deck; }
            else deck=decks[path];
            while(deck.Slides.Count>0) deck.Slides[1].Delete();
            int slides=random.Next(3,13);
            string image=Path.Combine(local,"chart.png");
            using(var bitmap=new Bitmap(random.Next(2)==0 ? 640 : 1600,900)) {
                using(var graphics=Graphics.FromImage(bitmap)) {
                    graphics.Clear(Color.White);
                    using(var font=new Font("Segoe UI",20)) graphics.DrawString("Synthetic quarterly activity",font,Brushes.DarkGreen,24,20);
                    for(int i=0;i<100;i++) using(var brush=new SolidBrush(Color.FromArgb(random.Next(255),random.Next(255),random.Next(255)))) graphics.FillRectangle(brush,i*(bitmap.Width/100),100+random.Next(250),Math.Max(2,bitmap.Width/100-2),random.Next(50,450));
                }
                bitmap.Save(image,System.Drawing.Imaging.ImageFormat.Png);
            }
            for(int i=0;i<slides;i++) {
                dynamic slide=deck.Slides.Add(i+1,12);
                try {
                    dynamic title=slide.Shapes.AddTextbox(1,30,20,650,70); title.TextFrame.TextRange.Text="Project review "+(i+1)+" / "+DateTime.Now.ToString("t"); Release((object)title);
                    dynamic picture=slide.Shapes.AddPicture(image,0,-1,30,100,500,280); Release((object)picture);
                    dynamic note=slide.Shapes.AddTextbox(1,30,400,650,90); note.TextFrame.TextRange.Text="Forecast: "+random.Next(100,900)+" units. Next action: review and publish internal plan. "+Guid.NewGuid().ToString("N"); Release((object)note);
                } finally { Release((object)slide); }
            }
            if(create) deck.SaveAs(path,24); else deck.Save();
            PowerPointAutomation.Restore(deck);
            // The saved document name contains this run's unique marker. Never claim a
            // pre-existing process, and never rely on Application.HWND being exposed.
            var matches=Native.Windows("POWERPNT").Where(h=>Native.Title(h).IndexOf(Path.GetFileNameWithoutExtension(path),StringComparison.OrdinalIgnoreCase)>=0).ToList();
            foreach(var h in matches) if(!presentationWindows.Contains(h)) presentationWindows.Add(h);
            if(matches.Count>0) Focus(matches[0],"PowerPoint");
            var pids=matches.Select(Native.WindowPid).Distinct().ToArray();
            if(pids.Length==1 && pids[0]>0 && !powerpointBefore.Contains(pids[0])) {
                quitPowerpoint=true; TrackProcess(pids[0]);
                log("Verified generated PowerPoint document / PID "+pids[0]);
            } else log("PowerPoint ownership not proven; cleanup will close generated documents only.");
            foreach(string key in decks.Keys.Where(k=>k!=path).Take(Math.Max(0,decks.Count-2)).ToArray()) { dynamic old=decks[key]; old.Close(); Release((object)old); decks.Remove(key); }
        }
        void OpenOutlook() {
            if(outlookRequested) return;
            outlookBefore=new HashSet<IntPtr>(Native.Windows("olk"));
            outlookRequested=true; outlookDeadline=DateTime.UtcNow.AddMinutes(5);
            progress.OutlookPreparation="Waiting up to five minutes for an Outlook window";
            log(progress.OutlookPreparation+"; account sign-in is manual, never automated.");
            Process.Start(new ProcessStartInfo("explorer.exe","shell:AppsFolder\\"+outlook) {UseShellExecute=true});
        }
        public bool OutlookPreparing { get { return outlookRequested && !outlookReady; } }
        public bool PrepareOutlook() {
            if(!outlookRequested) return false;
            if(outlookWindow!=IntPtr.Zero && !Native.Exists(outlookWindow)) {
                outlookWindow=IntPtr.Zero; outlookReady=false; outlookOwned=false;
                outlookDeadline=DateTime.UtcNow.AddMinutes(5);
                log("Outlook window changed or closed; waiting for its replacement.");
            }
            if(outlookReady) return false;
            if(outlookWindow==IntPtr.Zero) {
                var now=Native.Windows("olk");
                outlookWindow=now.FirstOrDefault(h=>!outlookBefore.Contains(h));
                if(outlookWindow!=IntPtr.Zero) {
                    if(!Native.Tag(outlookWindow,marker)) throw new IOException("Cannot mark ownership of the test-opened Outlook window.");
                    outlookOwned=true; progress.AppOpens++;
                } else outlookWindow=now.FirstOrDefault(h=>outlookBefore.Contains(h));
                if(outlookWindow==IntPtr.Zero) {
                    if(DateTime.UtcNow>=outlookDeadline) throw new InvalidOperationException("Outlook did not expose a window within five minutes. Complete its first-run setup before retrying.");
                    return true;
                }
                outlookWarmUntil=DateTime.UtcNow.AddMinutes(2);
                Focus(outlookWindow,"Outlook setup");
                log("Outlook window available; allowing two minutes for account configuration. This does not verify account readiness.");
            }
            double left=(outlookWarmUntil-DateTime.UtcNow).TotalSeconds;
            if(left>0) { progress.OutlookPreparation="Outlook setup grace: "+Math.Ceiling(left)+"s remaining; complete sign-in manually if prompted"; return true; }
            outlookReady=true; progress.OutlookPreparation="Setup grace ended; account readiness not automatically verified";
            log(progress.OutlookPreparation); return false;
        }
        bool Focus(IntPtr window,string name) {
            if(window==IntPtr.Zero || !Native.Exists(window) || !Native.DesktopAvailable() || Native.InputIdleSeconds()<15) return false;
            progress.ForegroundTarget=name; progress.ForegroundAttempts++;
            string detail; bool ok=Native.Foreground(window,out detail);
            if(ok) progress.ForegroundConfirmed++; else progress.ForegroundDenied++;
            log("FOREGROUND "+name+" / "+(ok ? "confirmed" : "not granted by Windows")+" / PID "+Native.WindowPid(window)+" / "+detail);
            return ok;
        }
        public void RotateForeground() {
            if(DateTime.UtcNow<nextFocus) return;
            var targets=new List<KeyValuePair<IntPtr,string>>();
            if(excel!=null) targets.Add(new KeyValuePair<IntPtr,string>(new IntPtr((int)excel.Hwnd),"Excel"));
            foreach(var h in presentationWindows.Where(Native.Exists)) targets.Add(new KeyValuePair<IntPtr,string>(h,"PowerPoint"));
            if(outlookReady && Native.Exists(outlookWindow)) targets.Add(new KeyValuePair<IntPtr,string>(outlookWindow,"Outlook"));
            foreach(var h in browserWindows.Where(h=>Native.Tagged(h,marker))) targets.Add(new KeyValuePair<IntPtr,string>(h,"Browser"));
            if(targets.Count==0) return;
            targets=FocusRotation.OnePerApplication(targets);
            var target=targets[focusIndex++ % targets.Count];
            if(Focus(target.Key,target.Value) && target.Value=="Browser") {
                if(Native.NextBrowserTab(target.Key)) { progress.BrowserTabSwitches++; log("BROWSER TAB NEXT / PID "+Native.WindowPid(target.Key)); }
                else log("BROWSER TAB NEXT skipped: browser window is no longer confirmed foreground or user input resumed.");
            }
            nextFocus=DateTime.UtcNow.AddSeconds(random.Next(30,61));
        }
        void Browse() {
            if(browsersOpened) {
                log("Live website tabs retained; page refresh behavior is controlled by each site and browser policy.");
                return;
            }
            foreach(var browser in browserPlan) {
                var pages=BrowserSites.Select(browserSites,browser.Tabs,random.Next());
                string executable=browser.Name=="Edge" ? edge : chrome;
                string processName=browser.Name=="Edge" ? "msedge" : "chrome";
                startupStage("Starting "+browser.Name+"; discovering the isolated browser window");
                BrowserDiscovery.CheckProfilePolicy(browser.Name,log);
                string profile=Path.Combine(local,browser.Name+"Profile");
                string args="--no-first-run --no-default-browser-check --new-window --user-data-dir=\""+Path.Combine(local,browser.Name+"Profile")+"\" "+String.Join(" ",pages.Select(url=>"\""+url+"\"").ToArray());
                log("BROWSER URLS "+browser.Name+": "+String.Join(" | ",pages));
                using(var launched=Process.Start(new ProcessStartInfo(executable,args) {UseShellExecute=false})) {
                    if(launched==null) throw new InvalidOperationException(browser.Name+" did not return a browser process.");
                    var windows=BrowserDiscovery.Wait(launched,processName,executable,profile,log);
                    if(windows.Count==0) throw new InvalidOperationException(browser.Name+" startup did not produce a verified test-profile window within 30 seconds. Export logs for launcher exit and profile-discovery details; check managed UserDataDir policy.");
                    foreach(var h in windows) { if(!Native.Tag(h,marker)) throw new IOException("Cannot mark ownership of the test browser window."); browserWindows.Add(h); }
                    progress.AppOpens++;
                    log("Opened isolated "+browser.Name+"; verified window PIDs="+String.Join(",",windows.Select(h=>Native.WindowPid(h).ToString()).Distinct().ToArray())+"; requested "+browser.Tabs+" website tabs. Page load success and tab count not independently verified.");
                }
            }
            browsersOpened=true;
        }
        void CloseBrowser() { foreach(var h in browserWindows) if(Native.Tagged(h,marker)) { Native.Close(h,marker); progress.AppCloses++; } browserWindows.Clear(); }
        void CloseOutlook() { if(outlookOwned && outlookWindow!=IntPtr.Zero && Native.Tagged(outlookWindow,marker)) { Native.Close(outlookWindow,marker); progress.AppCloses++; } outlookWindow=IntPtr.Zero; }
        void CloseOffice() {
            var errors=new List<Exception>(); bool excelClean=true, powerpointClean=true;
            foreach(var entry in books.ToArray()) {
                dynamic book=entry.Value;
                bool saved=CleanupSteps.Try("Save Excel document: "+entry.Key,delegate { book.Save(); Saved(entry.Key,false); },errors);
                bool closed=saved && CleanupSteps.Try("Close Excel document: "+entry.Key,delegate { book.Close(false); },errors);
                excelClean=excelClean && saved && closed;
                if(closed) { CleanupSteps.Try("Release Excel document: "+entry.Key,delegate { Release((object)book); },errors); books.Remove(entry.Key); }
            }
            foreach(var entry in decks.ToArray()) {
                dynamic deck=entry.Value;
                bool saved=CleanupSteps.Try("Save PowerPoint document: "+entry.Key,delegate { deck.Save(); Saved(entry.Key,false); },errors);
                bool closed=saved && CleanupSteps.Try("Close PowerPoint document: "+entry.Key,delegate { deck.Close(); },errors);
                powerpointClean=powerpointClean && saved && closed;
                if(closed) { CleanupSteps.Try("Release PowerPoint document: "+entry.Key,delegate { Release((object)deck); },errors); decks.Remove(entry.Key); }
            }
            if(excel!=null && excelClean) CleanupSteps.Try("Quit/release Excel",delegate {
                if(quitExcel) { excel.Quit(); progress.AppCloses++; } Release((object)excel); excel=null;
            },errors);
            if(powerpoint!=null && powerpointClean) CleanupSteps.Try("Quit/release PowerPoint",delegate {
                if(quitPowerpoint) { powerpoint.Quit(); progress.AppCloses++; } Release((object)powerpoint); powerpoint=null; quitPowerpoint=false;
            },errors);
            if(errors.Count>0) throw new AggregateException("Office cleanup incomplete; failed-save documents left open for recovery",errors);
            presentationWindows.Clear(); GC.Collect(); GC.WaitForPendingFinalizers();
            lock(processGate) ownedProcesses.Clear();
        }
        void TrackProcess(int pid) { using(var process=Process.GetProcessById(pid)) lock(processGate) ownedProcesses[pid]=process.StartTime.ToUniversalTime().Ticks; }
        public void EmergencyClose() {
            try { CloseBrowser(); } catch { } try { CloseOutlook(); } catch { }
            KeyValuePair<int,long>[] owned; lock(processGate) owned=ownedProcesses.ToArray();
            foreach(var entry in owned) try { using(var p=Process.GetProcessById(entry.Key)) {
                if(p.StartTime.ToUniversalTime().Ticks==entry.Value && (p.ProcessName=="EXCEL" || p.ProcessName=="POWERPNT")) p.Kill();
            } } catch { }
        }
        public void Dispose() {
            // Attempt every cleanup even when one application refuses to close.
            var errors=new List<Exception>();
            CleanupSteps.Try("Office documents and processes",CloseOffice,errors);
            CleanupSteps.Try("Browser windows",CloseBrowser,errors);
            CleanupSteps.Try("Outlook window",CloseOutlook,errors);
            if(errors.Count>0) throw new AggregateException("Application cleanup incomplete",errors);
        }
        static void Release(object value) { if(value!=null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    }
    internal static partial class Native {
        delegate bool EnumProc(IntPtr window,IntPtr parameter);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr parameter);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll",EntryPoint="IsWindow")] internal static extern bool Exists(IntPtr window);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int size);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from,uint to,bool attach);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window,int command);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [StructLayout(LayoutKind.Sequential)] struct LastInput { public uint Size,Tick; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput info);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern bool GetDiskFreeSpaceEx(string path,out ulong available,out ulong total,out ulong free);
        [DllImport("kernel32.dll")] internal static extern uint SetThreadExecutionState(uint flags);
        [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint pid);
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
        [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder value,int size,out int needed);
        internal static bool DesktopAvailable() {
            var h=OpenInputDesktop(0,false,0x0001); if(h==IntPtr.Zero) return false;
            try { int needed; var text=new StringBuilder(256); return GetUserObjectInformation(h,2,text,512,out needed) && text.ToString().Equals("Default",StringComparison.OrdinalIgnoreCase); }
            finally { CloseDesktop(h); }
        }
        [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="SetPropW")] static extern bool SetProp(IntPtr window,string name,IntPtr value);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="GetPropW")] static extern IntPtr GetProp(IntPtr window,string name);
        internal static bool Tag(IntPtr window,string marker) { return SetProp(window,"AfterSix."+marker,new IntPtr(1)); }
        internal static bool Tagged(IntPtr window,string marker) { return Exists(window) && GetProp(window,"AfterSix."+marker)==new IntPtr(1); }
        internal static void Close(IntPtr window,string marker) {
            if(!Tagged(window,marker)) return;
            PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero);
            for(int i=0;i<50;i++) { if(!Tagged(window,marker) || !IsWindowVisible(window)) return; Thread.Sleep(100); }
            throw new IOException("A test-owned application window did not close within five seconds.");
        }
        internal static int WindowPid(IntPtr window) { uint id; GetWindowThreadProcessId(window,out id); return (int)id; }
        internal static string Title(IntPtr window) { var s=new StringBuilder(512); GetWindowText(window,s,512); return s.ToString(); }
        internal static HashSet<int> ProcessIds(string name) { var result=new HashSet<int>(); foreach(var p in Process.GetProcessesByName(name)) using(p) result.Add(p.Id); return result; }
        internal static List<IntPtr> Windows(string name) {
            var result=new List<IntPtr>(); EnumWindows(delegate(IntPtr h,IntPtr unused) {
                if(IsWindowVisible(h)) try { using(var p=Process.GetProcessById(WindowPid(h))) if(p.ProcessName.Equals(name,StringComparison.OrdinalIgnoreCase) && p.SessionId==Process.GetCurrentProcess().SessionId) result.Add(h); } catch { }
                return true;
            },IntPtr.Zero); return result;
        }
    }
}
