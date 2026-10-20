using System;
using System.Collections.Generic;
namespace MatrixOffice {
    public sealed class OfficeProgress {
        public string State, Activity, Error, UserSid, Desktop, Documents, UpdatedUtc, Browsers;
        public string StudioCollection,StudioTheme,StudioStage;
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
            if(!edge && !chrome) throw new InvalidOperationException("Install Microsoft Edge or Google Chrome for Microsoft 365 Testing.");
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
            string[] start={"create-text","create-csv","create-excel","create-powerpoint","create-word","edit-excel","edit-powerpoint","edit-word"};
            for(int i=0;i<start.Length;i++) if((i+1)*5<seconds) steps.Add(new OfficeStep { AtSeconds=(i+1)*5,Action=start[i] });
            int next=60, restart=5400, pause=4200;
            string[] actions={"edit-excel","edit-excel","edit-powerpoint","edit-word","edit-text","edit-csv","create-text","browse","edit-excel","edit-powerpoint","view-outlook","draft-outlook","create-excel","create-powerpoint","create-word"};
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
    public sealed class StudioPart {
        public string Name;
        public double Cost;
        public int Quantity;
    }
    public sealed class StudioProduct {
        public string Name,Category,Tagline,Specification,Material;
        public int Price,Units;
        public StudioPart[] Parts;
        public double Cost(int stage) {
            double total=0; foreach(var part in Parts) total+=UnitCost(part,stage)*part.Quantity;
            return Math.Round(total,2);
        }
        public static double UnitCost(StudioPart part,int stage) {
            return Math.Round(part.Cost*(part.Name=="Unified memory" ? stage==2 ? 1.18 : stage>=3 ? 1.06 : 1 : 1),2);
        }
        public double Margin(int stage) { return (Price-Cost(stage))/Price; }
    }
    public sealed class StudioBrief {
        public const string Brand="NORTH / CONCEPT STUDIO";
        public const string Disclaimer="FICTIONAL CONCEPT / SYNTHETIC DATA / NOT A REAL PRODUCT OFFER";
        public string Theme,Finish,Accent,Background,Ink,Muted,Collection;
        public StudioProduct[] Products;
        public static readonly string[] Stages={"01 / DISCOVER","02 / ENGINEER","03 / ADAPT","04 / REVEAL"};
        public static int Stage(double elapsed,int seconds) {
            if(seconds<1 || Double.IsNaN(elapsed) || Double.IsInfinity(elapsed)) throw new ArgumentException("A positive duration and finite elapsed time are required.");
            return Math.Min(3,(int)(Math.Max(0,Math.Min(1,elapsed/seconds))*4));
        }
        public static StudioBrief Create(int seed) {
            var random=new Random(seed); int theme=random.Next(3);
            var brief=new StudioBrief { Theme=new[]{"Silver Tide","Midnight Graphite","Soft Mineral"}[theme],
                Collection="FORM / "+new[]{"01","02","03"}[theme],
                Finish=new[]{"Bead-blasted silver / ocean glass","Graphite ceramic / warm copper","Chalk aluminum / sage glass"}[theme],
                Accent=new[]{"#247B91","#CE9E6A","#658B74"}[theme],
                Background=theme==1 ? "#14191F" : "#F3F1EC",Ink=theme==1 ? "#F2F0EB" : "#19252B",Muted=theme==1 ? "#A7B0BB" : "#596A71" };
            string[] names={"Arc 14","Core Studio","Canvas 32"};
            string[] categories={"ULTRAPORTABLE","CREATIVE WORKSTATION","STUDIO DISPLAY"};
            string[] taglines={"Less weight. More possibility.","Quiet power. Clear thinking.","Room for your best work."};
            string[] specs={"14-inch concept display / 32 GB memory / 1 TB storage / 1.25 kg target","16-core concept processor / 64 GB memory / 2 TB storage / modular access","32-inch 6K concept panel / USB-C hub / adjustable recycled-aluminum stand"};
            string[] parts={"Compute module","Unified memory","Storage module","Display assembly","Enclosure","Power system","Connectivity","Thermal system","Assembly and validation"};
            double[][] costs={new double[]{220,96,64,160,78,52,28,24,44},new double[]{480,192,128,0,140,88,44,96,72},new double[]{35,12,0,360,86,44,38,18,42}};
            brief.Products=new StudioProduct[3];
            for(int p=0;p<3;p++) {
                var list=new List<StudioPart>();
                for(int i=0;i<parts.Length;i++) if(costs[p][i]>0) list.Add(new StudioPart {Name=parts[i],Quantity=1,Cost=Math.Round(costs[p][i]*(0.97+random.NextDouble()*0.06),2)});
                brief.Products[p]=new StudioProduct {Name=names[p],Category=categories[p],Tagline=taglines[p],Specification=specs[p],Material=brief.Finish,Price=new[]{1799,2999,1299}[p],Units=new[]{24000,12000,18000}[p],Parts=list.ToArray()};
            }
            return brief;
        }
        public string Decision(int stage) {
            return stage<2 ? "Protect the silhouette, simplify the service path, and validate cost before locking the design." : stage==2 ?
                "SIMULATED SUPPLIER CHANGE: memory cost rises 18%. Preserve launch prices while evaluating a second source." :
                "REVISED LAUNCH PLAN: qualify a second memory source at 6% above the original assumption. Retain launch prices; fund the validation gate.";
        }
        public string EmailSubject(int stage) { return Collection+" / "+Stages[stage]+" / design review"; }
        public string EmailBody(int stage) {
            var text=new System.Text.StringBuilder("HARDWARE LAUNCH STUDIO\r\n\r\n");
            text.AppendLine(Theme+" / "+Finish); text.AppendLine();
            foreach(var p in Products) text.AppendLine(p.Name+" — "+p.Tagline+" Concept price $"+p.Price+"; modeled unit cost $"+p.Cost(stage).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".");
            text.AppendLine(); text.AppendLine(Decision(stage)); text.AppendLine("\r\nNext: reconcile the workbook, design brief and launch deck at the same revision.\r\n\r\n"+Disclaimer);
            return text.ToString();
        }
    }
    public sealed class StudioShape {
        public string Kind="rect",Fill;
        public float X,Y,W,H,Radius;
        public int Alpha=255;
        public float[] Points;
    }
    public static class StudioScene {
        static string Number(float value) { return value.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        public static List<StudioShape> Create(StudioBrief brief,int product) {
            if(product<0 || product>2) throw new ArgumentOutOfRangeException("product");
            var shapes=new List<StudioShape>();
            Action<float,float,float,float,string,float> box=(x,y,w,h,color,r)=>shapes.Add(new StudioShape {X=x,Y=y,W=w,H=h,Fill=color,Radius=r});
            Action<float,float,float,float,string,int> ellipse=(x,y,w,h,color,a)=>shapes.Add(new StudioShape {Kind="ellipse",X=x,Y=y,W=w,H=h,Fill=color,Alpha=a});
            string metal=brief.Theme=="Midnight Graphite" ? "#555D67" : "#C7CCCD",edge=brief.Theme=="Midnight Graphite" ? "#353D46" : "#8C979D";
            for(int i=0;i<12;i++) ellipse(170-i*7,787-i*2,1230+i*14,43+i*4,"#000000",2);
            if(product==0) {
                box(235,192,1080,600,metal,35); box(247,204,1056,570,"#151C24",26);
                box(267,224,1016,530,brief.Accent,12);
                ellipse(410,260,750,420,"#FFFFFF",18); ellipse(600,325,370,280,"#FFFFFF",25);
                box(325,285,320,370,"#1C2935",15); box(352,320,160,10,"#C5D6DD",4);
                for(int i=0;i<6;i++) box(352,370+i*32,240-i*19,6,"#788B99",3);
                box(692,290,530,360,"#E7E6E0",12); box(725,323,230,12,brief.Accent,5);
                for(int i=0;i<5;i++) box(725+i*86,560-i*31,55,50+i*31,brief.Accent,5);
                ellipse(768,216,8,8,"#85949A",255);
                shapes.Add(new StudioShape {Kind="polygon",Fill=metal,Points=new float[]{235,792,1315,792,1460,858,90,858}});
                shapes.Add(new StudioShape {Kind="polygon",Fill=edge,Points=new float[]{90,858,1460,858,1410,883,145,883}});
                shapes.Add(new StudioShape {Kind="polygon",Fill="#AAB2B5",Points=new float[]{555,813,1000,813,1040,845,515,845}});
            } else if(product==1) {
                shapes.Add(new StudioShape {Kind="polygon",Fill=edge,Points=new float[]{480,218,750,135,1110,285,840,368}});
                shapes.Add(new StudioShape {Kind="polygon",Fill=metal,Points=new float[]{480,218,840,368,840,844,480,694}});
                shapes.Add(new StudioShape {Kind="polygon",Fill="#8B969D",Points=new float[]{840,368,1110,285,1110,762,840,844}});
                for(int i=0;i<30;i++) shapes.Add(new StudioShape {Kind="polygon",Fill=edge,Alpha=140,Points=new float[]{870,416+i*10,1075,352+i*10,1075,355+i*10,870,419+i*10}});
                ellipse(631,296,35,14,brief.Accent,255); box(541,597,90,10,"#323B44",4); box(541,626,90,10,"#323B44",4);
                ellipse(677,660,13,13,"#323B44",255);
                shapes.Add(new StudioShape {Kind="polygon",Fill="#E1E3E0",Alpha=55,Points=new float[]{480,218,485,221,845,371,840,368}});
            } else {
                shapes.Add(new StudioShape {Kind="polygon",Fill=edge,Points=new float[]{630,705,900,705,1030,867,495,867}});
                box(720,625,105,194,metal,15); box(185,165,1215,590,metal,24); box(198,178,1189,548,"#101922",14);
                box(217,197,1150,510,brief.Accent,7);
                ellipse(480,228,700,420,"#FFFFFF",22);
                box(265,240,1040,390,"#EAE8E2",11); box(296,270,130,15,brief.Accent,4);
                box(296,320,240,267,"#D0D8D7",8);
                for(int i=0;i<3;i++) { box(575+i*224,320,195,175,i==0 ? "#657F83" : i==1 ? "#A1B2A4" : "#C5B69F",7); box(575+i*224,521,164,9,"#8E9C9F",4); box(575+i*224,547,108,7,"#B5BFBE",3); }
                ellipse(780,737,13,5,brief.Accent,255);
            }
            return shapes;
        }
        public static string Svg(StudioBrief brief,int product) {
            var b=new System.Text.StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1600\" height=\"1000\" viewBox=\"0 0 1600 1000\"><rect width=\"1600\" height=\"1000\" fill=\""+brief.Background+"\"/>");
            foreach(var s in Create(brief,product)) {
                string style=" fill=\""+s.Fill+"\" fill-opacity=\""+(s.Alpha/255.0).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"\"";
                if(s.Kind=="polygon") {
                    b.Append("<polygon"+style+" points=\""); for(int i=0;i<s.Points.Length;i++) b.Append(s.Points[i].ToString(System.Globalization.CultureInfo.InvariantCulture)+(i%2==0 ? "," : " ")); b.Append("\"/>");
                } else if(s.Kind=="ellipse") b.Append("<ellipse"+style+" cx=\""+Number(s.X+s.W/2)+"\" cy=\""+Number(s.Y+s.H/2)+"\" rx=\""+Number(s.W/2)+"\" ry=\""+Number(s.H/2)+"\"/>");
                else b.Append("<rect"+style+" x=\""+Number(s.X)+"\" y=\""+Number(s.Y)+"\" width=\""+Number(s.W)+"\" height=\""+Number(s.H)+"\" rx=\""+Number(s.Radius)+"\"/>");
            }
            return b.Append("</svg>").ToString();
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
