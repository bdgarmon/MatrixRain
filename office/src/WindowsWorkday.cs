using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace MatrixOfficeActor {
    // Runs only in the authorized, unelevated desktop session. Never launches Office.
    internal static class WindowsWorkday {
        internal static int Run(string id,string local,MatrixOffice.OfficeProgress status,StreamWriter writer,Func<Authorization> read,Action<string> log) {
            var owned=new List<IntPtr>(); var gate=new object();
            bool done=false; int exit=0; Exception error=null;
            string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Matrix Rain Windows Workday",id);
            string desktop=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Matrix Rain Windows Workday",id);
            string marker="WindowsWorkday."+id;
            var clock=Stopwatch.StartNew();
            Authorization initial=read();
            var heartbeat=new Thread(delegate() {
                while(true) {
                    lock(gate) {
                        if(done) return;
                        try { status.UpdatedUtc=DateTime.UtcNow.ToString("o"); writer.WriteLine(new JavaScriptSerializer().Serialize(status)); }
                        catch { return; }
                    }
                    Thread.Sleep(1000);
                }
            }); heartbeat.IsBackground=true; heartbeat.Start();
            Action<string> activity=delegate(string text) { lock(gate) status.Activity=text; log(text); };
            Func<bool> active=delegate {
                var permit=read();
                if((DateTime.UtcNow-DateTime.Parse(permit.UpdatedUtc).ToUniversalTime()).TotalSeconds>20) throw new IOException("Administrator heartbeat stale.");
                return permit.HelperId==id && permit.State=="RUNNING" && clock.Elapsed.TotalSeconds<initial.PhaseSeconds &&
                    !File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"AfterSix","MatrixRainLab","workloads","stop-"+permit.RunId));
            };
            try {
                Directory.CreateDirectory(root); Directory.CreateDirectory(desktop);
                status.Desktop=desktop; status.Documents=root; status.State="ACTIVE";
                string file=Path.Combine(root,"Workday-"+id.Substring(0,8)+".txt");
                File.WriteAllText(file,"Matrix Rain Windows Workday\r\nSynthetic work journal.\r\n",Encoding.UTF8); status.Created++;
                activity("Opening synthetic journal in Notepad");
                var before=new HashSet<IntPtr>(Native.Windows("notepad"));
                using(var p=Process.Start(new ProcessStartInfo("notepad.exe",Quote(file)) { UseShellExecute=true })) { }
                IntPtr note=IntPtr.Zero;
                for(int i=0;i<60 && active();i++) {
                    note=Native.Windows("notepad").FirstOrDefault(h=>!before.Contains(h) && Native.Title(h).IndexOf(Path.GetFileNameWithoutExtension(file),StringComparison.OrdinalIgnoreCase)>=0);
                    if(note!=IntPtr.Zero) break; Thread.Sleep(500);
                }
                if(note==IntPtr.Zero) throw new IOException("No separate Notepad journal window appeared. Set Notepad to open files in new windows and retry; no personal window was claimed.");
                if(!Native.Tag(note,marker)) throw new IOException("Cannot mark test-owned Notepad window.");
                owned.Add(note); status.AppOpens++;
                activity("Opening File Explorer in the test Documents folder");
                var explorers=new HashSet<IntPtr>(Native.Windows("explorer"));
                using(var p=Process.Start(new ProcessStartInfo("explorer.exe","/n,"+Quote(root)) {UseShellExecute=true})) { }
                for(int i=0;i<30 && active();i++) {
                    var found=Native.Windows("explorer").Where(h=>!explorers.Contains(h) && Native.Title(h).IndexOf(id,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
                    if(found.Length>0) { foreach(var h in found) if(Native.Tag(h,marker)) { owned.Add(h); status.AppOpens++; } break; }
                    Thread.Sleep(250);
                }
                string edge=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}
                    .Select(p=>Path.Combine(p,"Microsoft","Edge","Application","msedge.exe")).FirstOrDefault(File.Exists);
                if(edge==null) throw new FileNotFoundException("Windows Workday requires the Windows-provided Microsoft Edge browser.");
                // Reuse this user's test-only profile across phases to avoid accumulating browser caches.
                string profile=Path.Combine(Path.GetDirectoryName(local),"WindowsWorkdayEdge");
                activity("Opening Edge with an isolated test profile and eight tabs");
                BrowserDiscovery.CheckProfilePolicy("Edge",log);
                string siteConfig=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"AfterSix","MatrixRainLab","browser-sites.json");
                string[] sites=MatrixOffice.BrowserSites.Select(File.Exists(siteConfig) ? new JavaScriptSerializer().Deserialize<string[]>(File.ReadAllText(siteConfig)) : MatrixOffice.BrowserSites.Defaults,8,Guid.Parse(id).GetHashCode());
                using(var p=Process.Start(new ProcessStartInfo(edge,"--user-data-dir="+Quote(profile)+" --no-first-run --no-default-browser-check --new-window "+String.Join(" ",sites.Select(Quote).ToArray())) {UseShellExecute=false})) {
                    foreach(var h in BrowserDiscovery.Wait(p,"msedge",edge,profile,log)) if(Native.Tag(h,marker)) { owned.Add(h); status.AppOpens++; }
                }
                if(!owned.Any(h=>Native.Exists(h) && Native.Windows("msedge").Contains(h))) throw new IOException("No verified isolated Edge window appeared.");
                status.Browsers="Edge"; status.BrowserTabsRequested=8;
                var random=new Random(Guid.Parse(id).GetHashCode()); double nextEdit=0,nextFocus=0; int edit=0,turn=0;
                while(active()) {
                    if(!Native.DesktopAvailable() || Native.InputIdleSeconds()<15) {
                        lock(gate) { status.State="PAUSED"; status.Activity="User input or locked desktop; waiting for 15 seconds idle"; status.InteractionPausedSeconds++; }
                        Thread.Sleep(1000); continue;
                    }
                    status.State="ACTIVE";
                    if(clock.Elapsed.TotalSeconds>=nextEdit) {
                        activity("Editing and saving the synthetic Notepad journal");
                        var prose=new StringBuilder(); string[] sentences={"Review the project milestones and record the next action. ","Compare today's figures with the previous planning cycle. ","Draft the meeting agenda and summarize outstanding questions. ","Prepare a short status update for tomorrow's review. ","Check the task list and note any changes to the schedule. "};
                        for(int n=random.Next(2,18);n>0;n--) prose.Append(sentences[random.Next(sentences.Length)]);
                        string text="\r\n"+DateTime.Now.ToString("s")+" — Planning update "+(++edit)+"\r\n"+prose+"\r\n";
                        if(Native.AppendAndSave(note,marker,Path.GetFileNameWithoutExtension(file),text)) {
                            bool saved=false;
                            for(int attempt=0;attempt<40;attempt++) {
                                try { if(File.ReadAllText(file).Contains("Planning update "+edit)) { saved=true; break; } }
                                catch(IOException) { }
                                Thread.Sleep(100);
                            }
                            if(!saved) throw new IOException("Notepad save could not be verified within four seconds; journal edits may be pending.");
                            status.Saved++; status.Actions++;
                            string folder=edit%2==0 ? root : desktop;
                            ulong available,total,free;
                            if(!Native.GetDiskFreeSpaceEx(folder,out available,out total,out free) || available<2UL*1024*1024*1024) throw new IOException("Disk reserve reached; leaving 2 GiB free.");
                            string generated=Path.Combine(folder,"Task-"+edit+(edit%3==0 ? ".csv" : edit%3==1 ? ".txt" : ".html"));
                            string content=edit%3==0 ? "Task,Value\r\nPlanning,"+edit : edit%3==1 ? text : "<!doctype html><meta charset=utf-8><title>Workday</title><h1>Synthetic work</h1><p>Task "+edit+"</p>";
                            content+=new string(' ',random.Next(1024,131073));
                            File.WriteAllText(generated,content,Encoding.UTF8); status.Created++; status.Saved++; status.BytesSaved+=Encoding.UTF8.GetByteCount(content)+Encoding.UTF8.GetByteCount(text);
                            log("SAVE "+generated+" / journal edit "+edit);
                        } else { status.SkippedActions++; log("Journal edit skipped: window focus or document ownership could not be confirmed."); }
                        nextEdit=clock.Elapsed.TotalSeconds+random.Next(45,121);
                    }
                    if(clock.Elapsed.TotalSeconds>=nextFocus) {
                        IntPtr target=owned[turn++%owned.Count]; string detail;
                        status.ForegroundAttempts++;
                        if(Native.Tagged(target,marker) && Native.Foreground(target,out detail)) {
                            status.ForegroundConfirmed++; activity("Reading / working in "+Native.Title(target));
                            if(Native.Windows("msedge").Contains(target) && Native.NextBrowserTab(target)) status.BrowserTabSwitches++;
                        } else status.ForegroundDenied++;
                        nextFocus=clock.Elapsed.TotalSeconds+random.Next(20,61);
                    }
                    Thread.Sleep(1000);
                }
            } catch(Exception e) { error=e; exit=1; log("WINDOWS WORKDAY FAILED: "+e); }
            finally {
                activity("Saving and closing test-owned windows; generated files remain");
                foreach(var h in owned) {
                    try { if(Native.Tagged(h,marker)) { Native.Close(h,marker); status.AppCloses++; } }
                    catch(Exception e) { error=error==null ? e : new AggregateException(error,e); exit=1; }
                }
                lock(gate) {
                    status.State=exit==0 ? "COMPLETED" : "FAILED"; status.Error=error==null ? null : error.ToString();
                    status.Activity=exit==0 ? "Windows Workday finished; generated files retained" : "Windows Workday failed; export logs";
                    status.UpdatedUtc=DateTime.UtcNow.ToString("o");
                    File.WriteAllText(Path.Combine(local,"summary.json"),new JavaScriptSerializer().Serialize(status));
                    try { writer.WriteLine(new JavaScriptSerializer().Serialize(status)); } catch { }
                    done=true;
                }
                heartbeat.Join(2000);
            }
            return exit;
        }
        static string Quote(string value) { return "\""+value.Replace("\"","")+"\""; }
    }
    internal static partial class Native {
        internal static bool AppendAndSave(IntPtr window,string marker,string document,string text) {
            string detail;
            if(!Tagged(window,marker) || Title(window).IndexOf(document,StringComparison.OrdinalIgnoreCase)<0 || !Foreground(window,out detail) || !SafeToFocus(window) || !ConfirmFocus(window)) return false;
            if(!SendKeys(new[]{Key(0x11),Key(0x23),Key(0x23,true),Key(0x11,true)})) return false;
            foreach(char c in text) {
                if(!ConfirmFocus(window) || !SafeToFocus(window) || Title(window).IndexOf(document,StringComparison.OrdinalIgnoreCase)<0) return false;
                var down=new Input {Type=1,Keyboard=new KeyboardInput {Scan=c,Flags=4,Time=unchecked((uint)Environment.TickCount)}};
                var up=new Input {Type=1,Keyboard=new KeyboardInput {Scan=c,Flags=6,Time=unchecked((uint)Environment.TickCount)}};
                if(!SendKeys(new[]{down,up})) return false;
            }
            return ConfirmFocus(window) && SendKeys(new[]{Key(0x11),Key(0x53),Key(0x53,true),Key(0x11,true)});
        }
    }
}
