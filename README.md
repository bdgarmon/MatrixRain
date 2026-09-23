# Matrix Rain

A lightweight Windows Matrix-style display and workload runner for endpoint experience testing, including Omnissa DEX pilots.

**Release: 2.5.0 · Windows/UEM version: 2.5.0.0 · Windows x64 · .NET Framework 4.8+**

![Matrix Rain icon](MatrixRainLab-UEM.png)

## Download and deploy

Download **MatrixRain-2.5.0-UEM.zip** from [GitHub Releases](https://github.com/bdgarmon/MatrixRain/releases). This is the deployable package; GitHub's automatically generated source archives are not installers.

For Workspace ONE, follow [UEM-SETUP.md](UEM-SETUP.md) in console order: Details, Files, Deployment Options, then Save & Assign. Upload the matching detection script and use **2.5.0.0** in both UEM version fields. The guide and copyable install command are also separate release assets.

For a local evaluation, extract the UEM ZIP and run `MatrixRain/Start.cmd`. Installed deployments start the display from the Start menu or at user sign-in. Installation does not automatically start a workload. Starting or stopping a workload requires administrator approval; Office automation runs as the interactive signed-in user.

## Features

- Matrix rain with hostname, CPU/RAM/GPU metrics when available, uptime and workload progress.
- Automatic baseline followed by controlled visualization, CPU, memory, disk, science or Office Workday workloads.
- Office Workday creates, edits and saves real Excel/PowerPoint documents and TXT/CSV files under the user's Desktop and Documents. It opens New Outlook and 8–15 real website tabs per selected browser (Edge, Chrome or both).
- Foreground rotation, idle-reading intervals, periodic Office restarts, input-aware pauses and cleanup with generated files retained.
- Display/system wake requests, configurable labels and website list, and local troubleshooting exports.

Office Workday requires desktop Excel and PowerPoint, New Outlook and Edge and/or Chrome, configured for the signed-in user. Outlook account sign-in and email sending are not automated. DEX collection remains external to this app. Default browser sites are listed in [OFFICE-WORKDAY.md](OFFICE-WORKDAY.md); administrators can customize browser-sites.json.

## Documentation

- [Workspace ONE setup](UEM-SETUP.md) and [deployment reference](deployment/UEM-DEPLOYMENT.md)
- [User guide](USER-GUIDE.md) and [workloads](WORKLOADS.md)
- [Office Workday behavior](OFFICE-WORKDAY.md)
- [Browser startup](BROWSER-STARTUP.md) and [foreground pilot](FOREGROUND-PILOT.md)
- [Validation results](TEST-RESULTS.md) and [release notes](CHANGELOG.md)

## Build from source

On Windows x64 with .NET Framework 4.8+, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

This creates MatrixRainLab.exe, MatrixLabWorker.exe and MatrixOfficeActor.exe. The internal MatrixRainLab names and AfterSix installation paths are retained for upgrade compatibility. Source archives exclude compiled executables; use the release UEM ZIP for ready-to-run files. Portable regression projects under tests use the .NET 10 SDK; that SDK is not an endpoint runtime requirement.

No third-party monitoring service is bundled. Troubleshooting exports can contain machine/user identifiers and should be reviewed before sharing publicly.
