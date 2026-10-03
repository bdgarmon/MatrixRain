using System;
using System.Collections.Generic;
namespace MatrixOffice {
    public sealed class OfficeProgress {
        public string State, Activity, Error, UserSid, Desktop, Documents, UpdatedUtc, Browsers;
        public int BrowserTabsRequested, BrowserTabSwitches, OutlookDrafts;
        public int ForegroundAttempts, ForegroundConfirmed, ForegroundDenied, InteractionPausedSeconds;
        public string ForegroundTarget, OutlookPreparation;
        public int Pid, Actions, Created, Saved, AppOpens, AppCloses, PausedSeconds, SkippedActions;
        public long BytesSaved;
    }
    public sealed class BrowserChoice {
        public string Name;
        public int Tabs;
    }
    public static class BrowserPlan {
        public static BrowserChoice[] Choose(bool edge,bool chrome,int seed) {
            if(!edge && !chrome) throw new InvalidOperationException("Install Microsoft Edge or Google Chrome for Office workday.");
            var random=new Random(seed); var choices=new List<BrowserChoice>();
            int combination=edge && chrome ? random.Next(3) : edge ? 0 : 1;
            if(combination==0 || combination==2) choices.Add(new BrowserChoice {Name="Edge",Tabs=random.Next(8,16)});
            if(combination==1 || combination==2) choices.Add(new BrowserChoice {Name="Chrome",Tabs=random.Next(8,16)});
            return choices.ToArray();
        }
    }
    public static class OfficeRequirements {
        public static string Missing(bool word,bool excel,bool powerpoint) {
            var missing=new List<string>();
            if(!word) missing.Add("Word");
            if(!excel) missing.Add("Excel");
            if(!powerpoint) missing.Add("PowerPoint");
            return String.Join(", ",missing.ToArray());
        }
        public static bool Met(bool word,bool excel,bool powerpoint) { return word && excel && powerpoint; }
    }
    public static class OutlookDraftPlan {
        static readonly string[] subjects={"Project status review","Customer follow-up notes","Weekly operations update","Quarterly planning draft","Team schedule review","Budget worksheet questions"};
        static readonly string[] paragraphs={"I reviewed the latest project materials and noted the items that need follow-up before the next check-in.","The working files have been updated with the current assumptions, dates, and open decisions for the team.","Please use this draft as a reference while we compare the schedule with the remaining work and available resources.","I will consolidate the requested changes after the next review and save the revised documents in the project folder."};
        public static string Subject(int seed) { var random=new Random(seed); return subjects[random.Next(subjects.Length)]+" "+random.Next(100,1000); }
        public static string Body(int seed) {
            var random=new Random(seed^0x51A7); int count=random.Next(2,5); var lines=new List<string>();
            lines.Add("WORKING DRAFT"); lines.Add("");
            for(int i=0;i<count;i++) { lines.Add(paragraphs[random.Next(paragraphs.Length)]); lines.Add(""); }
            lines.Add("Next step: review the attached project notes and update the action list.");
            return String.Join("\r\n",lines.ToArray());
        }
    }
    public sealed class OfficeStep {
        public int AtSeconds;
        public string Action;
    }
    public static class OfficePlan {
        public const int DefaultSeconds=21600, MaximumSeconds=28800;
        // Randomized timing is repeatable from the recorded seed. No wall-clock sleeps in the planner.
        public static List<OfficeStep> Create(int seconds,int seed) {
            if(seconds<60 || seconds>MaximumSeconds) throw new ArgumentException("Office duration must be 60..28800 seconds.");
            var random=new Random(seed); var steps=new List<OfficeStep>();
            steps.Add(new OfficeStep { AtSeconds=0,Action="open-apps" });
            string[] start={"create-text","create-csv","create-excel","create-powerpoint","browse","edit-excel","edit-powerpoint"};
            for(int i=0;i<start.Length;i++) if((i+1)*5<seconds) steps.Add(new OfficeStep { AtSeconds=(i+1)*5,Action=start[i] });
            int next=60, restart=5400, pause=4200;
            string[] actions={"edit-excel","edit-excel","edit-powerpoint","edit-text","edit-csv","create-text","create-excel","create-powerpoint","browse","edit-excel","edit-powerpoint","view-outlook","draft-outlook"};
            while(next<seconds) {
                if(next>=restart) { steps.Add(new OfficeStep {AtSeconds=next,Action="restart-apps"}); restart+=5400; next+=60; }
                else if(next>=pause) { steps.Add(new OfficeStep {AtSeconds=next,Action="break"}); next+=random.Next(600,1201); pause+=5400; }
                else { steps.Add(new OfficeStep {AtSeconds=next,Action=actions[random.Next(actions.Length)]}); next+=random.Next(30,121); }
            }
            return steps;
        }
    }
}

namespace MatrixOffice {
    // Writers publish complete snapshots with File.Replace. Readers must permit deletion
    // of the old snapshot and tolerate brief sharing conflicts during publication.
    public static class StatusFile {
        public static string Read(string path,bool retryMissing=false) {
            return ReadWithRetry(delegate {
                using(var stream=new System.IO.FileStream(path,System.IO.FileMode.Open,System.IO.FileAccess.Read,
                    System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete))
                using(var reader=new System.IO.StreamReader(stream)) return reader.ReadToEnd();
            }, delegate { System.Threading.Thread.Sleep(50); },retryMissing);
        }
        internal static string ReadWithRetry(System.Func<string> read,System.Action wait,bool retryMissing=false) {
            for(int attempt=0;;attempt++) {
                try { return read(); }
                catch(System.IO.FileNotFoundException) { if(!retryMissing || attempt==20) throw; wait(); }
                catch(System.IO.DirectoryNotFoundException) { throw; }
                catch(System.IO.IOException) { if(attempt==20) throw; wait(); }
            }
        }
    }
}

namespace MatrixOffice {
    public static class ForegroundSwitch {
        public static bool Run(System.Action restore,System.Action activate,System.Func<bool> confirmed,System.Action wait) {
            restore();
            for(int i=0;i<3;i++) { activate(); wait(); if(confirmed()) return true; }
            return false;
        }
    }
}

namespace MatrixOffice {
    public static class CleanupSteps {
        public static bool Try(string name,System.Action action,System.Collections.Generic.List<System.Exception> errors) {
            try { action(); return true; }
            catch(System.Exception e) { errors.Add(new System.InvalidOperationException(name,e)); return false; }
        }
        public static string Describe(System.Exception error) {
            var messages=new System.Collections.Generic.List<string>();
            Collect(error,messages);
            string text=System.String.Join(" > ",messages.ToArray());
            return text.Length>3000 ? text.Substring(0,3000)+" (full details in actions.log)" : text;
        }
        static void Collect(System.Exception error,System.Collections.Generic.List<string> messages) {
            messages.Add(error.GetType().Name+" [0x"+error.HResult.ToString("X8")+"]: "+error.Message);
            var aggregate=error as System.AggregateException;
            if(aggregate!=null) foreach(var child in aggregate.InnerExceptions) Collect(child,messages);
            else if(error.InnerException!=null) Collect(error.InnerException,messages);
        }
        public static string Preserve(string original,System.Exception cleanup) {
            return (System.String.IsNullOrEmpty(original) ? "" : original+" | ")+"Cleanup: "+Describe(cleanup);
        }
    }
}

namespace MatrixOffice {
    public static class BrowserSites {
        public static readonly string[] Defaults={"https://www.foxnews.com/","https://www.cnn.com/","https://gizmodo.com/","https://aftersixcomputers.com/","https://apnews.com/","https://www.theverge.com/","https://arstechnica.com/","https://www.bbc.com/news"};
        public static string[] Select(string[] sites,int tabs,int seed) {
            if(sites==null || sites.Length<1 || sites.Length>64) throw new ArgumentException("browser-sites.json must contain 1..64 HTTPS URLs.");
            if(tabs<8 || tabs>15) throw new ArgumentException("Browser tab count must be 8..15.");
            var valid=new System.Collections.Generic.List<string>();
            foreach(string site in sites) {
                System.Uri uri;
                if(System.String.IsNullOrWhiteSpace(site) || site.IndexOfAny(new[]{'\r','\n','\t','"',' '})>=0 ||
                    !System.Uri.TryCreate(site,System.UriKind.Absolute,out uri) || uri.Scheme!="https" ||
                    System.String.IsNullOrEmpty(uri.Host) || !System.String.IsNullOrEmpty(uri.UserInfo) || uri.AbsoluteUri.Length>2048)
                    throw new ArgumentException("browser-sites.json requires absolute HTTPS URLs without credentials or whitespace.");
                if(!valid.Contains(uri.AbsoluteUri)) valid.Add(uri.AbsoluteUri);
            }
            var random=new System.Random(seed); var result=new System.Collections.Generic.List<string>();
            while(result.Count<tabs) {
                var cycle=new System.Collections.Generic.List<string>(valid);
                while(cycle.Count>0 && result.Count<tabs) {int index=random.Next(cycle.Count);result.Add(cycle[index]);cycle.RemoveAt(index);}
            }
            return result.ToArray();
        }
    }
}

namespace MatrixOffice {
    public static class FocusRotation {
        public static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.IntPtr,string>> OnePerApplication(
            System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<System.IntPtr,string>> targets) {
            var result=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.IntPtr,string>>();
            var seen=new System.Collections.Generic.HashSet<string>();
            foreach(var target in targets) if(target.Value=="Browser" || seen.Add(target.Value)) result.Add(target);
            return result;
        }
    }
    public sealed class SyntheticInputClock {
        uint synthetic,previous; bool recorded;
        public void Record(uint tick,uint previousInput) { previous=UserTick(previousInput); synthetic=tick; recorded=true; }
        public uint UserTick(uint input) { return recorded && input==synthetic ? previous : input; }
    }
    public static class ForegroundFallback {
        public static bool Run(System.Func<bool> allowed,System.Func<bool> activate,System.Func<bool> unlock) {
            if(!allowed()) return false;
            if(activate()) return true;
            if(!allowed() || !unlock()) return false;
            return allowed() && activate();
        }
    }
}

namespace MatrixOffice {
    public static class BrowserProfile {
        public static bool Matches(string[] args,string profile) {
            string value=null; int matches=0;
            for(int i=1;i<args.Length;i++) {
                if(args[i].StartsWith("--user-data-dir=",System.StringComparison.OrdinalIgnoreCase)) { value=args[i].Substring(16); matches++; }
                else if(args[i].Equals("--user-data-dir",System.StringComparison.OrdinalIgnoreCase)) { value=++i<args.Length ? args[i] : null; matches++; }
            }
            return matches==1 && !System.String.IsNullOrEmpty(value) && System.String.Equals(value.TrimEnd('\\','/'),profile.TrimEnd('\\','/'),System.StringComparison.OrdinalIgnoreCase);
        }
    }
    public static class BrowserWindowWait {
        public static System.Collections.Generic.List<System.IntPtr> Run(System.Func<System.Collections.Generic.List<System.IntPtr>> discover,System.Func<double> seconds,System.Action wait) {
            while(seconds()<30) { var found=discover(); if(found.Count>0) return found; wait(); }
            return new System.Collections.Generic.List<System.IntPtr>();
        }
    }
}
