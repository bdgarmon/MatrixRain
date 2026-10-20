# Matrix Rain 3.2.0 validation

Locally executed on macOS:

- All three Windows binaries compile as C# 5, x64, against .NET Framework 4.8 reference assemblies with warnings as errors. Assembly/file versions: 3.2.0.0.
- 26 PowerShell scripts parse. This is not Windows PowerShell 5 execution.
- 1,163 studio model/composition checks pass, plus the strict Word Window.Hwnd fixture. Tests execute production builders against recording Office fixtures; no Office COM server runs on this host.
- 102 Office planner/IPC and 46 controller/configuration checks pass.
- Packaging checks ZIP integrity, every packaged file against its source, required deployment paths, and the matching detection copy.

Progressive checks cover one partial initial slide; subsequent shape additions retaining existing shape objects; slide-count limits by story stage; independent new-file progress late in the story; initial two-row BOM with no financial summaries/chart; incremental rows preserving existing cells; deferred scenario and second-source results; Word's two initial text additions and later append-only paragraphs. Fixtures deliberately reject clearing all spreadsheet cells or deleting the whole Word document. Final composition checks still cover all three themes, layout bounds, model costs, formulas, chart/image retention and story decisions.

Local logs: work/validation-v320 in the development workspace. Fixtures skip deliberate pacing delays. These results do not establish foreground timing, real Word pagination, Office COM compatibility, device DEX observations or UEM upgrade success.

Windows pilot: follow HARDWARE-LAUNCH-STUDIO.md. Watch startup drafts, revisit each file through several foreground intervals, confirm additions appear and earlier work remains, then stop before completion. Reopen retained files to confirm that the actual incomplete draft was saved. Run the full workday afterward to check later story sections, chart/scenario construction and cleanup. A short run need not finish all deliverables, and Stop/deadline must never fast-forward to completion.
