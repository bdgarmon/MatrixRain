# Matrix Rain 3.2.0 — minimum UEM setup

Use this checklist after uploading **MatrixRain-3.2.0-UEM.zip** as a Windows internal application or an upgrade. It follows the application editor tabs in order: **Details → Files → Deployment Options**. Fields below are listed top to bottom. Copy command text only, without Markdown backticks.

## 1. Details

| Field | Enter / select |
|---|---|
| Name | **Matrix Rain** |
| Managed By | Select the deployment organization group. |
| Application ID | Retain the application identity for upgrades. |
| App Version | **3.2.0.0** |
| Build Version | Leave the generated value. |
| Uploaded UEM Version | Set the four boxes to **3 · 2 · 0 · 0**. Correct any automatically generated different version. |
| Assignments Copied From | Leave the existing previous-version value. |
| Latest Version | Leave the displayed previous-version value. |
| Retire Previous Versions | For an upgrade, keep checked to replace the prior version. |
| Supported Processor Architecture | **64-bit only**; deselect 32-bit, ARM64 and ARM. |
| Is Beta | **No** |
| Change Log / Categories | Optional; leave blank. |
| Minimum OS | Select the minimum OS for your environment. Windows 11 x64 is the tested platform; .NET Framework 4.8+ is required. |
| Supported Models | **Desktop** |
| Default Scheme | Leave unchanged. |
| Description / Keywords / URL / Support Email / Support Phone | Optional; leave blank. |

The devices need **.NET Framework 4.8 or 4.8.1**; Windows 11 already includes a compatible version. **.NET 10 is a separate runtime and is not required by Matrix Rain.** Microsoft 365 Testing needs activated desktop Word, Excel and PowerPoint, New Outlook and at least one of Edge/Chrome, configured for the signed-in user. Version 3.1 performs real Word automation as well as Excel/PowerPoint.

## 2. Files

| Field / section | Enter / select |
|---|---|
| Application File | Confirm **MatrixRain-3.2.0-UEM.zip**. |
| App Dependencies | No bundled dependency packages; deploy prerequisites separately. |
| App Transforms | Leave empty. |
| App Patches | Leave empty. |
| App Uninstall Process → Use Custom Script for MSI | **Yes**, for this ZIP-based application. |
| Custom Script Type | **Input** |
| Uninstall Command | Paste the single line below. |

```text
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "& (Join-Path $env:ProgramW6432 'AfterSix\MatrixRainLab\Uninstall.ps1')"
```

The installed `MatrixRainLab` path is intentional: it preserves compatibility with existing installations.

## 3. Deployment Options

### When To Install

| Field | Enter / select |
|---|---|
| Data Contingencies | None. |
| Disk Space Required | **0 KB** |
| Device Power Required | **0** |
| RAM Required | **0 MB** |

These zero values leave the existing pilot's deployment gates unrestricted; they are not workload resource requirements.

### How To Install

| Field | Enter / select |
|---|---|
| Install Context | **Device** |
| Install Command | Paste the install line below. |
| Launch Type | **Launch Path** |
| Launch Command | Leave blank. Open Matrix Rain from the Windows Start menu after installation. |
| Admin Privileges | **Yes** — for installation. |
| Device Restart | **Do not restart** |
| Uninstall Device Restart | **Do not restart** |
| Retry Count | **3** |
| Retry Interval | **5** minutes |
| Install Timeout | **5** minutes |
| Installer Reboot Exit Code | **3010** |
| Installer Success Exit Code | **0** |

**Install Command:**

```text
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$ErrorActionPreference = 'Stop'; Expand-Archive -LiteralPath '.\MatrixRain-3.2.0-UEM.zip' -DestinationPath '.\payload-v320' -Force; & '.\payload-v320\MatrixRain\deployment\Install.ps1'"
```

The last character is a double quote. Do not append backticks. The same line is in **MatrixRain-3.2.0-UEM-install-command.txt** beside this guide. This installer does not request a reboot; do not use **0** for the reboot exit code.

### When To Call Install Complete

| Field | Enter / select |
|---|---|
| Use Additional Criteria | **Yes** |
| Identify Application By | **Using Custom Script** |
| Command to run the script | Paste the detection line below. |
| Script file | Upload **Detect-MatrixRain-3.2.0.ps1** from the release assets. |
| Success exit code | **0** |

```text
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ".\Detect-MatrixRain-3.2.0.ps1"
```

Use this custom script in place of the individual file criteria. It checks all three executable versions and the required configuration/control files.

## 4. Save & Assign

Keep the existing icon; no changes are required on Images, Terms of Use or Repository for this upgrade. Click **Save & Assign**, verify the intended pilot assignment and automatic delivery, then complete the console's save/publish flow.

After UEM reports Installed, open **Matrix Rain** normally from Start and confirm **v3.2.0**. Microsoft 365 Testing starts only when you select it and approve **Go**; installation does not start a workload. For the first test, set its duration to **600 seconds**.

---

[Full deployment reference](MatrixRain-3.2.0/deployment/UEM-DEPLOYMENT.md) · [Microsoft 365 Testing behavior](MatrixRain-3.2.0/OFFICE-WORKDAY.md)

Field order is based on your supplied screenshots. Installation privilege and timing units were cross-checked against [Omnissa's application configuration documentation](https://docs.omnissa.com/SoftwareDistributionManagement-VSaaS/AddWindowsapplicationsfromtheEnterpriseApplicationRepository).

Microsoft 365 Testing now opens live HTTPS websites. The installer supplies browser-sites.json automatically; no additional UEM upload is required. Existing custom site lists are preserved.

After deployment, follow **MatrixRain-3.2.0/FOREGROUND-PILOT.md** for the short focus pilot and longer workbook/cleanup verification.

Version 3.2.0 verifies browser startup before Office/Outlook launch. Use BROWSER-STARTUP.md in the source folder for the device pilot and diagnostic fields.
