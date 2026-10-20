# Matrix Rain 3.2.0

**New in 3.2.0 (Windows/UEM version 3.2.0.0): visible document construction.** Microsoft 365 Testing begins with incomplete working files and adds slide content, component rows and Word paragraphs over successive foreground/edit intervals. Each file keeps its own progress. Story stages unlock later work instead of loading completed deliverables. Stop saves the current drafts without completing them automatically. The fictional Hardware Launch Studio theme, native Office documents and related Outlook drafts remain. See [HARDWARE-LAUNCH-STUDIO.md](HARDWARE-LAUNCH-STUDIO.md).

**Retained from 3.0:** continuous capped Random Rotation with screensaver rest; Microsoft 365 exclusion from rotation; Windows Workday; saved rain speed/spacing controls and native-resolution rendering. See [VERSION-3.md](VERSION-3.md).

**Introduced in 1.5.2:** Matrix Rain branding and the PowerPoint HWND correction.

The package and detection script are renamed. Installed executable names, registry keys and data folders retain `MatrixRainLab` for upgrade compatibility; configuration and run history are preserved. Start menu and existing all-users desktop shortcuts targeting this installation migrate to **Matrix Rain**. User-created private shortcuts are not rewritten.

**Retained from 1.5.1:** Office startup now tolerates brief status-file sharing conflicts, allowing atomic status replacement. Failed runs show a concise prompt to open a resizable, scrollable, copyable Last result window; full worker errors are retained. Upgrade using the new ZIP and detection script.

**Introduced in 1.5.0:** select **Microsoft 365 Testing** for a six-hour desktop Excel/PowerPoint, Chrome/Edge and New Outlook session with generated files under the signed-in user's Desktop and Documents. See [OFFICE-WORKDAY.md](OFFICE-WORKDAY.md) for exactly what is automated and the five-minute pilot. The corrected 32-bit UEM installer handoff remains included.

A Windows x64 Matrix display plus controlled CPU, memory, disk and local science workloads for the six-node DEX lab. Three prebuilt .NET Framework 4.8 executables are included; no Python or runtime downloads are required on the laptops. The original synthetic modes need no browser; Microsoft 365 Testing requires installed desktop Word, Excel, PowerPoint, New Outlook, and at least one of Microsoft Edge or Chrome. When Chrome is absent, it uses Edge.

**Retained from 1.4.2:** embedded Windows/shortcut icons, an always-visible running version, responsive administrator approval, alternate-account process monitoring, shared-data permissions and expanded elevation diagnostics. The previous CPU/OS reporting and power-export fixes remain included.

**Baseline workflow:** click **Go (admin)** once to authorize a baseline followed automatically by the selected workload. Default: 60 seconds of baseline, then 600 seconds of workload. Settings adjusts both durations. Rain is off during baseline and resumes in the workload phase; the display stays awake throughout.

The main screen shows the phase, progress bar, phase and total time remaining, and last result with the reason the test ended. **Last result** opens full phase timings and results. **Export logs** saves a local troubleshooting ZIP. Attempting to close an active experiment offers Return, Stop and exit (admin), or Leave running and exit. Stop and exit waits for acknowledgement; cancellation/failure keeps the display open.

Every device uses its actual hostname. Random rotation covers visualization, CPU, memory, disk, science and Windows Workday in shuffled sets. After every choice it inserts a separate screensaver-only rest. It runs until Stop, a safety failure, or shutdown. One approval covers transitions. Microsoft 365 Testing remains an explicit choice. Single tests can still run with an initial rain-off baseline. Keep the Matrix display open for app-helper launches and the screen-awake request.

- **Deploy/upgrade:** [deployment/UEM-DEPLOYMENT.md](deployment/UEM-DEPLOYMENT.md)
- **Run experiments:** [WORKLOADS.md](WORKLOADS.md)
- **Test coverage and limits:** [TEST-RESULTS.md](TEST-RESULTS.md)

## Display startup

After UEM installation, open **Matrix Rain** from Start, or sign out/in for automatic startup. Install/upgrade is silent and does not launch a GUI in SYSTEM's session. No stress workload starts automatically at installation or sign-in.

For portable use with a different administrator account, both accounts must have access to the extracted folder; prefer the UEM installation with its shared ProgramData permissions. To run portably, extract the ZIP, open its `MatrixRainLab` folder, double-click `Start.cmd`. The main-screen buttons use that folder automatically. For script-based portable runs, pass `-DataDirectory` pointing to it.

Controls: **Go (admin)**, **Stop (admin)**, **Last result**, **Export logs**, test dropdown, optional **Settings** (**W**), **Esc** exit, **F11** fullscreen, **Space** rain on/off when a button/dropdown does not have focus.

While the display is open, it requests both display and system wakefulness, including when minimized or running a baseline. This also applies to upgraded configurations that still contain `KeepAwake: false`. The request renews every 15 seconds and after resume, is released on exit, and is shown as **AWAKE REQUEST ACTIVE** or **FAILED**. It changes no permanent power-plan settings. Closing the display leaves any bounded workload running, but releases the awake request.

Windows can still enforce a lock/screensaver, lid action, explicit sleep, battery protection or enterprise power-request override. The request is not a guarantee against those conditions. See [Microsoft's power-request behavior](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate). On a pilot, use elevated `powercfg /requests` while the display is open and leave it untouched beyond its normal display timeout.

## What is displayed

Actual hostname; experiment label and selected/current workload; system CPU; used/total physical RAM; busiest GPU engine; dedicated/shared GPU memory usage where available; uptime; experiment wall time; workload state/source/heartbeat detail; and the display process's own CPU/working set.

CPU is measured busy time across logical processors, which can differ from Task Manager's sampling and frequency-adjusted values. GPU readings use available Windows PDH counters, sum processes on each engine, then select the busiest engine across adapters. Memory values show usage, not claimed VRAM capacity; Intel integrated graphics can use shared system RAM. Missing counters show N/A. Uptime includes sleep and can reflect a Fast Startup kernel session. The DEX footer explicitly states that collection is managed externally and is not verified by this app.

The sampler runs separately from rendering. Default sampling is five seconds; rain is capped at 15 FPS. Minimized windows stop painting but continue sampling. Measure actual overhead on the Latitudes; GDI rain is not a GPU stress benchmark.

## Display configuration

Installed config: `%ProgramData%\AfterSix\MatrixRainLab\config.json`. Changes require display restart. The runner separately reads the configured StateFile on every new run. Standard users normally read machine configuration; SYSTEM/admin control it.

| Setting | Default / purpose |
|---|---|
| LabTitle | AFTER SIX LAB, max 48 characters |
| NodeLabel | Legacy compatibility field; actual hostname is used on screen and in CSV |
| ExperimentLabel | DEX EXPERIENCE LAB, max 70 characters |
| WorkloadLabel | MATRIX DISPLAY, max 70 characters |
| WatchProcess | Empty, or a process name (optional .exe), no executable path |
| StateFile | state.json, relative to config folder or an absolute literal path; empty disables |
| FramesPerSecond | 15, allowed 1–30 |
| SampleSeconds | 5, allowed 1–60 |
| Monitor | 0, zero-based; unavailable index falls back to last monitor |
| Fullscreen | true |
| RainEnabled | true; baseline turns rain off, other acknowledged main-screen tests turn it on |
| KeepAwake | Legacy compatibility field; display always requests wakefulness until exit |
| EnableCsv | false; optional local sampled measurements |
| EnableGpu | true; disable to remove GPU counter sampling |

Unknown config keys and invalid ranges are rejected. JSON paths do not expand environment variables. No fleet.csv or per-node profiles are required or shipped. Old configuration labels cannot assign a workload.

State priority: valid state file → configured process presence → DISPLAY ONLY. A malformed state file shows an error. Runner state has a heartbeat; a RUNNING heartbeat older than 20 seconds shows WORKLOAD UNRESPONSIVE. Legacy/manual state files without a heartbeat remain reported state only. Process presence is not proof of progress.

The controller applies rain off/on at each acknowledged phase transition. They do not change config.json. Space still works after that request. The phase clock freezes when finished. Remaining times are upper time limits; disk work can finish at its write budget first. It advances as wall time during a run, including any suspension.

For external workloads, `Set-WorkloadState.ps1` still accepts IDLE, RUNNING, PAUSED, COMPLETED and FAILED; `-NewExperiment` resets the UTC start. On an installed app explicitly pass `-Path "$env:ProgramData\AfterSix\MatrixRainLab\state.json"`. Do not write this file manually while a built-in workload is running.

## Troubleshooting export

Click **Export logs**, choose a ZIP destination, and wait for the saved-path message. Export runs in the background without stopping the experiment. After choosing a destination, select Yes for administrator power diagnostics, No for a standard-user export, or Cancel. Yes elevates a separate read-only worker invocation; the display and ZIP writer remain under the signed-in user. It includes available app versions, hostname/OS details, config, default state file, current and last run, up to 20 result records and command errors, five installer transcripts, recent worker logs, display errors/diagnostics and the latest CSV if enabled. Power diagnostics call `powercfg /requests`, `/getactivescheme`, and `/a` with five-second command limits. Cancelling UAC or helper failure falls back to standard-user collection; REPORT.txt records the limitation. An elevated snapshot includes its collection UTC time. The helper runs only these three fixed read-only commands, creates no workload and does not acquire the workload lock. Ten small power snapshots are retained in the workloads folder.

`REPORT.txt` identifies missing, unreadable or truncated items and whether the display awake request was accepted at export start. This acceptance is not proof that the screen remained lit; inspect power/requests.txt and device behavior. Each collected file is capped at 2 MiB, with a 24 MiB file-data budget; scratch files are excluded. Custom StateFile locations outside the default data/state.json are not copied. Files are live snapshots, not a transactional backup. The bundle may contain hostnames, paths and configuration labels; it remains local and nothing is uploaded. Closing the display waits until export finishes.

## Optional logs and diagnostics

Display CSV: `%LOCALAPPDATA%\AfterSix\MatrixRainLab\logs`, at most five files around 10 MiB each. Includes UTC, node/labels, system and display metrics, state/source, rain setting and warnings. Missing values are N/A. CSV is off by default to avoid added disk activity.

Display errors/diagnostics: `%LOCALAPPDATA%\AfterSix\MatrixRainLab\last-error.txt` and `diagnostics.json`. SYSTEM validation errors use SYSTEM's local profile. Installer transcripts are now under `%ProgramData%\AfterSix\MatrixRainLab\deployment-logs` (last five). Workload logs/results are documented in WORKLOADS.md. Uninstall retains config/state/results/logs.

## Build and verification

Both executables target Windows 10/11 x64 with .NET Framework 4.8. Close the display and stop the worker before rebuilding on Windows:

```powershell
.\Build.ps1
.\tests\Test-Core.ps1
.\tests\Test-Workloads.ps1
.\tests\Test-Windows.ps1
.\tests\Test-WorkerWindows.ps1
```

Build.ps1 uses the compiler already in the Windows .NET Framework directory. Source stays compatible with C# 5. Binaries are unsigned; use your organization's signing process if required. Launcher examples use process-scoped execution-policy bypass, not a machine policy change.

The local process harness defines PORTABLE_TEST to substitute only the Windows platform/memory boundary and uses a JSON compatibility shim; shipped builds do not define that symbol. No Mac fallback is present in the release executables. See TEST-RESULTS.md for tested vs. pending Windows behavior.

## References

- [Microsoft CPU accounting](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes)
- [Microsoft GPU metrics](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/)
- [Omnissa Windows application deployment](https://techzone.omnissa.com/resource/deploying-workspace-one-uem-applications-windows-devices)

## Administrator boundary

The display has an `asInvoker` manifest. The worker has an embedded `requireAdministrator` manifest and also checks its administrator token at runtime, before writing control files or doing work. The main screen launches the fixed adjacent worker executable using Windows `runas`; it does not run commands through PowerShell or a shell. Cancelling UAC starts no command. A standard user needs administrator credentials when allowed by local policy. Already-elevated administrators and SYSTEM do not need a second elevation; prompt appearance follows Windows policy. No administrator credentials are saved and no always-running privileged service is installed.

Starting and stopping both require administrator rights. Viewing status, selecting parameters and editing settings do not. An ordinary user cannot bypass the worker check by passing a writable DataDirectory. Existing Freestyle/system scripts still work without interactive UAC because they already run as SYSTEM.

Workload command failures are saved as `workloads\error-<command-id>.txt` (last approximately 20) in the data directory. The UI reads the matching file, shows UAC cancellation separately, and does not claim a start until its run ID is acknowledged. A stale stop request cannot stop a later run.

[Microsoft application manifest documentation](https://learn.microsoft.com/en-us/windows/win32/sbscs/application-manifests).

## Earlier 1.4.1 pilot findings addressed

The six-device 1.4 pilot completed all latest baseline/workload sequences. Version 1.4.1 fixed two reporting defects and closes the administrator power-query gap. It does not claim to resolve the unexplained 38-minute historical pre-upgrade run; no cause was established. CSV remains opt-in, and DEX ingestion/display appearance still require device verification. Existing configuration and results are preserved.

[Microsoft supportedOS manifest reference](https://learn.microsoft.com/en-us/windows/win32/sbscs/application-manifests).

## 3.2.0: Windows icons, visible version and alternate-admin elevation

The approved green M icon is embedded in both executables and set on the display window and installed Start menu shortcut. Shortcuts targeting the display EXE use that embedded icon; the installer also refreshes an existing all-users desktop shortcut named Matrix Rain if it targets this app. It does not create another desktop shortcut. Windows may cache an old pinned icon: unpin/re-pin the updated Start menu entry if needed. `MatrixRainLab.ico` contains 16, 24, 32, 48, 64, 128 and 256px images; `MatrixRainLab-UEM.png` is the 256px catalog image.

The top-right display header always shows **v3.2.0**, including in fullscreen and during metrics warnings. The title bar also includes the version. Both derive from the running assembly; the EXE file version is 3.2.0.0.

Go and Stop request Windows elevation on a separate STA thread, keep animation/status responsive and prevent duplicate requests while approval is pending. The display stays unelevated. A different administrator account can approve the worker without the display reopening that account's process for monitoring. Worker status files remain authoritative; an unavailable process query is diagnostic information, not a failed workload. UAC is still required. No credentials are stored, no service is installed, and no automatic privileged task is added.

The installer sets the app's shared ProgramData directory and existing contents to SYSTEM/Administrators full control and built-in Users read/execute using language-independent SIDs. This lets a normal desktop user read results created by a different elevated administrator while keeping workload control files protected. Any custom StateFile outside that directory needs its own matching permissions. Configuration edits in the installed shared directory require elevation.

A slow domain credential prompt is not yet diagnosed. The app cannot guarantee how quickly Windows displays it. `elevation.log` records the request, shell return/error, worker acknowledgement and exit errors; the elapsed shell time includes time spent entering credentials. Export logs now includes those bounded per-user logs, the display identity and shared-file ACLs. Worker startup logs record the elevated identity and data path; worker errors include the stage and full exception. Exports stay local.

After upgrading, validate one AD standard-user device and one Entra ID device. Run `tests\Test-StandardUserWindows.ps1` unelevated, then try Go cancellation, Go with alternate admin credentials, Stop and a full short sequence. If a failure persists, export from the normal desktop user's display immediately afterward, select **No** for optional elevated power diagnostics if that prompt also fails, and provide the ZIP plus the exact visible error and approximate click time. Native UAC, Windows ACL enforcement, icon caching and domain prompt latency could not be validated on the macOS build host.

## Runtime prerequisite

Matrix Rain requires **.NET Framework 4.8 or 4.8.1**, not modern **.NET 10**. These are separate runtimes. Windows 11 includes a compatible Framework version; installing .NET 10 is not required for Matrix Rain and does not replace Framework. See [Microsoft's installation guidance](https://learn.microsoft.com/en-us/dotnet/framework/install/on-windows-and-server).
