# Hardware Launch Studio / Matrix Rain 3.2.0

Select **Microsoft 365 Testing**, choose a duration, and click Go (admin). Hardware Launch Studio is the new content experience for this mode. The six-hour default, optional baseline, signed-in-user helper, existing focus safeguards and controlled cleanup are retained. It does not enter Random Rotation. Windows Workday is unchanged.

## The creative brief

Each run creates a reproducible fictional NORTH / CONCEPT STUDIO collection with three products: **Arc 14** ultraportable, **Core Studio** workstation and **Canvas 32** display. The recorded run seed selects Silver Tide, Midnight Graphite or Soft Mineral, and varies component costs. Names, specifications, materials, prices, BOM costs and decision text come from one shared model. Documents are explicitly marked fictional; prices, performance specifications and material goals are synthetic assumptions.

Artwork is generated locally from the same vector scene model, rasterized at 1600×1000 for Office. No AI service, external image download, font installation or additional endpoint dependency is needed. Product illustrations are designed concept drawings, not photographs or engineering renders. Office text uses Segoe UI.

## What develops on screen

| App | Actual document activity |
|---|---|
| PowerPoint | Begins with one partially constructed slide. Four editing turns progressively construct each slide's title, supporting text, artwork and detail, then move to the next slide. Story stages unlock up to five, seven, eight and nine slides; they do not generate those slides automatically. Completed slides remain intact; economics/decision slides are revised after the available deck is built. |
| Excel | Starts with headers and two component rows. Each editing turn enters up to two additional rows. A product's price/cost/margin/revenue summary appears only after its nine component rows are entered. The chart arrives after two product summaries exist. The scenario sheet appears in Adapt; its rows arrive gradually, with second-source results withheld until Reveal. Existing cells and charts are retained. |
| Word | Starts with two text additions, then appends up to two paragraph/page/image operations per editing turn. Text is entered in eight-character chunks with short pauses. Cover art, design principles, product sections and engineering review are added progressively. Earlier content is retained as a draft history; completed briefs receive appended review notes and updated economics. |
| New Outlook | Unsent synthetic review drafts with the same collection, products, current costs and sourcing decision; recipient fields remain blank. Existing compose-control guards are retained. |
| TXT / CSV | Concise studio notes and a component export drawn from the shared model. They replace the previous unrelated random filler. |

Word, Excel and PowerPoint are opened in maximized windows. Existing verified foreground rotation now includes Word. This is still real app editing, not slide-show playback or pre-rendered screenshots. Native Office controls/ribbons remain visible.

Initial working drafts are saved before Outlook's setup grace. They are deliberately incomplete. Planned edit actions and confirmed Office foreground turns both advance the selected file's build cursor. The owned document is brought forward before editing, and the active slide, worksheet or text range is shown during construction. Brief pauses make shape/cell changes visible; Word text is written in short chunks. When a file reaches the current stage's build limit, later turns review its existing content or revise the available economics. Each file has its own progress, including a second document created late in the run.

## Story progression

Four stages are selected from the available workday time, after subtracting Outlook preparation time:

1. **Discover:** establish the family and design language.
2. **Engineer:** add unit economics and explicit design tradeoffs.
3. **Adapt:** simulate an 18% increase in unified-memory component cost.
4. **Reveal:** qualify a second source at 6% above the original memory-cost assumption while retaining launch prices.

Stage changes unlock later sections; they do not bulk-fill documents or reset their progress. Each subsequent edit saves incremental work. The four story stages follow the run's time window, while actual document completion depends on editing opportunities. Reading breaks, locked desktops, user input, Outlook preparation or app startup can defer work. A short or interrupted run can end with partially built files. Deadline and Stop save the current drafts and close test-owned documents; neither fast-forwards unfinished content to a polished final version.

The PowerPoint economic labels, Word price summaries, Excel BOM inputs and Outlook draft figures all derive from the same model. Excel calculates its own cost rollups, margins and revenue. Gross margin excludes development, freight, channel costs and tax. The scenario is intended for believable local document activity, not real financial or hardware design guidance.

## Files and cleanup

Existing per-run Desktop/Documents folders remain `MatrixRainLab-Office\<phase-id>`. Generated documents use Product-Reveal, Launch-Economics and Design-Strategy filenames. There are at most two generated XLSX/PPTX/DOCX files per run and 12 TXT/CSV notes per type; later actions revise the existing documents. The shared brief is saved as `Hardware-Launch-Studio.json` in the run's Documents folder. Product PNG assets remain in the local helper folder and are embedded in the saved Office documents.

Stop/deadline saves and closes generated Word/Excel/PowerPoint documents; all generated files remain. Quit is used only for a process verified as newly created by the helper. Existing cleanup and 90-second watchdog behavior now includes Word. A blocked action may terminate a verified test-owned Word process; unsaved edits can be lost on that failure path. Personal documents are never selected as workload inputs.

Export logs now includes `studio-brief.json` alongside action log, schedule and summary. It excludes generated DOCX/XLSX/PPTX files and browser profiles. The display status includes the theme and current story stage.

## Deployment and first pilot

Use **3.2.0.0** in both UEM version fields and the matching detection script. See UEM-SETUP.md. Stop any active workload before upgrading. Desktop Word, Excel and PowerPoint must be installed, activated and configured for the signed-in user, together with New Outlook and Edge/Chrome. Retain the previously deployed package if rollback is needed.

Start with a **20-minute Microsoft 365 Testing run** on one device. Complete first-use account prompts. This checks incremental activity, not guaranteed completion of every document. Check that:

- Word opens and rotates alongside Excel, PowerPoint, Outlook and the browser.
- Documents show the same theme, product names, prices and current revision.
- Startup shows one incomplete slide, two component rows, and a short Word draft rather than finished files.
- Return to the same file after several editing turns: slide objects, BOM rows and Word paragraphs have been added; earlier content remains.
- Financial summaries and chart are withheld until their component rows are entered. The Adapt/Reveal stages unlock later work without completing it instantly.
- Stop during a Word edit saves/closes test documents and returns Matrix Rain; files reopen afterward.

Then run the normal six-hour session and inspect DEX and exported logs. Look for STUDIO BUILD entries containing filename, cursor and story stage. Stopping early must retain incomplete but reopenable documents. Compared with 3.0, document activity shifts from large filler tables to richer formatting, charts, illustrations and Word edits, so do not assume identical resource utilization.

Local tests execute production layout builders against recording fixtures and inspect preview galleries. They validate composition commands and shared-data contracts; they do not run Windows Office. Preview galleries use an Arial substitute for Segoe UI. Actual Office layout, licensing, focus behavior, COM startup/cleanup and UEM deployment still need this pilot.
