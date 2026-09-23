using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace MatrixRainLab {
    internal interface IElevatedProcess : IDisposable {
        bool TryGetExitCode(out int code);
    }

    // Use the handle returned by the shell. Do not reopen a different user's
    // process through Process.HasExited/ExitCode with broader access rights.
    internal sealed class ElevatedProcess : IElevatedProcess {
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        struct ShellInfo {
            public int Size;
            public uint Mask;
            public IntPtr Window;
            public string Verb, File, Parameters, Directory;
            public int Show;
            public IntPtr Instance, IdList;
            public string Class;
            public IntPtr ClassKey;
            public uint HotKey;
            public IntPtr Icon, Process;
        }
        [DllImport("shell32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        static extern bool ShellExecuteEx(ref ShellInfo info);
        [DllImport("kernel32.dll", SetLastError=true)]
        static extern bool GetExitCodeProcess(IntPtr process, out uint code);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        IntPtr handle;
        bool queryFailed;
        ElevatedProcess(IntPtr value) { handle=value; }
        public static IElevatedProcess Start(string file,string args,IntPtr owner) {
            var info=new ShellInfo { Size=Marshal.SizeOf(typeof(ShellInfo)),
                // NOCLOSEPROCESS | NOASYNC (this STA has no message loop) | FLAG_NO_UI.
                // NO_UI suppresses shell error boxes, never the security prompt.
                Mask=0x40|0x100|0x400, Window=owner, Verb="runas", File=file,
                Parameters=args, Directory=Path.GetDirectoryName(file), Show=0 };
            if(!ShellExecuteEx(ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return new ElevatedProcess(info.Process);
        }
        public bool TryGetExitCode(out int code) {
            code=0;
            if(handle==IntPtr.Zero || queryFailed) return false;
            uint native;
            if(!GetExitCodeProcess(handle,out native)) {
                queryFailed=true;
                // Status files remain authoritative if the process is not queryable.
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if(native==259) return false;
            code=unchecked((int)native); return true;
        }
        public void Dispose() { if(handle!=IntPtr.Zero) { CloseHandle(handle); handle=IntPtr.Zero; } }
    }

    // Only this object crosses threads. Session/UI state stays on the UI thread.
    internal sealed class ElevationOperation : IDisposable {
        readonly object gate=new object();
        IElevatedProcess process;
        Exception error;
        bool complete, disposed;
        public bool Complete { get { lock(gate) return complete; } }
        public Exception Error { get { lock(gate) return error; } }
        public ElevationOperation(Func<IElevatedProcess> launch) {
            var thread=new Thread(delegate() {
                IElevatedProcess result=null; Exception failure=null;
                try { result=launch(); } catch(Exception e) { failure=e; }
                lock(gate) {
                    if(disposed) { if(result!=null) result.Dispose(); }
                    else { process=result; error=failure; }
                    complete=true;
                }
            });
            thread.IsBackground=true; thread.Name="Matrix administrator approval";
            if(Environment.OSVersion.Platform==PlatformID.Win32NT) thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        public bool TryGetExitCode(out int code) {
            lock(gate) { code=0; return process!=null && process.TryGetExitCode(out code); }
        }
        public void Dispose() { lock(gate) { disposed=true; if(process!=null) process.Dispose(); process=null; } }
    }

    internal static class LaunchDiagnostics {
        static readonly object Gate=new object();
        public static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AfterSix","MatrixRainLab"); } }
        public static void Write(string directory,string message) {
            try {
                lock(Gate) {
                    Directory.CreateDirectory(directory);
                    string path=Path.Combine(directory,"elevation.log");
                    if(File.Exists(path) && new FileInfo(path).Length>262144) {
                        string old=path+".1"; if(File.Exists(old)) File.Delete(old); File.Move(path,old);
                    }
                    File.AppendAllText(path,DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine);
                }
            } catch { /* A diagnostic failure must not launch another command or break the UI. */ }
        }
    }
}
