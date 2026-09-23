# Matrix Rain 2.5.0

Windows executable and Workspace ONE version: **2.5.0.0**. Release tag: **v2.5.0**.

## Changes

Browser discovery now survives Edge/Chrome launcher exit and verifies windows using the expected executable, signed-in session and exact test-profile argument. Browser verification precedes Office/Outlook launch. Discovery logs explain launcher exits, rejected candidates and policy conflicts. Foreground fallback, application rotation and final cleanup reporting are included.

## Windows pilot results

Two devices completed a 60-second baseline and full six-hour Office Workday on the unchanged 2.5.0.0 executables.

| Result | Pilot A | Pilot B |
|---|---:|---:|
| Completed actions | 247 | 248 |
| Files created | 37 | 37 |
| Saves, including cleanup | 213 | 232 |
| Confirmed foreground checks | 624/624 | 639/639 |
| Scheduled Office restart cycles | 3 | 3 |
| Final cleanup | Successful | Successful |

Excel, PowerPoint, Outlook and Edge all received confirmed foreground focus; files were retained. Both pilots selected Edge. These results do not independently validate Chrome, website load success, live tab counts or DEX-measured foreground durations. Local regression validation: **199 checks passed**. One transient WMI discovery timeout recovered without failing the run.

## Deployment

Download **MatrixRain-2.5.0-UEM.zip**, **MatrixRain-2.5.0-UEM-SETUP.md**, **MatrixRain-2.5.0-UEM-install-command.txt** and **Detect-MatrixRain-2.5.0.ps1**. Keep the ZIP filename unchanged. Set both UEM version fields to **2.5.0.0**. Installer/detection success: **0**; reboot code: **3010**; restart: **Do not restart**.

Requirements: Windows x64 with .NET Framework 4.8/4.8.1. Office Workday additionally requires configured desktop Excel/PowerPoint, New Outlook and a supported browser. Upgrades preserve configuration and run history. No workload starts automatically at installation.

The executable bytes are unchanged from the validated pilot build. This release packaging updates public documentation and includes anonymized validation results, so its ZIP checksum differs from the earlier pilot package. SHA256SUMS.txt covers the attached assets. MatrixRain-2.5.0-Source.zip contains the repository source without prebuilt executables; use the UEM ZIP for deployment.
