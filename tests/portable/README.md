# Development-only process harness

Requires .NET SDK 10 and Python 3 on the development machine, not on deployed Windows laptops.

```
dotnet build TestHost.csproj
python3 check.py
```

This compiles the production worker/engine with PORTABLE_TEST: Windows platform/memory calls are substituted with an 8-GiB fixture, and the unavailable Framework JSON serializer is represented by Serializer.cs. It runs small real workloads, tests independent process locking/cancellation/crash recovery, and creates data only in a temporary directory. It does not validate Windows native APIs, Windows PowerShell launchers, or UEM.

Run `python sequence.py` after building TestHost.csproj for the baseline/workload sequence checks. The separate tests/controller/ControllerTests.csproj exercises the main controller and troubleshooting exporter using temporary fixtures.

Run `python power.py` for helper isolation, rejection and retention checks. PORTABLE_TEST skips native Windows authentication and reports powercfg unavailable on non-Windows hosts.
