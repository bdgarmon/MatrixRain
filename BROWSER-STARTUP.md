# Matrix Rain 3.2.0 — browser startup correction

The four latest 2.0.1.0 device exports all failed before any foreground attempt. Excel and PowerPoint opened, but the browser launcher exited before exposing a window. The old code broke out immediately instead of waiting for another process to own the window. The exports do not establish whether this was a normal handoff, a browser startup error or an enforced browser policy.

## Changes

- Continue window discovery after launcher exit, up to a 30-second discovery deadline under normal query timing.
- Use visible browser windows in the signed-in session; read the owning process's executable and command line through WMI. Require the expected executable and exactly one matching --user-data-dir argument. Parse Windows arguments with CommandLineToArgvW, preserving paths with spaces. Reject missing, ambiguous and unrelated profile arguments.
- Log launcher PID, exit code, matched process PID, rejected candidates and process-inspection errors. Do not log unrelated browser command lines.
- Detect machine/user UserDataDir registry policies in both registry views before launch. A conflicting policy fails explicitly; Matrix Rain does not change policy or adopt a personal browser window. Registry checks cannot enumerate every possible cloud policy source.
- Verify browsers before launching Office and Outlook. A browser failure therefore stops before those new windows are opened.
- Retain real 8–15 website tabs per selected browser, foreground fallback, human-input pause and final cleanup reporting.

WMI and System.Management are built-in Windows/.NET Framework components. If WMI access is denied or returns no readable executable/command line, discovery fails with diagnostics instead of guessing ownership. There is no silent fallback to a reduced workload. A found window does not verify that each website loaded or every requested tab was created.

Microsoft documents that Edge's managed [UserDataDir policy](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/userdatadir) overrides the command-line directory. If this policy is active, the pilot needs an administrator-approved configuration that permits isolation; the app does not bypass it.

## Windows pilot

Deploy 3.2.0.0 using the matching detection script. Start a 60-second baseline plus 600-second Microsoft 365 Testing. Confirm browser startup succeeds, then Office and Outlook open. After Outlook preparation, watch several foreground rotations and compare DEX foreground durations. Export logs even after success. Look for BROWSER verified profile owner and confirmed FOREGROUND entries for each selected app.

If discovery fails, the export should now distinguish launcher exit from missing profile ownership, unreadable WMI data or a conflicting registry policy. Native Windows browser handoff and foreground behavior could not be exercised on the macOS build host. After the short pilot succeeds, run at least 90 minutes to exercise Office workbook turnover and the first scheduled restart.
