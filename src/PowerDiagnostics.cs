using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace MatrixRainLab {
    public static class PowerDiagnostics {
        public static readonly string[] Commands={"/requests","/getactivescheme","/a"};
        public static void Save(string runtime,string id) {
            Directory.CreateDirectory(runtime);
            foreach(var file in new DirectoryInfo(runtime).GetFiles("power-*.json").OrderByDescending(f=>f.LastWriteTimeUtc).Skip(9)) file.Delete();
            var result=new Dictionary<string,string>();
            result["CapturedUtc"]=DateTime.UtcNow.ToString("o");
            foreach(string command in Commands) result[command]=PowerCommand(command);
            string path=Path.Combine(runtime,"power-"+Guid.Parse(id).ToString("N")+".json");
            string temp=path+".tmp";
            try { File.WriteAllText(temp,new JavaScriptSerializer().Serialize(result)); File.Move(temp,path); }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        public static string PowerCommand(string argument) {
            if(Array.IndexOf(Commands,argument)<0) throw new ArgumentException("Only read-only power diagnostic commands are allowed.");
            if(Environment.OSVersion.Platform!=PlatformID.Win32NT) return "Unavailable: powercfg requires Windows.";
            var text=new StringBuilder(); var gate=new object();
            try {
                string executable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"powercfg.exe");
                using(var process=new Process { StartInfo=new ProcessStartInfo(executable,argument) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true } }) {
                    DataReceivedEventHandler receive=delegate(object sender,DataReceivedEventArgs e) { if(e.Data!=null) lock(gate) { if(text.Length<65536) text.AppendLine(e.Data.Substring(0,Math.Min(e.Data.Length,65536-text.Length))); } };
                    process.OutputDataReceived+=receive; process.ErrorDataReceived+=receive;
                    process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                    if(!process.WaitForExit(5000)) { try { process.Kill(); } catch { } lock(gate) text.AppendLine("Timed out after five seconds."); }
                    else { process.WaitForExit(); lock(gate) text.AppendLine("Exit code: "+process.ExitCode+" (nonzero may mean administrator rights are required)."); }
                }
            } catch(Exception e) { lock(gate) text.AppendLine("Unavailable: "+e.Message); }
            lock(gate) return text.ToString();
        }
    }
}
