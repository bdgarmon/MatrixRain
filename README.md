# Matrix Rain

**Version 3.2.0 · Windows/UEM 3.2.0.0 · Windows x64 · .NET Framework 4.8+**

A Matrix-style fullscreen display and real workload runner for Windows endpoint experience testing.

## Download and deploy

Download **MatrixRain-3.2.0-UEM.zip** from [the latest release](https://github.com/bdgarmon/MatrixRain/releases/latest). Upload that ZIP to Workspace ONE UEM; GitHub's source archives are not installers.

Follow [UEM-SETUP.md](UEM-SETUP.md) in console order: Details, Files, Deployment Options. Use **3.2.0.0** in both UEM version fields, the matching detection script and supplied install command. The release includes separate copies of the guide, detector and install command.

For local evaluation, extract the deployable ZIP and launch `MatrixRain/Start.cmd`. Installation configures user-session startup; workloads require an explicit Go action and administrator approval.

## What's new

- Microsoft 365 Testing visibly constructs a fictional hardware launch across Word, Excel and PowerPoint, instead of opening finished deliverables. Native documents develop over successive editing/foreground intervals; partial drafts are saved on Stop.
- Windows Workday uses built-in Windows apps. Continuous Random Rotation excludes Microsoft 365 Testing, includes Windows Workday and screensaver-only rest, and varies activity until stopped.
- Rain speed/spacing controls, native display-pixel rendering and per-monitor DPI support.
- Retained baseline, workload status, power requests, troubleshooting exports, foreground safeguards and cleanup.

Microsoft 365 Testing needs activated desktop Word, Excel and PowerPoint, New Outlook and Edge and/or Chrome configured for the signed-in user. It uses real local Office documents and synthetic unsent email drafts. Generated files remain after cleanup. DEX collection is managed externally; Matrix Rain does not install or configure DEX.

## Documentation

- [UEM deployment](UEM-SETUP.md)
- [Full user guide](USER-GUIDE.md)
- [Hardware Launch Studio and progressive construction](HARDWARE-LAUNCH-STUDIO.md)
- [Microsoft 365 Testing behavior](OFFICE-WORKDAY.md)
- [Version 3 modes and display controls](VERSION-3.md)
- [Validation and pilot guidance](TEST-RESULTS.md)
- [Release history](CHANGELOG.md)

The maintainer reported successful 3.2.0.0 deployment and testing. Local checks also cover compilation, versioning, content builders and deployment packaging; they do not certify every Office version or endpoint configuration.

## Build

Run `Build.ps1` in Windows PowerShell on Windows with the .NET Framework 4.8 compiler/reference assemblies available. Source is included; prebuilt executables are in the deployable release ZIP.
