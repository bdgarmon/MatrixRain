using System;
using System.Runtime.InteropServices;

namespace MatrixOfficeActor {
    internal static class WordWindow {
        internal static IntPtr Handle(object document) {
            dynamic doc=document;
            dynamic window=doc.ActiveWindow;
            try { return new IntPtr((int)window.Hwnd); }
            finally { if(Marshal.IsComObject((object)window)) Marshal.ReleaseComObject((object)window); }
        }
    }
}
