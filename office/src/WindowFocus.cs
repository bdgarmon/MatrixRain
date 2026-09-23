using System;
using System.Runtime.InteropServices;
using System.Threading;
using MatrixOffice;
namespace MatrixOfficeActor {
    internal static partial class Native {
        static readonly SyntheticInputClock inputClock=new SyntheticInputClock();
        [StructLayout(LayoutKind.Sequential)] struct KeyboardInput {
            public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra;
        }
        // All shipped executables are x64. INPUT includes an aligned 32-byte union.
        [StructLayout(LayoutKind.Explicit,Size=40)] struct Input {
            [FieldOffset(0)] public uint Type;
            [FieldOffset(8)] public KeyboardInput Keyboard;
        }
        [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,Input[] inputs,int size);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flags);
        internal static double InputIdleSeconds() {
            var info=new LastInput {Size=(uint)Marshal.SizeOf(typeof(LastInput))};
            return GetLastInputInfo(ref info) ? unchecked((uint)Environment.TickCount-inputClock.UserTick(info.Tick))/1000.0 : 0;
        }
        static bool SafeToFocus(IntPtr window) {
            if(!Exists(window) || !DesktopAvailable() || InputIdleSeconds()<15) return false;
            // Also avoid combining the fallback with a held mouse button or key.
            for(int key=1;key<255;key++) if((GetAsyncKeyState(key)&0x8000)!=0) return false;
            return true;
        }
        static bool ConfirmFocus(IntPtr window) {
            var active=GetForegroundWindow();
            return !IsIconic(window) && active!=IntPtr.Zero &&
                (active==window || GetAncestor(active,3)==GetAncestor(window,3));
        }
        internal static bool Foreground(IntPtr window,out string detail) {
            string trace="before HWND="+GetForegroundWindow().ToInt64()+" target HWND="+window.ToInt64();
            bool ok=ForegroundFallback.Run(()=>SafeToFocus(window),delegate {
                return ForegroundSwitch.Run(
                    delegate { ShowWindowAsync(window,IsIconic(window) ? 9 : 5); },
                    delegate {
                        if(!SafeToFocus(window)) return;
                        uint ignored, current=GetCurrentThreadId();
                        uint foreground=GetWindowThreadProcessId(GetForegroundWindow(),out ignored);
                        uint target=GetWindowThreadProcessId(window,out ignored);
                        bool attachedForeground=foreground!=0 && foreground!=current && AttachThreadInput(current,foreground,true);
                        bool attachedTarget=target!=0 && target!=current && target!=foreground && AttachThreadInput(current,target,true);
                        try { BringWindowToTop(window); bool granted=SetForegroundWindow(window); if(granted) SetFocus(window); }
                        finally {
                            if(attachedTarget) AttachThreadInput(current,target,false);
                            if(attachedForeground) AttachThreadInput(current,foreground,false);
                        }
                    },()=>ConfirmFocus(window),()=>Thread.Sleep(100));
            },delegate {
                if(!SafeToFocus(window)) return false;
                var prior=new LastInput {Size=(uint)Marshal.SizeOf(typeof(LastInput))};
                if(!GetLastInputInfo(ref prior)) return false;
                uint tick=unchecked((uint)Environment.TickCount); if(tick==0) return false;
                var down=new Input {Type=1,Keyboard=new KeyboardInput {Key=0x12,Time=tick}};
                var up=new Input {Type=1,Keyboard=new KeyboardInput {Key=0x12,Flags=2,Time=tick}};
                uint sent=SendInput(2,new[]{down,up},Marshal.SizeOf(typeof(Input)));
                trace+=" Alt fallback events="+sent;
                if(sent==1) SendInput(1,new[]{up},Marshal.SizeOf(typeof(Input))); // Release if only key-down was accepted.
                if(sent>0) inputClock.Record(tick,prior.Tick);
                Thread.Sleep(50);
                return sent==2;
            });
            detail=trace+" after HWND="+GetForegroundWindow().ToInt64()+" after PID="+WindowPid(GetForegroundWindow());
            return ok;
        }
    }
}
