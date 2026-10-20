# Matrix Rain 3.2.0

Windows / Workspace ONE UEM version: **3.2.0.0**.

Microsoft 365 Testing now visibly builds the Hardware Launch Studio documents: a partial first slide, incremental component rows, and Word text entered in short chunks. Subsequent focused work intervals add content, while story stages unlock later sections. Stop saves the actual current draft without fast-forwarding completion.

This release also brings the version 3 features to GitHub: continuous Random Rotation with recovery/rest intervals, Windows Workday using OS apps, Microsoft 365 exclusion from rotation, adjustable rain speed/spacing, and native-pixel/per-monitor DPI rendering.

## Workspace ONE UEM

1. Upload **MatrixRain-3.2.0-UEM.zip** as the application package.
2. Follow **UEM-SETUP.md** in console order. Set App Version and Uploaded UEM Version to **3.2.0.0**.
3. Use **MatrixRain-3.2.0-UEM-install-command.txt** and **Detect-MatrixRain-3.2.0.ps1**; detection success exit code is 0. Installer success is 0, reboot code is 3010, and no restart is requested.
4. Upload **MatrixRain-Icon.png** for the catalog icon if needed.

GitHub's source ZIP/TAR downloads are source archives, not UEM installation packages. Stop active workloads before upgrading. .NET Framework 4.8+ is required; .NET 10 is a separate runtime and is not needed.

## Validation

The maintainer reported that 3.2.0.0 deployed and worked wonderfully on the Windows pilot. Locally: all three x64 executables compile with warnings as errors; versions verified; 26 PowerShell files parse; 1,163 studio checks plus Word handle regression, 102 Office/IPC checks and 46 controller checks pass. Verify behavior in your own Office/user environment. Public packaging preserves the tested executable bytes; only documentation is adapted for publication.
