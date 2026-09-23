using System;
namespace MatrixRainLab {
    public sealed class DisplayAwake : IDisposable {
        readonly Func<uint,uint> setState;
        bool disposed;
        public DisplayAwake(Func<uint,uint> setter) { setState=setter; }
        public bool Renew() {
            if(disposed) return false;
            return setState(0x80000003u) != 0;
        }
        public void Dispose() { if(!disposed) { disposed=true; setState(0x80000000u); } }
    }
}
