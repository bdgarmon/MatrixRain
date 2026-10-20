using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
namespace MatrixRainLab {
    public static class Troubleshooting {
        const int FileLimit=2*1024*1024, TotalLimit=24*1024*1024;
        public static void Export(string destination,string data,string local,string application,bool? awakeAccepted=null,string powerReport=null,string powerNote=null) {
            string temporary=destination+".partial-"+Guid.NewGuid().ToString("N");
            var report=new StringBuilder(); int remaining=TotalLimit;
            report.AppendLine("Matrix Rain troubleshooting / UTC "+DateTime.UtcNow.ToString("o"));
            report.AppendLine("Hostname: "+Environment.MachineName+"\r\nOS: "+WindowsBuild()+"\r\n64-bit OS/process: "+Environment.Is64BitOperatingSystem+" / "+Environment.Is64BitProcess);
            report.AppendLine("Display awake request accepted at export start: "+(awakeAccepted.HasValue ? awakeAccepted.Value.ToString() : "not supplied")+". Acceptance is not proof of a lit screen or a policy override.");
            report.AppendLine("Power collection: "+(powerNote ?? "standard user; privileged queries may be unavailable"));
            report.AppendLine("Data: "+data+"\r\nDisplay logs: "+local+"\r\nApplication: "+application);
            report.AppendLine("Display identity: "+Environment.UserDomainName+"\\"+Environment.UserName);
            foreach(string path in new[]{data,Path.Combine(data,"config.json"),Path.Combine(data,"workloads"),Path.Combine(data,"state.json")}) {
                try {
#if !PORTABLE_TEST
                    if(Environment.OSVersion.Platform==PlatformID.Win32NT) {
                        var acl=Directory.Exists(path) ? (System.Security.AccessControl.FileSystemSecurity)Directory.GetAccessControl(path) : File.GetAccessControl(path);
                        report.AppendLine("ACL "+path+": "+acl.GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.Access));
                    }
#endif
                } catch(Exception e) { report.AppendLine("ACL unavailable: "+path+" / "+e.Message); }
            }
            report.AppendLine("Local export only. May contain hostnames, paths and configuration labels. Nothing is uploaded.");
            report.AppendLine("Files are live snapshots, not a transactional backup. CSV/log files may be truncated. Scratch files and credentials are not collected.");
            try {
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var zip=new ZipArchive(output,ZipArchiveMode.Create)) {
                    Action<string,string> collect=delegate(string source,string entry) {
                        try {
                            if(!File.Exists(source)) { report.AppendLine("Absent: "+source); return; }
                            if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0) { report.AppendLine("Skipped linked file: "+source); return; }
                            using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                                int size=(int)Math.Min(input.Length,Math.Min(FileLimit,remaining));
                                if(size<=0 && input.Length>0) { report.AppendLine("Size budget exhausted: "+source); return; }
                                var bytes=new byte[size]; int read=0,n;
                                while(read<size && (n=input.Read(bytes,read,size-read))>0) read+=n;
                                using(var stream=zip.CreateEntry(entry,CompressionLevel.Optimal).Open()) stream.Write(bytes,0,read);
                                remaining-=read; report.AppendLine("Included: "+entry+" / "+read+" bytes"+(input.Length>read ? " / TRUNCATED" : ""));
                            }
                        } catch(Exception e) { report.AppendLine("Unavailable: "+source+" / "+e.Message); }
                    };
                    Action<string,string,int,string> group=delegate(string folder,string pattern,int count,string prefix) {
                        try {
                            if(!Directory.Exists(folder)) return;
                            if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0) { report.AppendLine("Skipped linked directory: "+folder); return; }
                            foreach(var file in new DirectoryInfo(folder).GetFiles(pattern).OrderByDescending(f=>f.LastWriteTimeUtc).Take(count)) collect(file.FullName,prefix+file.Name);
                        } catch(Exception e) { report.AppendLine("Cannot list "+folder+": "+e.Message); }
                    };
                    foreach(string name in new[]{"config.json","state.json","browser-sites.json"}) collect(Path.Combine(data,name),"data/"+name);
                    foreach(string name in new[]{"run.json","last-result.json","last-phase.json"}) collect(Path.Combine(data,"workloads",name),"workloads/"+name);
                    collect(Path.Combine(local,"appearance.json"),"display/appearance.json");
                    group(Path.Combine(data,"workloads"),"result-*.json",20,"workloads/");
                    group(Path.Combine(data,"workloads"),"error-*.txt",20,"workloads/");
                    group(Path.Combine(data,"workloads"),"worker-*.txt",10,"workloads/");
                    group(Path.Combine(data,"deployment-logs"),"install-*.txt",5,"deployment/");
                    foreach(string name in new[]{"last-error.txt","diagnostics.json","elevation.log","elevation.log.1","office-launch-error.txt"}) collect(Path.Combine(local,name),"display/"+name);
                    string officeRoot=Path.Combine(local,"office");
                    if(Directory.Exists(officeRoot)) foreach(var folder in new DirectoryInfo(officeRoot).GetDirectories().OrderByDescending(d=>d.LastWriteTimeUtc).Take(5)) {
                        if((folder.Attributes&FileAttributes.ReparsePoint)!=0) continue;
                        foreach(string name in new[]{"actions.log","summary.json","schedule.json","studio-brief.json"}) collect(Path.Combine(folder.FullName,name),"office/"+folder.Name+"/"+name);
                    }
                    group(Path.Combine(local,"logs"),"samples-*.csv",1,"display/");
                    foreach(string name in new[]{"MatrixRainLab.exe","MatrixLabWorker.exe","MatrixOfficeActor.exe"}) {
                        try { report.AppendLine(name+" version: "+FileVersionInfo.GetVersionInfo(Path.Combine(application,name)).FileVersion); }
                        catch(Exception e) { report.AppendLine(name+": "+e.Message); }
                    }
                    Dictionary<string,string> elevated=null;
                    if(powerReport!=null) try {
                        elevated=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(powerReport));
                    } catch(Exception e) { report.AppendLine("Elevated power report unavailable: "+e.Message); }
                    foreach(string argument in PowerDiagnostics.Commands) {
                        string content;
                        if(elevated==null || !elevated.TryGetValue(argument,out content)) content=PowerDiagnostics.PowerCommand(argument);
                        else content="Elevated snapshot UTC: "+elevated["CapturedUtc"]+Environment.NewLine+content;
                        using(var writer=new StreamWriter(zip.CreateEntry("power/"+argument.Substring(1)+".txt").Open())) writer.Write(content);
                    }
                    using(var writer=new StreamWriter(zip.CreateEntry("REPORT.txt").Open())) writer.Write(report.ToString());
                }
                if(File.Exists(destination)) File.Replace(temporary,destination,null); else File.Move(temporary,destination);
            } finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
        public static string WindowsBuild() {
            if(Environment.OSVersion.Platform!=PlatformID.Win32NT) return Environment.OSVersion.ToString()+" (non-Windows test host)";
            try {
                using(var hklm=Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine,Microsoft.Win32.RegistryView.Registry64))
                using(var key=hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) {
                    if(key==null) return "Unavailable: Windows build registry key missing";
                    return "Windows NT "+key.GetValue("CurrentMajorVersionNumber","?")+"."+key.GetValue("CurrentMinorVersionNumber","?")+"."+
                        key.GetValue("CurrentBuildNumber","?")+"."+key.GetValue("UBR","?")+" / "+key.GetValue("DisplayVersion","unknown release")+" (64-bit registry)";
                }
            } catch(Exception e) { return "Unavailable: "+e.Message; }
        }
    }
}
