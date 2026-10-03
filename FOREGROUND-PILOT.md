# Matrix Rain 2.6.0.0 — foreground pilot

Before the focus pilot, confirm browser discovery completes as described in BROWSER-STARTUP.md. Browsers are now verified before Office/Outlook launch.

## What changed

The 2.0.0.0 AS146274 export confirmed 20 foreground checks for Outlook and none for Excel, PowerPoint or Edge. It performed real document work for roughly 51 minutes, then failed while handling an Excel COM object. Successful document editing did not establish successful foreground switching.

2.6.0 restores the target, briefly attaches the calling thread to foreground/target input threads, requests activation and detaches in finally blocks. If ordinary requests fail, one Alt press/release may be sent before a second bounded activation sequence. An exact target window or its root owner must be foreground and restored to count as confirmed. The app records before/target/after HWNDs, the final foreground PID and whether fallback input was accepted. A matching process ID alone is no longer sufficient.

The fallback requires the interactive unlocked desktop, at least 15 seconds of human inactivity and no held keys/mouse buttons. A new input event cancels subsequent activation. Only the exact injected timestamp is discounted from idle accounting. This is ordinary synthetic keyboard input and may show menu hints; it does not type text, click or change Windows focus policies. Windows or security software can still reject it. No permanent topmost state is used.

Each Office application receives one rotation slot even if several presentations are open. Both browser windows are retained when Chrome and Edge are selected. Normal document-edit actions may also activate their application.

The release also avoids dynamic binding when releasing closed Office objects and keeps the coordinator connected until the final cleanup result is available.

## Verify on the affected Windows device

1. Deploy with both UEM version fields set to **2.6.0.0**, using the new detection script. Launch the updated Matrix Rain display.
2. Run Office Workday with a 60-second baseline and 600-second workload. Allow Outlook's preparation grace to finish, then leave input idle.
3. Observe several rotations. Excel, PowerPoint, Outlook and each selected browser should become the actual foreground app. Check DEX foreground duration for each, rather than relying only on visible background edits.
4. Move the mouse or type while watching. Automation should pause, then resume after 15 seconds idle. It must not keep taking focus while you work.
5. Stop or allow the pilot to finish. Check the final result, saved files and cleanup errors. Export logs even if it appears successful; actions.log should confirm foreground changes for all selected apps.
6. After a successful short pilot, run at least 90 minutes to cover workbook turnover and the first planned Office restart. A ten-minute focus pilot does not validate the previous 51-minute failure.

Local tests use injected delegates and IPC fixtures on macOS. They verify fallback bounds, cancellation guards, synthetic-input accounting, rotation selection and cleanup-result delivery; they cannot prove native Windows focus behavior.

Microsoft references: [foreground restrictions](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow), [Alt and foreground unlocking](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-locksetforegroundwindow), [input-thread attachment](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-attachthreadinput).
