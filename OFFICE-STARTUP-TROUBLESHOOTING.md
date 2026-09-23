# Office startup — 2.5.0.0

## What the pilot device export showed

The 2026-09-21 run <run-id> completed its 60-second baseline. Excel COM activation then failed after about 30 seconds with HRESULT 0x80080005 (CO_E_SERVER_EXEC_FAILURE / Server execution failed), before any document action, file save or successful application open was recorded. The three installed components were 1.5.5.0. This export does not establish a licensing problem or identify why the Excel COM server failed to start.

## Changes

Version 2.5.0.0 retries this particular activation error once after two seconds for Excel and PowerPoint. It records each attempt's duration, full exception, registration in both registry views and Office process/session snapshots. Each attempt remains subject to a 90-second watchdog; the workload deadline still applies. Other COM errors are not retried. The new error includes the application name, HRESULT and next steps. User documents, Office settings and existing Office processes are not modified to repair startup.

## Pilot on the affected device

1. Install 2.5.0.0 using the accompanying UEM setup guide and new detection script. Confirm 2.5.0.0 on the Matrix Rain display.
2. As the same ordinary signed-in user who runs Matrix Rain, open Excel and PowerPoint. Complete any visible setup/activation prompts. Save personal work and close the applications normally. Do not use a different administrator identity to validate Office readiness.
3. Start Office Workday with a 60-second baseline and 600-second workload. Leave the device untouched during the pilot. A first-run Outlook preparation pause may occur.
4. Verify actual generated XLSX/PPTX files, increasing created/saved counters, application foreground changes, and successful cleanup with files retained. A visible application window alone is not a successful document workload.
5. Export logs after the run. If it fails, include the new export. The office actions.log now contains `Office startup context`, `Office registration`, process snapshots and `activation attempt`/`activation succeeded` records. Compare the actor session with Office process sessions. A process snapshot or registry entry alone does not diagnose the cause.

The build is validated locally on macOS; successful Excel COM activation and full Office cleanup require this Windows pilot. A bounded retry may recover a transient failure but cannot repair a persistent Office automation startup problem.
