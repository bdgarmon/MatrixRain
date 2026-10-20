# Release notes

## 3.2.0 / Windows-UEM 3.2.0.0

- Replace eager document generation with per-file incremental builds. Startup saves a partial slide, two BOM rows and a two-addition Word draft.
- Confirmed Office foreground intervals and scheduled edits build toward the deliverables; bring the document forward before editing, pace visible changes, and retain existing content.
- Story stages unlock later slides/scenarios/sections without filling them automatically. Stop/deadline saves partial drafts rather than fast-forwarding completion.
- Progressive regression checks reject whole-document Word deletion and spreadsheet clearing, and verify deferred financial summaries/charts/scenario results.

## 3.1.1 / Windows-UEM 3.1.1.0

- Fix Microsoft 365 Testing failing during Word startup: resolve Hwnd from the owned document's ActiveWindow rather than Word.Application. Process ownership is established only after that document window exists; focus rotation uses owned document windows.
- Attempt every browser window during cleanup, retaining individual failures and allowing fifteen seconds per window instead of five. No forced process termination was added.
- Includes a strict Word document/window regression fixture. Real Office execution remains an endpoint pilot requirement.

## 3.1.0 / Windows-UEM 3.1.0.0

- Hardware Launch Studio replaces unrelated Microsoft 365 filler with a shared fictional product family, three visual directions and a four-stage launch story.
- Real Word documents join Excel, PowerPoint, Outlook and browser foreground rotation; Word startup, save/close and watchdog ownership are included.
- Editable PowerPoint typography/diagrams, generated product illustrations, native Excel formulas/charts and illustrated Word design briefs.
- Supplier-cost scenarios and revised launch assumptions propagate to the latest documents; story/theme appear in status and troubleshooting exports.
- UEM detection and deployment instructions updated to 3.2.0.0. Windows/UEM pilot still required; 3.0 remains the previous tested baseline.

## Retained from 3.0

- Continuous Random Rotation under one administrator approval, with randomized durations/intensity, mandatory screensaver rest and bounded phase history.
- Microsoft 365 excluded from rotation; renamed **Microsoft 365 Testing** in the UI.
- New **Windows Workday** using Notepad, File Explorer and Edge, with real journal edits/saves, synthetic task files, foreground rotation and browser tab changes. Included in random rotation.
- Per-user rain speed and column spacing sliders, retained across launches.
- Native display-pixel rendering with per-monitor DPI awareness, brighter antialiased rain and longer trails for UHD displays.
- Matching UEM detection script and screen-order setup guide at version 3.2.0.0.

This is a locally validated pilot package. Windows interactive behavior and Windows 11 26H2 still require endpoint validation; see VERSION-3.md.
