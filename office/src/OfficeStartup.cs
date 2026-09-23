using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace MatrixOfficeActor {
    public static class OfficeStartup {
        // Retry activation only; replaying document operations could duplicate work.
        public static object Create(string name, Func<object> factory, Action<int> wait,
                                    Action<string> stage, Action<string> log, Action snapshot) {
            for(int attempt=1;attempt<=2;attempt++) {
                stage("Starting "+name+" automation (attempt "+attempt+"/2)");
                snapshot();
                var timer=Stopwatch.StartNew();
                try {
                    object result=factory();
                    log(name+" activation succeeded on attempt "+attempt+" after "+timer.Elapsed.TotalSeconds.ToString("F1")+" seconds.");
                    return result;
                } catch(COMException e) {
                    log(name+" activation attempt "+attempt+" failed after "+timer.Elapsed.TotalSeconds.ToString("F1")+" seconds: "+e.ToString());
                    snapshot();
                    if(attempt==1 && e.ErrorCode==unchecked((int)0x80080005)) {
                        log("Retrying server startup once after 2 seconds; existing Office processes are left intact.");
                        wait(2000);
                        continue;
                    }
                    throw new InvalidOperationException(name+" automation startup failed (0x"+e.ErrorCode.ToString("X8")+"). Open and close "+name+" as the signed-in user, complete any setup/activation prompts, then retry. If it persists, export logs; application startup diagnostics are in actions.log.",e);
                }
            }
            throw new InvalidOperationException("Office startup attempts exhausted.");
        }
    }
}
