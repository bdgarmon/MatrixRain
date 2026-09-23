using System.Runtime.InteropServices;
namespace MatrixOfficeActor {
    public static class PowerPointAutomation {
        public static void Start(dynamic application) {
            application.Visible=-1;
            application.AutomationSecurity=3;
        }
        public static void Restore(dynamic presentation) {
            dynamic windows=presentation.Windows; dynamic window=windows[1];
            try { window.WindowState=1; } // ppWindowNormal, including minimized windows.
            finally {
                if(Marshal.IsComObject(window)) Marshal.ReleaseComObject(window);
                if(Marshal.IsComObject(windows)) Marshal.ReleaseComObject(windows);
            }
        }
        public static void Activate(dynamic presentation) {
            dynamic windows=presentation.Windows;
            dynamic window=windows[1];
            try { window.Activate(); }
            finally {
                if(Marshal.IsComObject(window)) Marshal.ReleaseComObject(window);
                if(Marshal.IsComObject(windows)) Marshal.ReleaseComObject(windows);
            }
        }
    }
}
