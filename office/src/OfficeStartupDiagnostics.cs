using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32;
namespace MatrixOfficeActor {
    internal static class OfficeStartupDiagnostics {
        internal static void Capture(string progId,string processName,Action<string> log) {
            // Read-only and best effort: diagnostics must not obscure the activation failure.
            try {
                using(var identity=WindowsIdentity.GetCurrent())
                using(var current=Process.GetCurrentProcess())
                    log("Office startup context: SID="+identity.User.Value+" session="+current.SessionId+" x64="+Environment.Is64BitProcess+" apartment="+Thread.CurrentThread.GetApartmentState()+" administrator="+new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator));
                foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}) {
                    using(var root=RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot,view))
                    using(var id=root.OpenSubKey(progId+@"\CLSID")) {
                        string clsid=id==null ? null : id.GetValue(null) as string;
                        using(var server=clsid==null ? null : root.OpenSubKey(@"CLSID\"+clsid+@"\LocalServer32"))
                            log("Office registration "+view+" "+progId+" CLSID="+(clsid??"missing")+" LocalServer32="+(server==null ? "missing" : Convert.ToString(server.GetValue(null))));
                    }
                }
                var processes=Process.GetProcessesByName(processName);
                log(processName+" process count="+processes.Length);
                foreach(var p in processes) using(p) {
                    try { log(processName+" PID="+p.Id+" session="+p.SessionId+" startedUtc="+p.StartTime.ToUniversalTime().ToString("o")+" responding="+p.Responding+" hasWindow="+(p.MainWindowHandle!=IntPtr.Zero)); }
                    catch(Exception e) { log("Office process snapshot unavailable: "+e.Message); }
                }
            } catch(Exception e) { log("Office startup diagnostics unavailable: "+e.Message); }
        }
    }
}
