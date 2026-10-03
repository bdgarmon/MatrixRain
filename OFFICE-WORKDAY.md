# Office workday workload — version 2.6.0

This is real application and file activity in the signed-in user's Windows desktop. It is a synthetic workday, not a recording/replay of human keystrokes or a calibrated productivity benchmark.

## Start a workday

1. Install this UEM package. Sign in as the lab user and complete Word, Excel, PowerPoint, Microsoft Edge, and **New Outlook** first-run setup/activation. Chrome is optional; if it is unavailable, the run uses Edge. Use a dedicated test session; the workload brings windows forward and closes its own documents/windows.
2. Open Matrix Rain **normally**, not with Run as administrator. Choose **Office workday**. Selecting it sets the workload to **21,600 seconds / six hours**, preceded by the usual 60-second baseline.
3. For the first pilot, open Settings and set workload duration to **600 seconds**. The Office range is 60–28,800 seconds; baseline is additional. Intensity/CPU/memory/disk settings apply to the existing synthetic tests, not Office workday.
4. Click **Go (admin)**. Approve using your administrator account. The coordinating worker is elevated; the Office helper and all applications run as the original signed-in user. The display minimizes while the helper runs. Office apps start after the baseline.
5. Restore Matrix Rain from the taskbar to view counters or click **Stop (admin)**. A stop allows the current application action to finish and waits for cleanup. The display restores when the run finishes or fails. Stop/cleanup may take up to roughly 90 seconds if an application is slow; blocked actions fail visibly.

A workday is an explicit selection. It is **not** added to the five short tests in Random rotation. There is no automatic next workday, sign-in launch of stress, or restart after reboot. Start the Office workload from the local display; the existing SYSTEM/Freestyle start scripts remain for the six original modes because SYSTEM has no interactive user desktop.

## What actually happens

| Component | Actual activity |
|---|---|
| Excel | Opens desktop Excel, creates genuine `.xlsx` workbooks, writes 50–10,000 rows of synthetic project data, changes those rows, runs a SUM formula/recalculation, formats columns, and saves/reopens files. Keeps at most two generated workbooks open at a time. |
| PowerPoint | Opens desktop PowerPoint, creates genuine `.pptx` presentations with 3–12 slides, adds text and embedded generated charts, replaces slide content and saves/reopens presentations. Keeps at most two generated presentations open at a time. |
| Chrome / Edge | Randomly selects Edge, Chrome, or both from installed browsers. Opens **8–15 live website tabs per selected browser** in an isolated profile, kept open through breaks and Office restarts until cleanup. Each default eight-site list is shuffled before repeats are used to fill extra tabs. No periodic forced reload is performed; live page activity follows the site scripts and browser policies. |
| New Outlook | Opens the installed New Outlook app, leaves its window open during work/reading periods, and closes/reopens a test-owned window during application cycles. If Outlook was already open, it stays under user ownership. **No automated email reading, typing, sending, deleting or mailbox changes are implemented.** The app itself may perform its normal background synchronization. |
| TXT and CSV | Creates and rewrites synthetic notes and tabular files. Target sizes are approximately 4 KiB, 32 KiB, 128 KiB, 512 KiB, 2 MiB and 8 MiB. These writes use file APIs; they are not edits performed in a text editor. |

Excel/PowerPoint editing uses their real desktop automation interfaces, not fabricated files with Office extensions. It does not simulate every keystroke, mouse action, or user-input latency. The actual compressed Office file size depends on its contents; size targets above apply to TXT/CSV files. The action log records the actual byte size after each successful save.

After Outlook preparation, the initial document actions create notes, CSV, a workbook, a presentation; the browser opens live websites, then edits both Office documents. Later actions are selected randomly, usually **30–120 seconds apart**. Applications remain open during thinking/reading gaps. There are longer **10–20 minute breaks** and Excel/PowerPoint restart cycles at approximately 90, 180 and 270 minutes. The recorded random seed and schedule make each run's intended sequence inspectable. Timings are approximate because real application operations take time. Failed operations stop the workload rather than silently falling back to fake visualizations.

## Browser tabs, Outlook drafts, and prerequisites (2.6.0)

When a tagged browser window is successfully brought to the foreground, Matrix Rain sends one guarded `Ctrl+Tab` to display the next tab. It does not send keystrokes while the desktop is locked, a key or mouse button is held, or the user has interacted in the preceding 15 seconds. The result view and `actions.log` record browser-tab switches.

New Outlook does not provide the classic Outlook desktop COM interface. Matrix Rain therefore creates a draft only after it has brought its tagged New Outlook window forward and Windows UI Automation exposes both the compose subject and body controls. The generated draft has an empty recipient field, a synthetic subject, multi-paragraph body, and a bold generated header, and is closed without sending. It never uses `Ctrl+Enter`, sets a recipient, or automates account sign-in. If those compose controls are unavailable because of a UI update, first-run state, or a user interaction, that one draft action is skipped and logged; the workday continues.

Before opening any browser or Office application, Office Workday checks the desktop registrations for Word, Excel, and PowerPoint. If one or more are missing, it writes and opens a retained Desktop Notepad message: `This PC is not properly configured for this test. Install Microsoft 365 apps and retry.` Matrix Rain records the missing application names as a failed Office result and restores its matrix display behind the Notepad warning. Windows Notepad is a plain-text editor, so it cannot render that message in bold. No workload files, browsers, or Office applications are started in this condition.

## Files and folders

The helper resolves Windows' actual Desktop and Documents known folders for the **signed-in user**, including OneDrive Known Folder Move and supported redirected/network locations. It creates:

```text
<Desktop>\MatrixRainLab-Office\<run-id>\
<Documents>\MatrixRainLab-Office\<run-id>\
```

It alternates new generated files between the two locations. Existing personal documents are never selected as inputs. Each run uses unique subfolders; repeat runs do not overwrite earlier runs. Each run creates at most 12 files of each of TXT, CSV, XLSX and PPTX before switching to updates of existing generated files.

Files remain after completion for inspection and sync testing. Remove only the `MatrixRainLab-Office` run folders when you no longer need them. If Desktop/Documents sync through OneDrive or are redirected to a server, these are real writes and will participate in that environment's normal sync/redirection behavior. The helper does not change those policies.

Each action checks a 2 GiB free-space reserve for both output locations. A cumulative saved-file-byte counter stops further actions after reaching 8 GiB; the last whole-file save can cross that threshold. This counter is the sum of file sizes on saves, not physical disk writes or network transfer bytes. Office files remain relatively small; large size variation is mainly in TXT/CSV. Each selected browser's isolated profile is stored under the helper's LocalAppData run directory and is retained separately from the Desktop/Documents artifacts.

## User session, ownership and stopping

UAC still gates Go/Stop. A protected administrator-worker status record authorizes one run for the originating user's SID and session. A separate **MatrixOfficeActor.exe** with an asInvoker manifest is started by the normal display, not by the elevated worker. It checks that authorization and the status pipe's server PID before working. Do not launch the display itself elevated for this mode.

New Outlook and browser windows are marked for test ownership before closing them. Pre-existing Outlook windows are not closed. Excel/PowerPoint operations target generated documents; application Quit is used only for a fresh process created by the test. A process may remain in the background after its visible window closes; app-open/close counters are lifecycle/window actions, not a guarantee that every background service exited.

Keep this dedicated session signed in and unlocked. When the input desktop is unavailable or locked, new scheduled actions pause; the wall-clock deadline still advances, and missed actions are skipped rather than replayed in a burst. Paused time and skipped actions are recorded. A run completed with little activity must not be interpreted as six hours of productive work. The helper also requests display/system wakefulness while active; policy-enforced locking, sleep and sign-out remain Windows-controlled.

Each application action has a 90-second watchdog. On a blocked action, the helper reports FAILED, requests closure of its marked browser/email windows, and may terminate Excel/PowerPoint processes it created. Already saved artifacts remain; an unfinished in-memory edit may be lost. Shared/pre-existing app processes are not killed. Inspect the action log after failures. This is not a guarantee that a blocked application's own internal dialogs will always close cleanly.

## Evidence and DEX interpretation

Machine-wide results remain in:

```text
%ProgramData%\AfterSix\MatrixRainLab\workloads\run.json
%ProgramData%\AfterSix\MatrixRainLab\workloads\last-result.json
```

The `Office` object includes helper PID/user SID, current activity, created-file/save/action counts, sum of bytes saved, application lifecycle counts, resolved folders, paused seconds, skipped actions and errors. Baseline/workload phase timestamps retain the same run ID. Counters record completed operations, not merely scheduled ones. `Browsers` records the random selection and `BrowserTabsRequested` records the total requested tab count. Tab count is a launch request, not an independently inspected count of browser tabs; confirm 8–15 per browser during the Windows pilot.

Per-user evidence is in:

```text
%LocalAppData%\AfterSix\MatrixRainLab\office\<run-id>\actions.log
%LocalAppData%\AfterSix\MatrixRainLab\office\<run-id>\schedule.json
%LocalAppData%\AfterSix\MatrixRainLab\office\<run-id>\summary.json
```

Export logs includes these three files for the five newest local Office runs, subject to the existing export size limits. It excludes generated Office documents and browser profile contents. Export from the originating user session to include that user's logs. Launch failures before connection are recorded in `office-launch-error.txt`.

In DEX, correlate the run's hostname and UTC phase times with **EXCEL.EXE**, **POWERPNT.EXE**, **msedge.exe** and/or **chrome.exe**, New Outlook's **olk.exe**, **MatrixOfficeActor.exe**, and **MatrixLabWorker.exe**. Observe CPU/memory, app lifecycle, storage and any configured profile/sync telemetry. This app does not configure DEX collection, calculate a productivity score, or verify DEX ingestion.

## Validation limits and first pilot

The macOS build host can compile Windows executables, test schedule generation and the coordinator/helper protocol, and run the original small workload regressions. It cannot execute Microsoft 365 desktop applications or validate your Windows UAC, Office activation, New Outlook registration, redirected folders, window ownership or six-hour app behavior.

Run the ten-minute pilot first. Confirm actual windows open and each selected browser has 8–15 tabs; both run folders contain readable XLSX/PPTX files; existing generated files receive updates; counters and action logs agree; Stop closes test-owned windows; a stopped run preserves artifacts. Open a saved workbook/presentation manually afterward. Then run six hours on one AD and one Entra ID device and compare DEX timelines before expanding deployment. Do not treat the automated planner/IPC tests as a completed six-hour Windows Office test.

## Retained 1.5.2 PowerPoint correction

The AS146274 export showed baseline completing, Excel opening, then `MissingMemberException: Error while invoking HWND` in PowerPoint startup. The corrected helper never reads PowerPoint Application.HWND. It activates the generated presentation through its document window using the [documented Activate method](https://learn.microsoft.com/en-us/office/vba/api/powerpoint.documentwindow).

A generated presentation filename includes a run-specific marker. Its visible window can identify the PowerPoint process; only a process absent before COM startup is eligible for whole-application cleanup. When that ownership cannot be established, generated documents are closed but PowerPoint may remain open. Pre-existing PowerPoint processes are not terminated.

Matrix Rain minimizing at Go is intentional so real application windows are visible. Returning to Matrix Rain does not cancel the workload. It restores at completion or failure. The output folders are created during preflight, so their presence alone does not mean documents were saved.

## Foreground activity, Outlook setup and manual interaction (introduced in 1.5.3)

Minimized windows are restored before foreground requests. A rotation attempts Excel, one generated PowerPoint window, Outlook and each selected browser window every 30–60 seconds; normal document actions can also change focus. Requests are checked against the actual foreground window/root owner, rather than only the process ID. `ForegroundAttempts`, `ForegroundConfirmed`, `ForegroundDenied` and `ForegroundTarget` appear in the Office result and action log. They are point-in-time verification, not accumulated foreground durations; DEX remains the source for those durations. Windows may deny focus under its policies, on a locked desktop or while another application owns input. In 2.6.0 a denied normal activation can trigger one synthetic Alt key press/release followed by a bounded retry. This is permitted only on an unlocked desktop after 15 seconds of user inactivity with no keys or mouse buttons held. No text or clicks are injected. Only the exact timestamp of Matrix Rain’s own injected event is excluded from user-idle accounting; subsequent human activity still pauses the workload. The fallback may briefly expose application menu hints. See [Microsoft's SetForegroundWindow restrictions](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow).

Outlook is launched asynchronously and allowed **five minutes** to expose a window, followed by a **two-minute setup grace**. During preparation, new document actions and focus rotation wait; already-open apps remain open. Complete sign-in or consent yourself if prompted. Readiness of the account is not automatically verified. Outlook stays open across the periodic Office restart cycles. If a setup window is replaced, the helper looks for the replacement. Waiting for Outlook is cancellable and appears as PREPARING. The total workload deadline keeps advancing; expiration before preparation finishes is reported as a failure. Use **600 seconds** for the first pilot, especially on fresh devices.

Mouse or keyboard activity pauses new actions and focus rotation until **15 seconds idle**. It does not stop the run, extend its deadline or instantly interrupt an action already in progress. `InteractionPausedSeconds` records this separately from a locked desktop. Your activity still affects DEX observations. Opening menus, editing generated documents or closing apps can interfere with automation; use Stop before taking over the session for normal work. Missed later scheduled actions are skipped; initial document-creation actions are retained. Cleanup runs at stop/deadline even if you have recently used the mouse.

At normal completion or operator stop, generated open Excel workbooks and PowerPoint presentations are explicitly saved before closing. Test-owned browser and Outlook windows are closed; saved files and browser profiles remain. Pre-existing Outlook windows and Office processes whose ownership cannot be proven remain untouched. Cleanup attempts each application even if another reports an error. Blocked/crashed applications can still fail cleanup and lose an unfinished edit; those failures are recorded. Matrix Rain restores and requests activation when the helper finishes; Windows focus policy can still affect which window is foreground.

## Earlier status and cleanup corrections

Both 2026-09-19 exports failed reading the required `workloads/run.json` snapshot. The active Office reader now retries a missing file for up to one second, just as it retries transient sharing conflicts. It does not continue from cached authorization: persistent missing status, missing directories, access denial and stale worker status still fail. The idle UI's optional status lookup remains immediate.

Cleanup reports the original error plus named inner failures with exception codes; full exception chains remain in actions.log. Each generated document's save/close and the other apps' cleanup are attempted independently. If saving a document fails, normal cleanup leaves it open and avoids quitting that application so it can be recovered manually. A blocked application action can still reach the existing watchdog/emergency cleanup. Successful saves remain on disk. This is a fix to cleanup isolation and diagnostics, not proof of the unidentified cleanup COM failure's cause: 1.5.3 discarded the inner error in the latest AS146274 run.

Office Workday requires activated, editing-capable desktop Office for the signed-in user. COM registration and a successful app launch do **not** prove licensing. Before retrying on an unlicensed device, activate Office and manually create, edit and save one workbook and presentation as that user. The AS53702 export failed during Outlook preparation before any workbook or presentation save was attempted; its FileNotFoundException was for Matrix Rain's status file, not an Office license file. Matrix Rain does not activate Office or bypass licensing. Other non-Office workloads remain available.

The AS146274 run recorded 18 created files and 36 saves, including real XLSX/PPTX edits, before failing. Only 3 of 11 foreground requests were confirmed. That earlier pilot did not validate foreground switching. Version 2.6.0 adds the guarded fallback described above; a Windows pilot remains necessary.

## Live browser websites (introduced in 1.5.5)

Defaults are [Fox News](https://www.foxnews.com/), [CNN](https://www.cnn.com/), [Gizmodo](https://gizmodo.com/), [After Six Computers](https://aftersixcomputers.com/), [AP News](https://apnews.com/), [The Verge](https://www.theverge.com/), [Ars Technica](https://arstechnica.com/) and [BBC News](https://www.bbc.com/news). The intended Fox News/Gizmodo domains are used, not the malformed hyperlink targets in the original request.

Each browser opens all eight default sites in shuffled order before repeating sites to reach its requested 8–15 tabs. If Chrome and Edge are both selected, each gets its own plan and profile. Browser windows remain open through the workday; later browse actions retain the same tabs rather than continuously adding more. The browser startup log records the requested URLs, launcher PID/exit code, verified window-owner PID and requested tab count. Neither successful page loads nor live tab counts are independently verified. Website titles are no longer used to identify ownership: only a window in the current session whose executable and command-line test-profile argument match is tagged for cleanup. Launcher exit does not end discovery; a replacement process can own the window. If no verified window appears within the bounded discovery period, startup fails with diagnostics. Conflicting managed UserDataDir registry policy produces an explicit isolation error.

Live websites introduce actual network, rendering, JavaScript and potentially advertising/media activity. A frequently updated website does not guarantee an already-open tab refreshes itself. There is **no forced reload timer**. Site scripts, consent prompts, connectivity, managed browser policies, sleeping tabs and background throttling affect the resulting workload. The helper does not click consent/login prompts, scrape articles or change managed browser settings. Confirm the pages load normally on the pilot devices and use DEX for observed activity.

To customize the list, edit this file as an administrator before starting the next run:

```text
C:\ProgramData\AfterSix\MatrixRainLab\browser-sites.json
```

It is a JSON array of 1–64 absolute HTTPS URLs. Example:

```json
[
  "https://www.foxnews.com/",
  "https://www.cnn.com/",
  "https://gizmodo.com/",
  "https://aftersixcomputers.com/"
]
```

Short lists repeat to fill the tab count; longer lists select a shuffled subset. Credentials embedded in URLs, non-HTTPS schemes, whitespace and command-line quote characters are rejected. The packaged defaults are installed when the file is absent; ordinary upgrades preserve an existing list. `-ReplaceConfig` also resets this list. Missing files use built-in defaults; malformed existing files fail visibly before application launch. This list is separate from the strict display config.json. Exports include browser-sites.json and requested URLs in actions.log. Generated HTML research pages are no longer created; TXT/CSV/XLSX/PPTX activity is unchanged.

## Office startup retry (introduced in 2.0.0.0)

An Excel/PowerPoint COM activation failure with HRESULT 0x80080005 receives one retry after two seconds. Other errors fail immediately. Each attempt has a fresh 90-second watchdog window; the experiment's overall deadline is unchanged. The startup stage is shown in workload activity and logged. No document editing operation is retried by this mechanism. Startup diagnostics record the actor SID/session/architecture, COM registration in both registry views, and Office process IDs/session/start times/responding/window presence. Export logs includes this information in actions.log.

A normally working Office UI does not establish that its COM automation endpoint is working. Persistent errors require device investigation; this release does not repair Office, alter DCOM settings or terminate existing Office processes to force startup. See OFFICE-STARTUP-TROUBLESHOOTING.md.

## Closed-object handling and final cleanup report (introduced in 2.0.1)

Calls to release Office objects now use static object arguments, avoiding dynamic binding against an already-closed workbook or presentation. The helper reports CLEANING until all cleanup steps finish, then sends FAILED or COMPLETED with the final details. This prevents the coordinator from disconnecting upon the initial error and losing subsequent cleanup results. Persistent native Office failures can still prevent a final save; the app does not claim recovery of unsaved data.

## Browser startup sequencing (2.6.0)

Selected browsers are launched and verified first. Only then are Excel, PowerPoint and Outlook opened. This prevents the reported browser failure from leaving a newly launched Outlook window in front while no workload actions are running. WMI process inspection uses the Windows/.NET Framework components already present on the device; there is no new download dependency. Discovery continues after the launcher exits, requests a two-second WMI enumeration timeout and polls on a 500 ms cadence until 30 elapsed seconds. The existing 90-second watchdog remains the outer bound for a blocked native call. Ownership cannot be inferred from a website title or a generic browser process name.
