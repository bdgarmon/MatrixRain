using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using MatrixOffice;
using Microsoft.Win32;
namespace MatrixOfficeActor {
    internal static class BrowserDiscovery {
        [DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CommandLineToArgvW(string command,out int count);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
        static string[] Arguments(string command) {
            int count; var memory=CommandLineToArgvW(command,out count);
            if(memory==IntPtr.Zero) return new string[0];
            try { var args=new string[count]; for(int i=0;i<count;i++) args[i]=Marshal.PtrToStringUni(Marshal.ReadIntPtr(memory,i*IntPtr.Size)); return args; }
            finally { LocalFree(memory); }
        }
        static ManagementObjectSearcher Query(int pid) {
            var query=new ManagementObjectSearcher("SELECT CommandLine, ExecutablePath FROM Win32_Process WHERE ProcessId="+pid);
            query.Options.Timeout=TimeSpan.FromSeconds(2);
            return query;
        }
        internal static void CheckProfilePolicy(string browser,Action<string> log) {
            string path=browser=="Edge" ? @"SOFTWARE\Policies\Microsoft\Edge" : @"SOFTWARE\Policies\Google\Chrome";
            foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})
            foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}) {
                using(var root=RegistryKey.OpenBaseKey(hive,view)) using(var key=root.OpenSubKey(path)) {
                    string value=key==null ? null : key.GetValue("UserDataDir") as string;
                    if(!String.IsNullOrEmpty(value)) {
                        log("BROWSER managed UserDataDir detected: "+hive+" "+view+" "+path);
                        throw new InvalidOperationException(browser+" has a managed UserDataDir policy. Matrix Rain cannot guarantee isolation of its browser profile; use a pilot device policy that permits an isolated profile. No policy was changed.");
                    }
                }
            }
        }
        internal static List<IntPtr> Wait(Process launcher,string processName,string executable,string profile,Action<string> log) {
            var timer=Stopwatch.StartNew(); bool exitLogged=false;
            var messages=new HashSet<string>(); int session;
            using(var current=Process.GetCurrentProcess()) session=current.SessionId;
            Action<string> once=message=>{if(messages.Add(message)) log(message);};
            log("BROWSER DISCOVERY launcher PID="+launcher.Id+" profile="+profile+" timeout=30s; launcher exit does not end discovery.");
            var result=BrowserWindowWait.Run(delegate {
                if(!exitLogged && launcher.HasExited) { exitLogged=true; log("BROWSER launcher exited: PID="+launcher.Id+" code="+launcher.ExitCode+"; continuing profile discovery."); }
                var found=new List<IntPtr>();
                foreach(var group in Native.Windows(processName).GroupBy(Native.WindowPid)) {
                    int pid=group.Key;
                    try {
                        using(var process=Process.GetProcessById(pid)) {
                            if(process.SessionId!=session) continue;
                            using(var query=Query(pid))
                            using(var rows=query.Get()) foreach(ManagementObject row in rows) using(row) {
                                string command=row["CommandLine"] as string,path=row["ExecutablePath"] as string;
                                bool owned=String.Equals(path,executable,StringComparison.OrdinalIgnoreCase) &&
                                    !String.IsNullOrEmpty(command) && BrowserProfile.Matches(Arguments(command),profile);
                                if(!owned) { once("BROWSER rejected PID="+pid+": executable/profile could not be verified (unrelated or unreadable process)."); continue; }
                                if(process.HasExited) continue;
                                foreach(var window in group) if(Native.Exists(window) && Native.WindowPid(window)==pid) found.Add(window);
                                once("BROWSER verified profile owner PID="+pid+" session="+session+" (launcher PID="+launcher.Id+").");
                            }
                        }
                    } catch(Exception e) { once("BROWSER discovery PID="+pid+" error="+e.GetType().Name+": "+e.Message); }
                }
                return found;
            },()=>timer.Elapsed.TotalSeconds,()=>Thread.Sleep(500));
            if(result.Count==0) log("BROWSER discovery timed out without a verified window. Check browser startup and managed UserDataDir policy; no personal browser window was claimed.");
            return result;
        }
    }
}
