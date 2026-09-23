# Matrix Rain 2.5.0 validation

**199 targeted checks passed:** 56 core/parsing, 37 controller/export, 15 deployment detection and 91 Office/startup/focus/profile/IPC checks.

Nine new checks exercise exact profile matching, separate argument/path spaces/casing, rejection of URL text and similar prefixes, ambiguous/missing arguments, delayed window discovery independent of launcher lifetime, immediate success and bounded discovery timeout. Existing focus fallback, input pause, startup retry and final cleanup delivery tests remain passing.

All three Windows executables compile as C# 5/x64 against .NET Framework 4.8 with warnings as errors. Assembly/file version is **2.5.0.0**. Native manifests/icons and deployment detection are checked. Package validation verifies every archived file, ZIP integrity, SHA-256 hashes, MatrixRain/ root and the matching standalone detection script.

The tests run on macOS with injected browser-discovery delegates and IPC fixtures. They do not execute Windows WMI, CommandLineToArgvW, native browser handoff, registry policy inspection, Office automation or foreground APIs. Those integrations compile but still need a Windows pilot. The evidence supports correcting the premature launcher-exit failure; it does not prove the devices' browsers will start successfully or that Windows will grant every foreground request.

Use BROWSER-STARTUP.md and FOREGROUND-PILOT.md for a short startup/focus pilot, followed by at least 90 minutes to check Office turnover and cleanup. Page loads, requested tab counts and DEX foreground durations require device observation.

## Completed Windows validation

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

