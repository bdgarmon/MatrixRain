# Matrix Rain 3.2.0 / UEM version 3.2.0.0

## Daily operation

Sign into Windows, let Matrix Rain open, select **Random rotation**, click **Go (admin)** and approve. Leave the display running. At the end of the workday choose **Stop (admin)**, wait for the stopped result and app cleanup, then shut down normally. This version does not schedule shutdown or start pressure automatically at sign-in.

## Continuous Random Rotation

The worker owns one continuous run ID and exclusive lease. It shuffles visualization, CPU, memory, disk, science and Windows Workday. Microsoft 365 Testing never participates. Every randomly selected phase is followed by a separate visualization/rest phase with rain and telemetry but no synthetic pressure. Both activity and recovery phases last a random 3–12 minutes. A shuffled set covers all six choices before reshuffling. Visualization may therefore be followed by more rest; that is intentional.

Rotation uses its own presets rather than the manual pressure controls:

| Parameter | Rotation policy |
|---|---|
| CPU/science duty | Random 15–55% per worker |
| Threads | At most two; one on one/two/three logical CPU devices |
| RAM touched | Random 128–512 MiB |
| Disk scratch | 32 MiB |
| Disk rate ceiling | 2 MiB/s before intensity scaling |
| Write budget | 128 MiB per disk phase |
| Rest | 3–12 minutes after every randomly selected phase |

These are pressure limits, not a target for measured whole-machine utilization. Browsers, Windows maintenance and rendering also consume resources. This creates uneven activity and long quiet intervals; it is not a calibrated human-behavior trace or a temperature-control system. Existing free-memory and free-disk guards remain active. A helper failure or resource guard ends the run with a visible failed result rather than endlessly retrying it.

The phase progress and remaining time apply to the current phase. Total time displays **UNTIL STOPPED**. Shared `workloads/run.json` contains current phase and actual pressure parameters. `last-phase.json` retains the most recently finished phase; `Phases` retains the latest 32 entries so a continuous run does not grow its status file indefinitely. Stop cancels the entire session and waits for any app cleanup. It does not automatically start another phase afterward.

## Applications

The former Microsoft 365 Testing/Microsoft 365 Testing label is now **Microsoft 365 Testing**. Its internal mode is still `office` for compatibility; its document/browser/Outlook behavior is retained. It must be chosen explicitly.

**Windows Workday** is an independent `windows` mode, available directly or through rotation. Standalone duration is 60 seconds to eight hours, plus optional baseline. It uses only Notepad, File Explorer and Edge. See [WINDOWS-WORKDAY.md](WINDOWS-WORKDAY.md).

## Rain appearance and UHD

Open **Settings** (or press W). Adjust speed from **0.25× to 6×**, and horizontal column spacing from **0.75× to 4×**. Smaller spacing produces denser rain. Apply saves those appearance values for this signed-in user under `%LOCALAPPDATA%\AfterSix\MatrixRainLab\appearance.json`; administrator approval is not required. Shared `config.json` supports `RainSpeed` (default 2.0) and `RainSpacing` (default 1.25). Per-user saved preferences override those two shared values.

The display renders rain directly at the active monitor's pixel dimensions, with antialiased glyphs, bright leading characters, subtle head glow and longer tails. Per-monitor DPI awareness prevents Windows from simply stretching a low-resolution canvas. A monitor set to **3840×2160** gets a native UHD canvas. A 4K-capable panel running at a lower Windows resolution must first be configured to its recommended/native resolution in Windows Display Settings. Matrix Rain does not change display modes. OLED panels retain black backgrounds with brighter green trails; this is SDR rendering, not HDR or a GPU benchmark.

UHD and dense settings increase rendering cost. The existing 15 FPS cap remains the default to limit display overhead. There is no claim of on-device frame-rate or CPU validation until the pilot is performed.

## Deployment

Use `MatrixRain-3.2.0-UEM.zip`, matching `Detect-MatrixRain-3.2.0.ps1`, and [UEM-SETUP.md](UEM-SETUP.md). Both UEM version fields must be **3.2.0.0**. Existing install/data paths and auto-launch at interactive sign-in remain compatible. Installation starts no workload. Existing Microsoft 365 workloads must be stopped before upgrading. Framework requirement remains .NET Framework 4.8/4.8.1; the separate .NET 10 runtime is not required.

## Windows pilot checklist

1. Deploy to one AD/Entra test endpoint and confirm UEM detection passes and v3.2.0 appears.
2. On the fresh Windows 11 image, run Windows Workday for ten minutes. Confirm a new Notepad journal, Explorer and Edge appear; journal updates save; foreground apps/tabs rotate; Desktop/Documents files remain after cleanup.
3. Start Random Rotation once. Observe multiple phases including Windows Workday and screensaver rest, then Stop during an app phase. Confirm cleanup, IDLE status and no new phase. Check DEX foreground/background data.
4. Run six to eight hours and compare CPU behavior with the phase timestamps. Confirm memory/disk use remains acceptable and no screen sleep occurs under the device's policies.
5. On a 3840×2160 OLED panel, try speed/spacing sliders at several Windows scaling values. Confirm crisp native-pixel rain and acceptable overhead. Confirm preferences persist after relaunch.

Compilation and portable tests run locally on macOS. Interactive Windows apps, UAC, Windows 11 26H2, OLED quality, display-sleep policy and UEM upgrade behavior require this endpoint pilot. Do not infer those results from compilation.
