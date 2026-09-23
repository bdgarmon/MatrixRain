# Real bounded workload tests, runnable on PowerShell 7/macOS or Windows PowerShell 5.1.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$testSource=@'
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MatrixLabWorkloads;
public static class WorkloadTests {
    static int passed;
    static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
    static MemoryInfo Plenty() { return new MemoryInfo { Total = 8L * 1073741824, Available = 6L * 1073741824 }; }
    static long DiskSpace() { return 5L * 1073741824; }
    static bool Never() { return false; }
    static void Pulse(Result r) { }
    static Engine Create(Options o, string d) { return new Engine(o,d,Plenty,DiskSpace,Never,Pulse); }
    public static void Run(string folder) {
        foreach (string mode in new [] { "baseline","visualization","cpu","memory","science" }) {
            var o = new Options { Mode=mode, Seconds=1, Intensity=10, Threads=1, MemoryMiB=8 };
            var wall = Stopwatch.StartNew(); Result r = Create(o,folder).Run();
            Check(r.Reason == "duration reached" && wall.Elapsed.TotalSeconds < 4, mode + " terminates at deadline");
            if (mode=="memory") Check(r.AllocatedMiB==8,"Memory touches requested allocation");
            if (mode=="science") Check(r.Iterations > 1000 && r.Pi > 2.5 && r.Pi < 3.8,"Science computes plausible pi from real samples");
            if (mode=="cpu") { Check(r.Iterations > 0,"CPU performs real work"); Check(!r.Pi.HasValue,"CPU results do not contain a scientific estimate"); }
        }
        foreach (string mode in new [] { "cpu","memory","disk","science" }) {
            var clock = Stopwatch.StartNew();
            var engine = new Engine(new Options { Mode=mode, Seconds=10, Intensity=10, Threads=1, MemoryMiB=8, DiskMiB=4, WriteLimitMiB=4096 },folder,Plenty,DiskSpace,delegate { return clock.ElapsedMilliseconds > 200; },Pulse);
            Result r=engine.Run(); Check(r.Reason=="stopped by operator" && clock.Elapsed.TotalSeconds<4,mode + " cancels promptly");
        }
        bool rejected=false;
        try { Create(new Options { Seconds=3601 },folder).Run(); } catch (ArgumentException) { rejected=true; }
        Check(rejected,"Excessive duration rejected");
        rejected=false;
        try { Create(new Options { Intensity=100 },folder).Run(); } catch (ArgumentException) { rejected=true; }
        Check(rejected,"100 percent duty rejected");
        rejected=false;
        try { new Engine(new Options { Mode="memory", MemoryMiB=8 },folder,delegate { return new MemoryInfo { Total=8L*1073741824, Available=1073741824 }; },DiskSpace,Never,Pulse).Run(); } catch (InvalidOperationException) { rejected=true; }
        Check(rejected,"Insufficient memory reserve prevents allocation");
        int checks=0; rejected=false;
        try { new Engine(new Options { Mode="memory", Seconds=3, MemoryMiB=8 },folder,delegate { checks++; return checks<4 ? Plenty() : new MemoryInfo {Total=8L*1073741824,Available=1073741824}; },DiskSpace,Never,Pulse).Run(); } catch (InvalidOperationException) { rejected=true; }
        Check(rejected,"Falling available RAM stops an active memory run");
        rejected=false;
        try { new Engine(new Options { Mode="disk" },folder,Plenty,delegate { return 1073741824; },Never,Pulse).Run(); } catch (InvalidOperationException) { rejected=true; }
        Check(rejected,"Disk free-space reserve enforced");
        File.WriteAllText(Path.Combine(folder,"sentinel.txt"),"do not alter");
        Result disk=Create(new Options {Mode="disk",Seconds=10,DiskMiB=4,WriteLimitMiB=8,Intensity=50},folder).Run();
        Check(disk.Reason=="write budget reached" && disk.BytesWritten==8L*1048576,"Disk write budget is exact");
        Check(disk.BytesRead>0,"Disk reads the generated data");
        Check(Directory.GetFiles(folder,"io-*.tmp").Length==0,"Disk scratch cleaned after completion and cancellation");
        Check(File.ReadAllText(Path.Combine(folder,"sentinel.txt"))=="do not alter","Unrelated files preserved");
        string leasePath=Path.Combine(folder,"lease.test");
        using(var lease=new FileStream(leasePath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
            rejected=false; try { using(var other=new FileStream(leasePath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {} } catch(IOException) { rejected=true; }
            Check(rejected,"Lease excludes a second writer");
        }
        using(var reacquired=new FileStream(leasePath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) Check(true,"Lease reacquired after release");
        Console.WriteLine("TOTAL: " + passed + " workload checks passed.");
    }
}
'@
$temp=Join-Path ([IO.Path]::GetTempPath()) ('MatrixWorkloadTests-'+[guid]::NewGuid().ToString('N'))
New-Item $temp -ItemType Directory | Out-Null
try {
    $testFile=Join-Path $temp 'WorkloadTests.cs'
    [IO.File]::WriteAllText($testFile,$testSource)
    Add-Type -Path @((Join-Path $root 'workloads/src/Engine.cs'),(Join-Path $root 'shared/OfficeModel.cs'),$testFile)
    [WorkloadTests]::Run($temp)
} finally { Remove-Item $temp -Recurse -Force }
