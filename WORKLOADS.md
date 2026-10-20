# Workloads: start here

**Version 3:** [Continuous rotation and rain settings](VERSION-3.md), [Windows Workday](WINDOWS-WORKDAY.md), and explicit [Microsoft 365 Testing](OFFICE-WORKDAY.md). The Freestyle scripts below remain available for the original six short modes; app modes and continuous rotation are started from the signed-in user's display.

Version 1.4 runs a baseline and then a workload in one elevated worker process. DEX can distinguish `MatrixLabWorker.exe` from `MatrixRainLab.exe` overhead.

## Start from the main screen

1. Open Matrix Rain. Choose **Random rotation** for an indefinite day of varied activity and rest, or choose an individual test.
2. Click **Go (admin)** and approve once. Rotation uses its own randomized capped presets, excludes Microsoft 365, and keeps moving through phases until stopped or a failure occurs. Keep the display process open for wake requests and app helpers.
3. For an individual test, **Settings** adjusts its duration and optional 0–600 second baseline. Short test total duration cannot exceed 3600 seconds; Microsoft 365 Testing and Windows Workday allow up to eight hours plus baseline. An initial baseline turns rain off; workload/rest phases restore it.
4. Watch current phase, progress and time left. Rotation shows total time as **UNTIL STOPPED**. Disk work may finish early at its write budget; stale heartbeats freeze progress.
5. **Stop (admin)** cancels the entire session and waits for app cleanup. A single test completes after its selected duration; Random Rotation starts its next phase automatically until you stop it. Safety failures end the session visibly.
6. The bottom line preserves the last finished result while another experiment runs. **Last result** shows UTC phase timings, result measurements and the stop reason. **Export logs** includes those records and available diagnostics in a local ZIP.

Pressing Esc or closing during an active run opens the existing exit choices. Use **Stop and exit** for a controlled end. Leaving the display closed releases its wake request and prevents future app-helper launches, so it is unsuitable for a continuous Windows Workday rotation. Windows shutdown/forced process termination is not blocked.

## Random selection and baseline

Random Rotation shuffles visualization, CPU, memory, disk, science and Windows Workday. Every selected phase is followed by a screensaver-only rest. Both last a random 3–12 minutes. Microsoft 365 Testing is excluded. The same elevated worker/run ID spans the indefinite rotation, including helper cleanup and rest. See [VERSION-3.md](VERSION-3.md) for pressure caps and result retention.

Each device now supplies its own baseline observation interval before pressure begins. Compare the phase UTC timestamps and hostname/run ID in DEX; the baseline includes display telemetry and awake overhead but no rain or synthetic pressure. This does not guarantee a simultaneous fleet-wide control device or thermal cooldown. If you need a dedicated concurrent control, explicitly choose **Baseline** on any machine; it runs one standalone baseline for the workload-duration setting, with no duplicate prelude. No hostname mapping or per-device ZIP is needed.

## Optional six-node Freestyle scripts

Legacy scripts retain their single-phase behavior unless Start-Workload.ps1 is given -BaselineSeconds. The scripts below are optional automation hooks for Freestyle workflows. They are no longer the primary way to control workloads. Use the main screen above for local operation.


Upgrade the display package to 3.2.0 first. For central orchestration, use the appropriate file from `uem` in a **SYSTEM-context Freestyle script step** for that node. Give each run script at least a **12-minute execution timeout** for its default 10-minute run. These are script actions, not another application-install assignment.

| Script | Node | What it does |
|---|---|---|
| `Run-Node01.ps1` | 01 | Ten-minute baseline marker, no synthetic pressure; requests rain off |
| `Run-Node02.ps1` | 02 | Ten-minute visualization marker; requests rain on in the existing display |
| `Run-Node03.ps1` | 03 | CPU: two workers, each targeting 50% duty |
| `Run-Node04.ps1` | 04 | Memory: allocate/touch 512 MiB while maintaining a free-memory reserve |
| `Run-Node05.ps1` | 05 | Disk: 64 MiB scratch file, about 5 MiB/s target writes, at most 1 GiB written |
| `Run-Node06.ps1` | 06 | Local science: Monte Carlo estimate of pi, two workers at 50% duty |
| `Stop-Node.ps1` | Any | Request cancellation of that node's current run |
| `Get-NodeStatus.ps1` | Any | Return status, heartbeat, parameters and results as JSON |

The disk preset usually ends at its write budget before ten minutes (roughly 205 seconds at the target rate, potentially longer with I/O overhead). All modes stop by their time limit in normal operation. A stuck OS/driver operation can delay shutdown; the coordinator waits up to five seconds per worker and reports a shutdown failure if a worker does not exit.

Baseline and visualization are controlled observation periods, not extra stress generators. The display must already be running in a signed-in desktop to show rain. The worker cannot show a GUI from SYSTEM. The worker sends phase-specific rain state; Space remains usable afterward. On completion the display continues running with that setting. For a true idle baseline, close the display too.

The science test performs a real local numerical calculation and records its sample count/estimate. It is not BOINC, SETI or Folding@home, does not download tasks, and does not contribute to an external project.

## Optional administrator commands and parameter reference

On the laptop, use Administrator Windows PowerShell. On UEM use SYSTEM context. Machine control files live under ProgramData and standard users are not granted write access.

```powershell
$start = Join-Path $env:ProgramW6432 'AfterSix\MatrixRainLab\Start-Workload.ps1'
& $start -Mode Cpu -BaselineSeconds 60 -DurationSeconds 600 -Intensity 50 -Threads 2 -Wait
```

Other examples (run one at a time):

```powershell
& $start -Mode Memory -MemoryMiB 2048 -Intensity 50 -DurationSeconds 600 -Wait
& $start -Mode Disk -DiskMiB 64 -DiskMiBPerSecond 20 -Intensity 50 -WriteLimitMiB 2048 -DurationSeconds 300 -Wait
& $start -Mode Science -Threads 2 -Intensity 75 -DurationSeconds 600 -Wait
& $start -NodeId '03' -DurationSeconds 120 -Wait
```

Use `-Mode` OR `-NodeId`, not both. `NodeId` selects the table above; it does not rename the device or modify its installed node identity.

| Parameter | Default | Allowed / meaning |
|---|---|---|
| DurationSeconds | 600 | 1–3600; workload-phase limit |
| BaselineSeconds | 0 in scripts; 60 in UI | 0–600 in scripts; adds a baseline before a non-baseline workload; combined limit 3600 |
| Intensity | 50 | 1–95; mode-specific, described below |
| Threads | 2 | 1–32; capped to available logical processors; CPU/science only |
| MemoryMiB | 512 | 8–4096; explicit allocation, memory mode only |
| DiskMiB | 64 | 4–256; maximum scratch-file size |
| WriteLimitMiB | 1024 | At least DiskMiB, at most 4096; total logical writes per run |
| DiskMiBPerSecond | 10 | 1–256; disk write-rate ceiling before applying intensity |
| DataDirectory | Machine ProgramData folder | Control files and config location; optional for a portable/test installation |
| Wait | Off | Wait for completion and propagate failure; use this with UEM |

CPU/science intensity is a duty-cycle target **per selected worker**, not a promise of whole-machine utilization. Two workers at 50% use roughly one logical CPU's worth of compute. CPU frequency, scheduler, background work and hardware affect observed utilization. The scheduler uses roughly 100 ms work/sleep slices.

Memory intensity changes how often allocated pages are touched; allocation stays at MemoryMiB. Startup and ongoing checks preserve a measured reserve of at least 1 GiB or 20% of physical RAM, whichever is larger. If the reserve is crossed, the run fails and releases its allocation. Other processes can consume memory between checks; this is not an OS reservation.

Disk intensity scales the configured write ceiling: `DiskMiBPerSecond × Intensity / 100`. Writes use a private sequential scratch file and write-through mode; reads can still come from the OS cache. This is useful synthetic I/O, not a calibrated storage benchmark. The worker checks for at least 2 GiB free plus one write block before writing. It stops at the write budget or time limit, whichever comes first. Reports count application bytes, not physical SSD writes/wear.

`-Wait` is preferred for UEM because the script remains alive and reports the run's final exit status. Without `-Wait`, the launcher returns after startup is acknowledged; that is useful in a local admin shell but is not a guarantee that a remote execution host will preserve child processes after its job ends.

## Stop and status

From another elevated window, or an independently executed UEM script:

```powershell
& (Join-Path $env:ProgramW6432 'AfterSix\MatrixRainLab\Stop-Workload.ps1')
& (Join-Path $env:ProgramW6432 'AfterSix\MatrixRainLab\Get-WorkloadStatus.ps1')
```

Stop requests are tied to a unique run ID, so an old request cannot stop a newer run. Cancellation normally takes under a second, followed by file cleanup. The stop script waits up to 15 seconds for acknowledgement. If your UEM queues scripts serially, use a local elevated shell to stop an active `-Wait` run, or wait for its configured deadline.

One worker per machine data directory is enforced with an OS-held file lock across sessions. A duplicate start fails instead of increasing pressure. Do not use alternate data directories to run overlapping tests on the same laptop.

## Display states and results

- **RUNNING**: the runner is executing; it publishes a heartbeat every two seconds.
- **COMPLETED**: duration or disk write budget reached. Science also shows its pi estimate.
- **IDLE / stopped by operator**: cancellation processed successfully.
- **FAILED**: a reserve, I/O, configuration or worker error stopped the run.
- **HEARTBEAT STALE**: the display has not received a valid running heartbeat for more than 20 seconds. This can mean a crash, suspension or blocked worker; it does not assert that the process is gone.

The main-screen controller polls the worker every second; metrics sample separately every five seconds by default. The elapsed clock freezes at the reported result duration after completion. Do not use `Set-WorkloadState.ps1` concurrently with a worker: the runner owns the state file during the run.

The runner honors `StateFile` in `%ProgramData%\AfterSix\MatrixRainLab\config.json`. A missing config defaults to `state.json` in the data directory; an explicitly disabled StateFile rejects workload startup. Custom state paths must be writable by the worker and readable by the display.

Machine records are under `%ProgramData%\AfterSix\MatrixRainLab\workloads`:

- `run.json`: current/latest experiment, PID, run ID, phase/index/count, elapsed and planned duration, UTC timestamps, options and current phase result.
- `last-result.json`: most recent terminal experiment, retained while another runs.
- `Phases` in a finished record preserves each completed phase’s timestamps and result, or its error.
- `result-<run-id>.json`: last 20 run results; science includes iterations/hits/pi, disk includes bytes written/read, memory includes allocated MiB.
- `worker-<run-id>.out.txt` / `.err.txt`: last 20 stdout and stderr files from launcher runs.
- `scratch\io-<unique-id>.tmp`: private disk file, deleted on normal completion/cancellation. After a crash, the next worker removes only files matching its own naming pattern while holding the exclusive lock.

Reboot kills the worker and releases its allocation; runs do not resume automatically. The display flags a stale RUNNING heartbeat. A new start acquires the released lock and creates a new experiment. A crash can leave one scratch file (maximum 256 MiB) until the next run. Upgrading/uninstalling requests a graceful stop first and aborts if the installed worker does not exit, instead of deleting its executable while it is still working.

## Pilot before assigning all six

1. Upgrade one node, then launch the updated display from Start or sign in again.
2. Run `tests\Test-Windows.ps1 -Installed`, then `tests\Test-WorkerWindows.ps1 -Installed` from the extracted package.
3. Run a 60-second CPU experiment. Confirm RUNNING → COMPLETED, a numeric result duration, and the worker process in DEX/Task Manager.
4. Start a longer run and issue Stop from another elevated window. Confirm IDLE and no running worker. Try a duplicate start; it should be rejected.
5. Try modest memory/disk runs, then increase only the dimension being tested. Compare the same display setting, OS/driver versions, power plan and AC state across nodes.
6. Use UTC timestamps/run IDs/hostname to correlate with your existing Omnissa collection. The app does not configure or verify DEX ingestion.

Local tests cover real small workloads and process lifecycle on macOS with Windows adapters substituted in the process harness. The supplied Windows executables compile against .NET Framework 4.8. Windows GUI/driver behavior, SYSTEM permissions and actual UEM script lifecycle still require this pilot; see TEST-RESULTS.md.
