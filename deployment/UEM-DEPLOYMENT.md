# Workspace ONE UEM: upgrade to 3.2.0

This release replaces local research tabs with live websites and installs a separately configurable browser-sites.json. Existing settings and custom site lists are preserved. Update both the package filename and installation command; the ZIP now extracts to MatrixRain/. Installed paths remain unchanged for upgrades. Upload MatrixRain-3.2.0-UEM.zip as the next version of the existing app. The installer preserves configuration/history, installs all three executables and retains the corrected native-PowerShell bootstrap. Desktop Microsoft 365 and New Outlook must already be installed; this package does not install Office or sign users in.

**Do not repackage the supplied ZIP.** Its top-level folder is deliberately named `MatrixRain`, matching the updated command below. The command below handles extraction explicitly; it does not assume UEM has already extracted the content.

## Install settings

| Setting | Value |
|---|---|
| App version | 3.2.0 |
| Platform / architecture | Windows Desktop / x64 |
| Context / admin rights | Device/SYSTEM / required |
| Installer success exit code | 0 |
| Installer reboot exit code | 3010 |
| Device restart | Do not restart |
| Install timeout | 5 minutes |

**Install command** — one line, with this exact uploaded ZIP filename:

```text
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$ErrorActionPreference = 'Stop'; Expand-Archive -LiteralPath '.\MatrixRain-3.2.0-UEM.zip' -DestinationPath '.\payload-v320' -Force; & '.\payload-v320\MatrixRain\deployment\Install.ps1'"
```

If your console renames the uploaded archive, change only the ZIP filename to match the actual cache file. There is one enclosing `MatrixRain` folder; do not remove that component from the script path. Keep exit-code checking enabled. This installer returns 0 or 1 and never requests a reboot.

**Uninstall command:**

```text
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "& (Join-Path $env:ProgramW6432 'AfterSix\MatrixRainLab\Uninstall.ps1')"
```

Native 64-bit PowerShell is selected internally if UEM initially uses a 32-bit host. Installation is silent. The updated GUI starts from its Start menu shortcut or at next user sign-in, not in SYSTEM's desktop. Upgrade closes the old display; reopen it after installation to see the new heartbeat/rain behavior.

## Detection: all conditions must match

Use built-in **File exists** criteria with these literal paths. Do not paste quotes or Markdown backticks into the path fields. On a nonstandard Windows installation substitute its actual Program Files/ProgramData location.

| File path | Required file version |
|---|---|
| `C:\Program Files\AfterSix\MatrixRainLab\MatrixRainLab.exe` | Equals 3.2.0.0 |
| `C:\Program Files\AfterSix\MatrixRainLab\MatrixLabWorker.exe` | Equals 3.2.0.0 |
| `C:\Program Files\AfterSix\MatrixRainLab\MatrixOfficeActor.exe` | Equals 3.2.0.0 |
| `C:\ProgramData\AfterSix\MatrixRainLab\config.json` | Any |
| `C:\Program Files\AfterSix\MatrixRainLab\Start-Workload.ps1` | Any |
| `C:\Program Files\AfterSix\MatrixRainLab\Stop-Workload.ps1` | Any |
| `C:\Program Files\AfterSix\MatrixRainLab\Get-WorkloadStatus.ps1` | Any |

Do not use config.json alone: it is retained on uninstall. A version check ensures an older display does not satisfy this assignment. Leave unnecessary modified-date/size checks unset.

The installer also writes a 64-bit registry marker `HKLM\SOFTWARE\AfterSix\MatrixRainLab`, `DisplayVersion` REG_SZ = `3.2.0`, and an InstallPath value. `deployment\Detect.ps1` (also copied beside the installed EXE) verifies registry version, all three binary versions, config and control-script presence. It prints `3.2.0` and exits 0 if all match, otherwise exits 1. For the configured UEM custom-script workflow, use the standalone Detect-MatrixRain-3.2.0.ps1 and command in the Custom detection section below. The built-in rules above remain an equivalent file-only alternative.

## Configuration and startup

Ordinary upgrades preserve `%ProgramData%\AfterSix\MatrixRainLab\config.json` and results. Existing NodeLabel and KeepAwake values remain valid but no longer control identity or wakefulness: the display uses the actual hostname and always requests an awake display. No fleet.csv, computer-name edits, or per-node ZIP is needed. `-ReplaceConfig` restores the generic packaged config. Legacy installer `-NodeId` is accepted but ignored. `-NoAutoStart` removes sign-in startup but keeps the shortcut.

Reopen **Matrix Rain** from Start after upgrade, or sign out/in. The main screen shows a randomly selected next test. Click **Go (admin)** and approve UAC, then watch baseline phase 1/2 automatically advance to workload phase 2/2 under the same approval. Defaults are 60 + 600 seconds; Settings adjusts both. Stop during baseline cancels the whole experiment. **Stop (admin)** ends a run early. Settings are optional. UAC cancellation starts no command. No stress workload starts automatically at installation or sign-in.

Export logs saves diagnostics locally and does not upload them to UEM. Normal user closure during an experiment prompts for a choice; the installer still stops the worker before closing the old display.

The package's `uem` scripts are optional SYSTEM-context Freestyle actions; they are not application install/detection commands. Legacy Run-Node script names are only fixed test aliases, not hostname assignments. Prefer explicit mode commands for new automation. Details: [WORKLOADS.md](../WORKLOADS.md).

## Logs and troubleshooting

Installation transcripts (last five): `C:\ProgramData\AfterSix\MatrixRainLab\deployment-logs`. These begin inside the native 64-bit installer. Extraction/path failures occur before that and still require UEM's command output/cache listing.

Workload logs/results: `C:\ProgramData\AfterSix\MatrixRainLab\workloads`. They are machine-wide and retained on uninstall. The runner follows StateFile in the installed display config, so a custom state path must be writable by SYSTEM and readable by the desktop user.

Upgrade/uninstall first stops the existing workload and waits for the installed worker to exit. If it is blocked and cannot exit, deployment aborts. It does not indiscriminately kill other processes. Installer file staging/rollback is not an MSI transaction; a failure after startup/config edits may require another installation.

If notifications alternate success/failure, inspect the log sequence: exit 0 followed by final detection failure can trigger UEM's uninstall rollback. Correct the failing detection rule rather than changing success codes or repeatedly reinstalling. Files can disappear because of that rollback. Keep version rules exact and preserve the actual ZIP/script paths above.

Pilot on one device: confirm UEM Installed; run `tests\Test-Windows.ps1 -Installed` and `tests\Test-WorkerWindows.ps1 -Installed`; reopen display; run/stop a short workload; confirm DEX sees the two separate processes. Then expand to six.

[Omnissa Windows application deployment reference](https://techzone.omnissa.com/resource/deploying-workspace-one-uem-applications-windows-devices).

## Elevation pilot

After confirming Installed on one Latitude, sign in as the normal lab user and open the display. Cancel Go at the UAC prompt: no new run should appear. Click Go again and supply administrator approval: RUNNING should appear while the display stays unelevated. Stop should request approval and produce IDLE. Validate a standard user's direct worker launch cannot bypass elevation with tests/Test-ElevationWindows.ps1, run from a non-elevated PowerShell window. Native UAC rendering/credentials could not be exercised on the macOS development host.

## Reporting regression check

Open a retained CPU result: no Pi value should appear. Export with administrator power diagnostics, approve UAC, and verify power/requests.txt contains the command result rather than a privilege-denied message. REPORT.txt should identify the Windows build (for the pilot devices, 10.0.26200 plus the installed revision) and display awake-request acceptance. Repeating export while a test runs must leave its run ID and worker PID unchanged. Cancel UAC once and confirm a limited standard-user ZIP is still saved. The exporter does not upload any bundle.

## Custom detection for 3.2.0

Upload `Detect-MatrixRain-3.2.0.ps1` from the release root as the UEM detection script. Use:

```powershell
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ".\Detect-MatrixRain-3.2.0.ps1"
```

Success exit code: **0**. Not detected/check failed: **1**. This standalone PowerShell 5+ script consolidates the seven file criteria above. It checks all three EXE versions as 3.2.0.0. Replace the previous version's script when uploading this release. Installer success stays 0, reboot code stays 3010, and restart remains disabled; this release requests no reboot.

The catalog PNG does not provision endpoint shortcuts. The installer now assigns the embedded EXE icon to the Start menu shortcut, and the display explicitly uses it for its window/taskbar icon. Upgrade closes the old display; reopen it from Start to see the new icon and **v3.2.0** header. There is no need to uninstall or remove retained results/config first.

AD standard-user and Entra ID pilot instructions and diagnostic additions are in the README's 3.2.0 section. Windows elevation prompt latency still requires a device-side reproduction; this release removes the UI-thread wait and hardens alternate-account monitoring and shared-data permissions.


## Microsoft 365 Testing pilot

Reopen the display normally after installation and confirm v3.2.0. Choose Microsoft 365 Testing, set Settings duration to 600 seconds, and approve Go. The display minimizes; real apps start after baseline. Restore the display to stop/view progress; it restores automatically at completion/failure. Review OFFICE-WORKDAY.md before the six-hour pilot. No Office apps are launched in SYSTEM/session 0 during installation.
