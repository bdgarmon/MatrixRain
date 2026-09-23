using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using MatrixOffice;
namespace MatrixLabWorkloads {
    internal sealed class OfficeBridge : IDisposable {
        readonly NamedPipeServerStream pipe;
        readonly Thread reader;
        readonly object gate=new object();
        readonly string sid;
        OfficeProgress progress;
        string failure;
        DateTime last=DateTime.UtcNow;
        bool disposed;
        public OfficeProgress Progress { get { lock(gate) return progress; } }
        public OfficeBridge(string id,string userSid) {
            sid=userSid;
#if PORTABLE_TEST
            pipe=new NamedPipeServerStream("AfterSix.MatrixOffice."+id,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
#else
            var security=new PipeSecurity();
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(sid),PipeAccessRights.ReadWrite,AccessControlType.Allow));
            pipe=new NamedPipeServerStream("AfterSix.MatrixOffice."+id,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,security);
#endif
            reader=new Thread(Read); reader.IsBackground=true; reader.Start();
        }
        void Read() {
            try {
                pipe.WaitForConnection();
                using(var input=new StreamReader(pipe,Encoding.UTF8)) {
                    var line=new StringBuilder(); var json=new JavaScriptSerializer();
                    while(true) {
                        int c=input.Read(); if(c<0) break;
                        if(c=='\n') {
                            var value=json.Deserialize<OfficeProgress>(line.ToString()); line.Length=0;
                            if(value==null || value.UserSid!=sid || value.Pid<=0 || value.Actions<0 || value.BytesSaved<0) throw new InvalidDataException("Invalid Office helper report.");
                            lock(gate) { progress=value; last=DateTime.UtcNow; }
                        } else { if(line.Length>=8192) throw new InvalidDataException("Office report exceeds size limit."); line.Append((char)c); }
                    }
                }
                lock(gate) if(!disposed && (progress==null || progress.State!="COMPLETED" && progress.State!="FAILED")) failure="Office helper disconnected.";
            } catch(Exception e) { lock(gate) if(!disposed) failure=e.Message; }
        }
        public void Check() {
            lock(gate) {
                if(failure!=null) throw new IOException(failure);
                if(progress!=null && progress.State=="FAILED") throw new InvalidOperationException("Office activity failed: "+progress.Error);
                if((DateTime.UtcNow-last).TotalSeconds>90) throw new TimeoutException(progress==null ? "No Office helper connected from the signed-in user's display within 90 seconds." : "Office helper heartbeat missing for 90 seconds.");
            }
        }
        public Result Run(Options options,Func<bool> stopped,Action<Result> pulse) {
            var result=new Result(); var clock=System.Diagnostics.Stopwatch.StartNew();
            bool stopping=false; DateTime stopTime=DateTime.MaxValue;
            while(true) {
                Check(); var p=Progress;
                result.ElapsedSeconds=clock.Elapsed.TotalSeconds;
                if(p!=null) { result.Iterations=p.Actions; result.BytesWritten=p.BytesSaved; result.Office=p; }
                if(stopped() && !stopping) { stopping=true; stopTime=DateTime.UtcNow; }
                pulse(result);
                if(p!=null && p.State=="COMPLETED") {
                    result.Reason=stopping ? "stopped by operator" : "office workday completed"; return result;
                }
                if(stopping && (DateTime.UtcNow-stopTime).TotalSeconds>90) throw new TimeoutException("Office helper did not confirm document/window cleanup after stop.");
                if(!stopping && clock.Elapsed.TotalSeconds>options.Seconds+90) break;
                Thread.Sleep(1000);
            }
            throw new TimeoutException("Office cleanup did not finish within 90 seconds of its deadline.");
        }
        public void Dispose() { lock(gate) disposed=true; pipe.Dispose(); reader.Join(1000); }
    }
}
