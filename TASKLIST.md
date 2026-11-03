# Task List

Every piece of work gets a T-ID. Rules in SESSION_BASELINE.md.

---

## T001: Set Up Session Starter Files
- **Status**: Open
- **Description**: Copy the session starter files into the repo root and
  fill in the placeholders in SESSION_BASELINE.md (git identity, branch,
  repository, current scope) and README.md.
- **Work Done**:
  - (first session fills this in)
- **~time-spent so far**: 0 hours

---

## T002: Reading Session Starter Files
- **Status**: Closed
- **Description**: Read the session entry point and required baseline,
  task list, and README files; run session-start repository checks.
- **Work Done**:
  - Read `.claude.md`, `SESSION_BASELINE.md`, `TASKLIST.md`, and `README.md`.
  - Checked Git identity, branch, and remote.
- **time-spent**: ~0 hours

---

## T003: Resolving Git Identity Mismatch
- **Status**: Closed
- **Description**: Git identity differs from the identity specified in
  `SESSION_BASELINE.md`; ask user before changing local Git configuration.
- **Work Done**:
  - Found configured identity `Vanderdi Gustav OA133`
    / `Gustav.Vanderdi@draexlmaier.com`.
  - Baseline specifies `Gustav Vanderdi`
    / `Vanderdi.Gustav@gmail.com`.
  - 2026-10-29: User chose gmail identity for the T007 commit only, via
    per-command `-c user.name/user.email`.
  - User is sole developer, works from several computers. User approved
    repo-local gmail identity on this machine (global config untouched)
    and a per-machine check note in SESSION_BASELINE Git section.
  - Committed and pushed baseline and task list updates (user approved).
- **time-spent**: ~0.05 hours

---

## T004: Planning Folder Monitoring Application
- **Status**: Open
- **Description**: Define project goals and requirements for an application
  that maps folders, monitors for generated ZIP files, copies pictures to
  local or network destinations, and generates JSON reports.
- **Work Done**:
  - Captured folder layout by year/month/day and restartable copy tracking.
  - ZIPs contain PNGs and a JSON metadata file with mandatory and optional
    fields; output JSON describes pictures.
  - Captured separate network `logs` and `data` destinations, organized by
    date and order, plus background operation and taskbar settings.
  - User prefers overwrite on retry, completion tracking per order, and
    processing only after ZIP creation finishes.
  - Date folder source should be configurable; always-on service must run
    without a logged-in user, with a separate tray/settings interface.
  - Added per-station model update settings: poll network source on a
    configurable schedule (hourly default), compare ZIP name or creation
    time, and download newer models to local staging.
  - User activates model after current order finishes; validate ZIP
    readability/completeness and expected files first. Archive old model
    as ZIP with configurable retention.
  - Settings panel should show small operational metrics.
  - Repository had no solution or project files at planning start.
  - Need representative ZIP/schema, exact version comparison rule, folder
    mapping count, and desired metrics.
  - User wants a wireframe before plan approval: Upload, Download, and
    History pages, with settings on the page they affect.
  - Upload destination is built from configurable network/local root,
    auto-detected computer name (editable for testing), project name,
    fixed `data` or `logs` folder, date folders, order folder, and filename.
  - Network root may require user credentials; local root is needed for
    testing. Do not collect or store credentials in task notes.
  - User supplied `order_report.json` and target JSON screenshot. Sample
    order report has order-level timing/counts, `Results[]` with image paths,
    AI test status/data, expected positions, and report data.
  - Sample `Results[].ImagePath` values end in `.bmp`; target upload uses
    `.png`. Confirm how source images and report relate inside ZIP.
  - User confirmed each ZIP contains `order_report.json` and PNGs. Target
    report gets one picture entry for each PNG found.
  - ZIP images are located in a `raw` folder; copy every picture from that
    folder. Exact report-to-image matching still needs definition.
  - After copying all pictures from a ZIP, create one JSON report containing
    one `Data[]` entry per picture, following the provided target schema.
  - Target screenshot defines mandatory, required-if-available, and optional
    fields; unavailable non-mandatory fields should not be filled.
  - User confirmed field-by-field source mapping will be defined later.
    Target JSON shape and value types are the current design reference.
  - Still need define how PNGs match `Results[]` records and handle
    mandatory fields or failed/missing AI results.
  - User accepted the Upload page direction; detailed requirements remain
    in progress.
  - Upload page draft:
    - Show background monitor state, last scan, watched root, and discovered
      ZIP status. Watched layout: `<root>\<year>\<month>\<day>\*.zip`.
    - Configure local/network destination root, auto-detected but editable
      computer name, project name, and output path previews.
    - Use watched source-folder date for destination folders by default;
      keep date source configurable.
    - Put pictures in
      `<root>\<computer>\<project>\data\<year>\<month>\<day>\`
      `<OrderId>_<timestamp>\<original name>`. Preserve source filenames.
    - Put generated JSON in
      `<root>\<computer>\<project>\logs\<year>\<month>\<day>\`
      `<OrderId>_<timestamp>.json`. Put operational logs under `logs`;
      exact operational-log file layout is TBD.
    - Show one queue row per ZIP/order, with copied/total picture count only,
      processing state, report state, and retry action.
    - Process a ZIP after it is unlocked and ZIP readability is validated.
      Copy every picture from `raw`, then write one JSON per ZIP with one
      `Data[]` entry per picture.
    - Persist per-order progress across restart. Overwrite outputs on retry.
    - After initial failure, retry automatically three times, five minutes
      apart; then wait for manual retry.
  - User approved queue states: Waiting for ZIP unlock, Validating, Copying,
    Writing JSON, Retry scheduled, Complete, and Failed/manual retry.
    Field mapping and image/report matching remain deferred.
  - User supplied JSON path example:
    `\logs\2022\10\10\K05666151BK4701_20220901074055.json`.
  - User confirmed JSON filename:
    `<OrderId>_<order timestamp>.json`; timestamp format is
    `yyyyMMddHHmmss`. No separate order subfolder for this JSON path.
  - User supplied picture path example:
    `\data\2022\10\10\K05666151BK4701_20220901074055\image2.png`.
  - User confirmed picture path uses the same
    `<OrderId>_<order timestamp>` order folder and preserves source names.
  - Upload path previews include configured root, computer name, and project
    before the confirmed `data` or `logs` paths.
  - User chose order-level queue rows with copied/total picture counts only;
    no separate queue row for each picture.
  - User chose automatic retries for failed ZIP processing, limited before
    manual intervention: three retries after the first failure, five minutes
    apart, then manual retry.
  - User approved the first implementation plan: clean-architecture
    Domain, Application, Infrastructure, Worker, and WPF UI layers.
    Upload pipeline comes first; field mapping, Download, and History defer.
  - 2026-10-30: User pointed to real archives `D:\Data\Archives` (50 ZIPs)
    and test output root `D:\Data\uploadtest` (not yet created).
  - Survey of all 50 ZIPs (read-only): each has `order_report.json`,
    `raw/*.bmp` (no PNG), and `result/<image stem>_result.txt` per picture.
  - Report `OrderId` equals filename order ID in all 50. `Results[]` count
    equals `raw` count, and every `raw` filename equals the leaf of one
    `Results[].ImagePath` (folder part points to an old machine path).
    Matching by filename leaf works for all 50.
  - Every result has `Success=false`, an `AI` entry in `Tests[]`
    (recount 2026-11-02, T015), no `Error`; `PassedCount=0`. `ReportData`
    has `AssemblyBoardNr`, `AssemblyGroup`, `SteeringType`.
    `ExpectedPositions` is a string array.
  - BMP-only finding: see T010.
  - User decided (2026-10-30): accept any image type in `raw` (bmp, png,
    jpg) and copy as-is with original name. Match picture to `Results[]`
    by filename leaf, case-insensitive, ignoring the `ImagePath` folder.
    A picture without usable AI result still gets a `Data[]` entry; AI
    fields omitted.
  - Still open: field-by-field mapping needs the target schema screenshot
    (T009).
- **~time-spent so far**: ~0.45 hours

---

## T005: Building Upload Application Core
- **Status**: In Progress
- **Description**: Build a testable Upload path in clean-architecture layers.
- **Work Done**:
  - Added solution with separate Domain and Application projects.
  - Added Upload stages, progress model, package contracts, output path
    builder, and order-processing use case.
  - Added Infrastructure ZIP validation/copying and in-memory progress
    storage. Progress resumes while GUI stays open; app restart recovery is
    deferred.
  - Added console Worker that processes one ZIP with explicit order/date
    inputs and a WPF desktop GUI for ZIP, order/date/destination inputs,
    output path preview, copy progress, and cancellation.
  - Added VS Code GUI launch and desktop build-task configurations. README
    documents GUI copy-test setup.
  - Copy test deliberately does not generate JSON; exact target schema and
    field mapping are not available. Continuous folder monitoring, automatic
    retries, and Windows service remain.
  - Removed SQLite dependency and its transitive vulnerability after user
    requested a simpler first milestone.
  - Progress resets when destination, order, or ZIP picture list changes.
  - Earlier Worker smoke test used SQLite. After switching to in-memory
    progress, `dotnet build DaisiProvider.sln` succeeded in 1.51 seconds with
    zero warnings and zero errors. GUI runtime and user test remain pending.
  - User will test through GUI; command-line Worker is an internal test seam.
- **~time-spent so far**: ~1.1 hours

---

## T006: Reading Session Starter Files (2026-10-29)
- **Status**: Closed
- **Description**: Session start: read entry point, baseline, task list,
  and README; check Git identity and remote.
- **Work Done**:
  - Read `.claude.md`, `SESSION_BASELINE.md`, `TASKLIST.md`, `README.md`.
  - Git identity still mismatches baseline (see T003). Remote is
    `https://github.com/xGusu/DaisiProvider.git`, branch `master`.
  - README documents **Process folder** batch mode and archive filename
    parsing; T005 does not record that work yet.
- **time-spent**: ~0.05 hours

---

## T007: Committing and Pushing Upload Core (2026-10-29)
- **Status**: Closed
- **Description**: User asked to commit all pending work and push.
- **Work Done**:
  - Committed all staged and unstaged files on `master` as
    `Gustav Vanderdi <Vanderdi.Gustav@gmail.com>` (see T003).
  - Pushed `master` to `origin`.
- **time-spent**: ~0.05 hours

---

## T008: Reading Session Starter Files (2026-10-30)
- **Status**: Closed
- **Description**: Session start: read entry point, baseline, task list,
  and README; check Git identity.
- **Work Done**:
  - Read `.claude.md`, `SESSION_BASELINE.md`, `TASKLIST.md`, `README.md`.
  - Git identity matches baseline (repo-local gmail identity, see T003).
- **time-spent**: ~0.05 hours

---

## T009: Planning Samples Missing From Repo
- **Status**: Closed
- **Description**: T004 sample `order_report.json` and target JSON schema
  screenshot were never saved to the repo (Knowledge Capture rule). They
  are needed to close T004 matching and field-mapping questions.
- **Work Done**:
  - 2026-10-30: Searched repo; no sample report, schema, or `Attachments/`.
  - Fix: user re-supplies files; save under `Attachments/`.
  - 2026-11-02: Source structure saved as
    `Attachments/order_report_structure.md` (T015). User re-supplied
    target schema screenshot: `Attachments/target_json_schema.png`,
    written out as `Attachments/target_json_schema.md`.
- **time-spent**: ~0.05 hours

---

## T010: Upload Ignores BMP Pictures
- **Status**: Open
- **Description**: Real archives store pictures as `raw/*.bmp`, but
  `ZipUploadPackageReader` only accepts `raw/*.png`.
- **Work Done**:
  - Symptom (expected from code, not run): all 50 ZIPs in
    `D:\Data\Archives` would yield zero pictures.
  - Cause: extension filter `.png` in `ZipUploadPackageReader.cs` (line 89);
    T004 assumed PNG.
  - User decided: accept bmp/png/jpg under `raw`, copy as-is (T004).
  - 2026-11-02: Fixed in code by configurable copy rules (T012); default
    rule covers `raw/*.bmp`. Build green. Stays open until tests run and
    a real archive copies through the GUI.
- **~time-spent so far**: ~0.05 hours

---

## T011: Reading Session Starter Files (2026-11-02)
- **Status**: Closed
- **Description**: Session start: read entry point, baseline, task list,
  and README; check Git identity.
- **Work Done**:
  - Read `.claude.md`, `SESSION_BASELINE.md`, `TASKLIST.md`, `README.md`.
  - Git identity matches baseline (repo-local gmail identity, see T003).
  - `TASKLIST.md` has uncommitted changes from the 2026-10-30 session
    (T004 survey notes, T008-T010).
- **time-spent**: ~0.05 hours

---

## T012: Planning Configurable ZIP Copy Selection and Service UI
- **Status**: Open
- **Description**: User wants to focus on image copy first: configurable
  selection of what to copy from inside the ZIP (folder/files), and the
  service must have a UI.
- **Work Done**:
  - 2026-11-02: Current reader hard-codes `raw/` + `.png`
    (`ZipUploadPackageReader.IsPictureInRawFolder`); covers T010.
  - Clarifying questions asked (selection rule, config home, service/UI
    split).
  - User decided (2026-11-02):
    - Selection = rule list: each rule has enabled flag, ZIP folder
      (e.g. `raw`, `result`) and file patterns (e.g. `*.bmp;*.png`).
    - Destination flattened: all selected files into
      `<OrderId>_<timestamp>\` with original names; duplicate leaf name
      is an error.
    - Final shape: Windows service does the work; separate WPF tray app
      edits shared settings and shows live status (named pipe).
    - First milestone: copy rules + saved settings + Desktop UI uses them
      (fixes T010). Service in a later step.
  - Plan approved with unit-test project and build (user, 2026-11-02).
  - Implemented:
    - Application: `CopyRule`, `CopySelection` (matcher, defaults
      `raw` `*.bmp;*.png;*.jpg` on, `result` `*.txt` off),
      `UploadAppSettings`, `IUploadSettingsStore` port.
      `UploadPackage.PictureNames` renamed `FileNames`.
    - Infrastructure: `ZipUploadPackageReader` takes `CopySelection`;
      errors for no enabled rule, no match, duplicate leaf name.
      `JsonUploadSettingsStore` at `%ProgramData%\DaisiProvider\`
      `settings.json`, atomic write via `.tmp`.
    - Desktop: copy-rules grid (add/remove/save); settings load at start;
      UI text says files instead of pictures.
    - Worker: reads settings file, falls back to default rules.
    - `tests/DaisiProvider.Tests` (xUnit 2.9.3): matcher, reader, store.
  - Build `dotnet build DaisiProvider.sln`: succeeded, 0 warnings,
    0 errors, 7.96 s. Tests not run yet (need user go-ahead).
  - Progress model still uses picture names (`PictureCount`,
    `CopiedPictureCount`); left as is to keep the change small.
- **~time-spent so far**: ~0.5 hours

---

## T013: Planning Simpler Copy Rules and Tabbed UI
- **Status**: Open
- **Description**: User wants simple rules (user defines ZIP folder and
  file types to copy) and a tabbed UI: copy rules on their own tab, room
  for additional JSON rules on another tab.
- **Work Done**:
  - 2026-11-02: Rules are user-defined already (not generated from ZIP);
    defaults only fill an empty settings file. Plan and questions sent.
  - User decided (2026-11-02):
    - Rule = ZIP folder + file types as plain extensions (`bmp, png`);
      `*` means all files in that folder. No wildcard patterns.
    - Tabs: Upload | Copy rules | JSON rules.
    - JSON rules = where output JSON report data comes from (field
      mapping). Tab is a placeholder until target schema exists (T009).
  - Implemented:
    - `CopyRule.Patterns` replaced by `FileTypes`. `CopySelection` matches
      extension; accepts `bmp`, `.bmp`, `*.bmp`; `*`/`*.*` = all files.
      `ParsePatterns` replaced by `ParseFileTypes` (dedupes, lowercases).
      Missing `FileTypes` in settings file treated as empty.
    - Desktop window split into tabs Upload | Copy rules | JSON rules.
      Save settings on Upload (destination box) and Copy rules tabs.
      JSON preview warning moved to JSON rules tab.
    - Tests updated to file types; new cases for `*`, prefixes, null.
  - Not built or tested yet (needs user go-ahead).
- **~time-spent so far**: ~0.3 hours

---

## T014: Planning Multiple Destinations and Network Credentials
- **Status**: Open
- **Description**: User wants source archive folder `D:\Data\Archives`
  and destination `D:\Data\uploadtest` for now, network share
  destinations with user and password, and copying to several
  destinations.
- **Work Done**:
  - 2026-11-02: Archive folder is not saved in settings today; will add.
  - Questions sent: credential storage, connection type, multi-target
    failure handling. Credentials never go into task notes.
  - User decided (2026-11-02):
    - Password stored encrypted with DPAPI LocalMachine scope in
      settings file (readable by app and later service on this PC only).
    - Connect by UNC path with user/password before copy; no drive
      letter.
    - Several destinations, each tracked separately; copy continues to
      the others when one fails; retry only the failed one.
    - Computer and project folder configurable per destination.
  - Plan proposed; waiting for approval. Paused 2026-11-02: user moved
    to JSON creation (T015).
- **~time-spent so far**: ~0.15 hours

---

## T015: Planning Order JSON Report Creation
- **Status**: Open
- **Description**: Work out how to build the output order JSON report:
  which data it needs and where each value comes from.
- **Work Done**:
  - 2026-11-02: Read one sample `order_report.json` and one
    `result/*_result.txt`; surveyed all 50 ZIPs in `D:\Data\Archives`
    (read only). Structure saved to
    `Attachments/order_report_structure.md`.
  - `result/*.txt` repeats the `Results[]` entry as text; no extra data.
    `order_report.json` is the only source inside the ZIP.
  - Finding: picture filename prefix `<position>#` matches an
    `ExpectedPositions` entry for 1275/1275 pictures, so each picture
    gets a position code. 1596 positions vs 1275 pictures.
  - Finding: T004 note "empty `Tests[]`" was wrong; all 1275 results
    have an `AI` test. T004 text replaced.
  - Finding: `ImagePath` separators vary (`\` or `/`); first count split
    only on `\` and wrongly showed 26/1275 matches.
  - User supplied target schema screenshot (T009 closed). Read keys,
    groups (mandatory / required if available / optional) and types into
    `Attachments/target_json_schema.md` with proposed source per field.
  - Mandatory fields with unclear source: `TimeStampUTC`, `Barcode`,
    `Identifier`, `ImagePath` base, `TestTime`, `Project`,
    `PredictionType`. `Prediction.Threshold`/`Blobsize` have no source.
    Ten open questions listed in the schema doc; sent to user.
  - README got an "Order JSON report (requirements)" section.
  - User supplied DAISI log file documentation (pasted text), saved as
    `Attachments/daisi_log_file_documentation.md`. Schema doc rewritten:
    - Answered: `TimeStampUTC` = `StartTime` (test start, UTC);
      `Identifier`/`Barcode` = `OrderId`; `Project` = setting matching
      DAISI Models path; `PredictionType` = `Skipped` while AI is skipped,
      `Prediction` then left out; file name
      `<Identifier>_<TimeStampUTC digits>.json`.
    - Conflicts screenshot vs doc listed (`Machine`/`Plant` mandatory,
      `ExecutionTime` type, unit spelling, `LogVersion`).
    - Remaining open: `ImagePath` base, per-picture `TestTime`, skipped
      detection, position code, naming (T016), date folder source.
  - Doc contains an internal server/share name; user confirmed it may
    stay (repo is private, 2026-11-02).
  - User decided (2026-11-02): `TestTime` = `StartTime` + summed earlier
    `AnalysisDuration`; naming from `StartTime` UTC (T016); date folders
    from `StartTime` UTC date. `ImagePath` form: user asks DAISI team;
    a setting until then. Recorded in schema doc "Decisions".
  - Still open: skipped detection rule, position code, conflicts 1-4.
  - User requirement (2026-11-02): JSON is the server's trigger to move
    images and log to the cloud, so it is written only after all images
    are copied. Derived: write via temp name + rename; per destination;
    no JSON on copy failure. Added to schema doc "Write order".
  - User pasted path-segment table (computer folder per PC, project
    folder agreed with bucket, data file names free). Added to DAISI doc.
  - Finding: source picture names contain `#`; risky in URLs for the
    labelling page/bucket. Also `.bmp` vs spec `.png` examples. Open
    question added to schema doc.
  - User approved build order (2026-11-02): step 1 JSON writer + T016,
    step 2 destinations (T014); build + tests after each step. Defaults
    for open questions: `ImagePath` = `<computer>/<project>/data/...`
    (setting); keep `#`/`.bmp` names; existing JSON -> skip order unless
    overwrite; skipped = message prefix; position code dropped; conflict
    proposals 1-4 applied.
  - Step 1 implemented (2026-11-02):
    - Application: `OrderReport` model, `DaisiLogSettings` (Plant and
      Project mandatory, prediction type, `ImagePath` style),
      `DaisiLogBuilder` (JSON per schema doc), `IUploadReportStore` port.
      `ProcessUploadOrder` reads order ID/start from `order_report.json`,
      skips when log exists (new stage `AlreadyDelivered`), copies, then
      writes JSON last (stage `WritingJson` -> `Complete`; stage
      `PicturesCopied` removed). Fails before copying if settings
      incomplete.
    - `UploadRequest` no longer takes order or date; `UploadProgress`
      date nullable.
    - Infrastructure: `OrderReportParser`, `FileUploadReportStore`
      (`.json.tmp` then rename). Settings store writes enums as text.
    - Desktop: order ID/timestamp/date inputs removed; JSON rules tab with
      Machine, DAISI project, prediction type, ImagePath form; previews
      show `<OrderId>_<StartTime UTC>`; folder run reports completed /
      already delivered / failed.
    - Worker args now `process <zip> <root> <computer> <project>
      [--retry]`; launch.json updated.
    - `UploadArchiveMetadataParser` no longer used; left in place (delete
      needs user OK).
    - Tests: log builder, processing order (JSON after copy, none on
      failure, skip/overwrite, settings check), report store, report
      parsing.
  - Build: succeeded, 0 warnings, 0 errors, 6.29 s.
  - Test run 1: 52/53 passed. `OrderReportWithoutStartTime_Throws` failed
    with "Entries cannot be created while previously created entries are
    still open": defect in the test (ZIP entry writer not closed), fixed.
  - Test run 2: 53/53 passed, 180 ms.
  - Finding T017: computed `TestTime` not unique in real data.
  - Not yet run against real archives or in the GUI.
- **~time-spent so far**: ~0.8 hours

---

## T016: Order Folder Name Uses Wrong Timestamp
- **Status**: Closed
- **Description**: DAISI spec names log file and image folder
  `<Identifier>_<TimeStampUTC digits>` (test start, UTC). Code takes the
  timestamp from the ZIP filename.
- **Work Done**:
  - 2026-11-02: Sample ZIP `..._20261021_160208.zip`: `StartTime`
    `14:01:43Z`, `EndTime` `14:02:08Z`. ZIP timestamp = local end time
    (UTC+2), not UTC start. Folder would be `..._20261021160208`, spec
    wants `..._20261021140143`.
  - Cause: `UploadArchiveMetadataParser` reads order timestamp from ZIP
    name (T004 decision predates the spec).
  - Fix proposal: read `StartTime` from `order_report.json`.
  - User decided (2026-11-02): use `StartTime` UTC for name and date
    folders.
  - Fixed in T015 step 1: order identity and date folders now come from
    `order_report.json` `StartTime` (UTC). Unit test
    `Json_IsWrittenOnlyAfterAllFilesAreCopied` checks
    `K1_20261021140143` and `2026\10\21` for start 14:01:43.6Z.
- **time-spent**: ~0.1 hours

---

## T017: Computed TestTime Not Unique
- **Status**: Open
- **Description**: DAISI spec: `TestTime` "should be different for all AI
  predictions". Chosen rule (start + earlier analysis durations) gives
  equal values in second resolution.
- **Work Done**:
  - 2026-11-02: Checked all 50 archives in `D:\Data\Archives`: 50/50
    orders get duplicate `TestTime`s; max summed analysis time per order
    7.1 s.
  - Cause: analysis durations are ms; format has whole seconds.
  - Options listed in `Attachments/target_json_schema.md` ("Known gap").
    Needs user decision.
  - 2026-11-02 (T021): `TestTime` now from picture capture time. Still
    50/50 orders have at least two pictures in the same second. Open.
- **~time-spent so far**: ~0.05 hours

---

## T018: Order Name Back to ZIP Timestamp
- **Status**: Closed
- **Description**: User reports Process folder writes no JSON and wants
  JSON/order folder named with the ZIP order time "like before". Reverses
  the naming part of T016.
- **Work Done**:
  - 2026-11-02 evidence: Desktop build 11:19:59, newer than source
    (11:17:57), so new code ran. `D:\Data\uploadtest` empty. Settings file
    has `DaisiLog.Project` = "" (mandatory), so processing refused to
    start; message only in the Upload status line (finding T019).
  - User decided: order folder and JSON name use the ZIP filename time
    `order_<id>_raw+result_<yyyyMMdd>_<HHmmss>.zip` (local end time).
    Kept: `TimeStampUTC` in the JSON = StartTime UTC; date folders =
    StartTime UTC date. ZIP order ID must equal report `OrderId`.
  - Implemented: `UploadArchiveMetadataParser.ParseOrder` (name only);
    `ProcessUploadOrder` uses it for paths and checks order ID against
    report; `UploadPackage.Order` removed; preview text updated.
  - Tests: name `K1_20261022013000` with date folder `2026\10\21` for
    UTC start 21st 23:30; order ID mismatch and bad ZIP name fail before
    copying.
  - Build succeeded (0 warnings, 0 errors); tests 55/55 passed, 196 ms.
  - README and schema doc updated (deviation from DAISI naming noted).
- **time-spent**: ~0.25 hours

---

## T019: Missing Mandatory Settings Message Easy to Miss
- **Status**: Open
- **Description**: When Plant or DAISI project is empty, Copy/Process
  folder do nothing except a status-line text on the Upload tab. User
  read it as "not triggering".
- **Work Done**:
  - 2026-11-02: Proposed fix: message box and switch to JSON rules tab.
    Not started; needs user OK.
- **~time-spent so far**: ~0 hours

---

## T020: Planning Model Folder as Prediction Source
- **Status**: Open
- **Description**: User proposes a settable models root (e.g.
  `D:\Ai\Models\BMW_XNF_AssemblyBoard`) with one folder per model; read
  `meta.json` there to fill more DAISI log fields.
- **Work Done**:
  - 2026-11-02: Folder exists on this PC. Read one `meta.json` and
    surveyed all 20 (read only). Structure saved to
    `Attachments/model_meta_structure.md`.
  - Finding: folder names = position numbers, same as picture prefix.
    Variants (`_B`, `_Fritz`) and `backup\` exist.
  - Usable: `TrainID` -> `ModelID`, `PredictionType`, Threshold,
    Blobsize. `AnomalyFound` not in meta (needs real run result).
    ImageCrop only a size, not X1/Y1/X2/Y2.
  - Questions sent: model folder choice, ModelID, Project source,
    skipped tests vs model data.
  - User decided (2026-11-02): models root is a setting; picture
    `<position>#...` uses folder `<root>\<position>` exactly (suffix
    folders and `backup` ignored); `ModelID` = `Meta.TrainID`; `Project`
    stays the setting (user asks DAISI team about meta Project); a
    skipped picture with a model folder gets `ModelID` but stays
    `Skipped` without `Prediction`. When a test really ran:
    `PredictionType` from meta, `Prediction` with `AnomalyFound` = test
    not passed, `Threshold`, `Blobsize`.
  - Implemented: `ModelInfo`, `IModelCatalog` port,
    `DaisiLogSettings.ModelsRoot`, `DaisiLogBuilder` model handling and
    `PicturePosition`; `ProcessUploadOrder` loads models per distinct
    position before copying; `FolderModelCatalog` (exact folder, numeric
    position only, missing root throws, cache by meta.json write time);
    JSON rules tab field + Browse; Worker wired.
  - Tests: catalog (values, variant/backup ignored, missing root, no
    TrainID, path guard, reload), builder (skipped with model, executed
    with model, key order, position parse), processing (lookup once per
    position before copy, catalog failure stops copy, no root = no
    lookup). 71/71 passed, 337 ms.
  - Build: Application, Infrastructure, Worker, tests built. Desktop
    output could not be replaced: files locked by running app ".NET Host
    (41420)" (MSB3026 retries). User must close app and rebuild (F5).
- **~time-spent so far**: ~0.6 hours

---

## T021: Log JSON Without order_report.json
- **Status**: Closed
- **Description**: User asks whether the log JSON can be built without
  the `order_report.json` inside the ZIP.
- **Work Done**:
  - 2026-11-02: All 1275 picture names in 50 archives match
    `<position>#<n>#<yyyy-MM-dd>_<HH>h<mm>m<ss>s<fff>ms.<ext>`, so each
    has a capture time. In 50/50 orders at least two pictures share the
    same second (still not unique, see T017).
  - Sources without report: order ID and order time from ZIP name;
    capture times from picture names; Machine/Project from settings;
    ModelID/Threshold/Blobsize from models folder. Lost: Steering,
    StructuralGroup (optional), test result (skipped or ran, passed),
    ExecutionTime, AnomalyFound.
  - Questions sent: replace or fallback, PredictionType without result,
    TimeStampUTC source.
  - User decided (2026-11-02): fallback (use report when present, else
    build without it); picture without test result gets PredictionType
    from model `meta.json`, else setting, no `Prediction`; without report
    `TimeStampUTC` = earliest picture capture time (local -> UTC);
    `TestTime` = picture capture time (local -> UTC) always.
  - Implemented: `UploadPackage.Report` nullable; reader allows missing
    report (broken report still fails); `DaisiLogBuilder.Build` takes the
    ZIP order, `OrderStartUtc` (report / earliest capture / ZIP time),
    `PictureCaptureTime`, local -> UTC with DST-gap handling; new
    PredictionType rule (reported skipped / ran / unknown);
    `ProcessUploadOrder` checks order ID only with report, date folder
    from `OrderStartUtc`.
  - Tests: missing and broken report, capture-time TestTime, no-report
    order level and types, ZIP-time fallback, name parsing, DST gap,
    processing without report. 81/81 passed, 324 ms.
  - Build: all projects compile; Desktop copy step failed (MSB3027,
    files locked by running app ".NET Host (29692)"). User must close the
    app before F5.
- **time-spent**: ~0.5 hours

---

## T022: Reading Session Starter Files (2026-11-03)
- **Status**: Closed
- **Description**: Session start: read entry point, baseline, task list,
  and README; check Git identity.
- **Work Done**:
  - Read `.claude.md`, `SESSION_BASELINE.md`, `TASKLIST.md`, `README.md`.
  - Git identity matches baseline (repo-local gmail identity, see T003).
  - Uncommitted work from 2026-11-02 sessions (T012-T021): modified
    sources, new Application/Infrastructure files, `Attachments/`,
    `tests/`. Last commit `7880b91`.
- **time-spent**: ~0.05 hours

---

## T023: Planning Local or Network Destination
- **Status**: Open
- **Description**: Destination can be a local PC path or a network share;
  app connects to the share itself (user/password). JSON `ImagePath` must
  show the share path, e.g. `//srvlme04/presqual-ingoing/data/...`.
  Smaller step of T014 (one destination only).
- **Work Done**:
  - 2026-11-03: Today `ImagePath` comes from setting "ImagePath form"
    (`DaisiLogBuilder.ImageBasePath`), default relative from computer
    folder, e.g. `N026995/Project/data/...`.
  - User decided (2026-11-03):
    - Network: share root holds `data` and `logs` directly; no computer or
      project folders added. `ImagePath` =
      `//<server>/<share>/data/<yyyy>/<MM>/<dd>/<order>/<file>`.
    - Drive letter only if copying needs it. It does not: Windows can
      authenticate to a UNC path without a letter (`WNetAddConnection2`,
      no local name). So no drive letter.
    - Local: `ImagePath` = full local path, forward slashes.
    - Connection stays open after copy (no disconnect).
  - Plan approved (user, 2026-11-03), incl. removing "ImagePath form".
  - Implemented:
    - Application: `UploadDestinationKind` (Local/Network),
      `NetworkShareSettings` (UNC path, user, password; `ToString` hides
      password; `ShareRoot`, `NormalizedPath`). `UploadPathBuilder.
      OutputRoot`: network = share path, local = root\computer\project.
      `DaisiImagePathStyle` removed; `ImagePath` = full picture path with
      `/`. Port `INetworkShareConnector`; `ProcessUploadOrder` connects
      before the already-delivered check.
    - Infrastructure: `WindowsNetworkShareConnector`
      (`WNetAddConnection2`, no local name, 30 s timeout, skips when path
      already reachable, clear message for error 1219).
      `MachinePasswordProtector` (DPAPI LocalMachine, `dpapi:` prefix,
      package System.Security.Cryptography.ProtectedData 10.0.0).
      Settings store encrypts on save, decrypts on load; old files
      without `Kind` load as Local; leftover `ImagePathStyle` ignored.
    - Desktop: Local/Network switch, network path, user, password box,
      Test connection; ImagePath preview; ImagePath form dropdown gone.
    - Worker: passes connector; destination from args stays local.
    - Tests: network paths and connect order, connect failure stops copy,
      local does not connect, ImagePath local/UNC, password encrypted in
      file, old file = Local, foreign encrypted password fails, ToString
      hides password, UNC parsing.
  - Build: succeeded, 0 warnings, 0 errors, 4.33 s. Tests: 91/91 passed,
    345 ms.
  - Not tested against a real share or in the GUI (needs user).
- **~time-spent so far**: ~0.5 hours

---

## T024: Computer Folder on Share, Debugger Stop, Destination Check
- **Status**: Open
- **Description**: User wants the computer folder between share and
  `data`/`logs`; reports a "crash" when the share connection fails; wants
  destination folder validation.
- **Work Done**:
  - 2026-11-03 user decisions: network path
    `\\<server>\<share>\<computer>\data|logs\...` (no project folder);
    `ImagePath` follows. Validation = destination root exists and is
    writable. Crash = VS Code debugger stops, on Copy / Process folder.
  - Evidence: no .NET Runtime / Application Error event for the app in
    the last 6 h, so the process did not die; consistent with a debugger
    break.
  - Cause (from code): share login threw inside `Task.Run`. With Just My
    Code the debugger breaks on an exception that leaves user code into
    the Task machinery ("user-unhandled"), although Copy/Process catch it
    after `await`. Not confirmed by a stack trace.
  - Fix: `INetworkShareConnector` replaced by `IUploadDestinationAccess.
    PrepareAsync` (`WindowsUploadDestinationAccess`): background part
    returns the problem, throw happens after `await`. Connects share,
    then checks root exists (never created) and writes/deletes a
    `.daisiprovider-write-test-*.tmp` file. Runs before the
    already-delivered check for local and network.
  - `UploadPathBuilder.DestinationRoot` added; `OutputRoot` network =
    share\computer. Computer folder field moved out of the local panel
    (used by both kinds).
  - Also: Save settings and Test connection (async void) now catch all
    exceptions; an escaping one would end the app.
  - Tests: network paths with computer folder, prepare first, prepare
    failure stops before delivered check, local prepared; access: missing
    root (not created), writable root leaves no file, root is a file.
    Run 1: 91/94, three old tests expected no events and now saw the
    prepare event (test defect, assertions made exact). Run 2: 94/94,
    175 ms. Build 0 warnings, 0 errors.
  - Open: user retest in GUI with real share (debugger stop gone?).
- **~time-spent so far**: ~0.4 hours

---

## T025: Configurable Destination Path Template
- **Status**: In Progress
- **Description**: User: project folder must not be in the path, only the
  computer. For the future, a path builder where the layout is chosen.
- **Work Done**:
  - 2026-11-03 evidence: settings `Kind=Network`, but run 13:43 copied
    to `D:\Data\DaisiUpload\N026995\Project\data`: the Local layout
    (root\computer\project), which still added the project folder.
  - User decided: template text plus token buttons and live preview,
    one template for files folder and one for log JSON; one layout for
    Local and Network (`{root}` = local root or share path); default
    `{root}\{computer}\data|logs\...` (no project).
  - Implemented:
    - `UploadDestinationSettings.FilesTemplate` / `LogTemplate` (null =
      default). `UploadPathBuilder` renders tokens `{root}`,
      `{computer}`, `{project}`, `{yyyy}`, `{MM}`, `{dd}`, `{OrderId}`,
      `{OrderTime}`; checks start `{root}`, `{OrderId}` present, log ends
      `.json`, unknown token, each rendered segment a valid name (no
      `..`, empty or separator from values). `{project}` needed only when
      used. `OutputRoot` removed.
    - Desktop: Path layout group (two templates, token buttons insert at
      cursor of last focused box, Reset to default); computer and project
      fields shared above Local/Network; preview uses an example order.
    - Tests: `UploadPathBuilderTests` (default local/network, custom with
      project and `/`, empty project allowed, 8 invalid templates,
      computer with separators).
  - Build: libraries and tests compile. Desktop copy step failed
    (MSB3027): files locked by running app ".NET Host (31348)" and the
    debug adapter. No compile error reported. Tests 107/107, 271 ms.
  - Open: user closes app, F5, checks layout in GUI.
- **~time-spent so far**: ~0.4 hours

---

## T026: Tokens in DAISI Project
- **Status**: Open
- **Description**: User wants tokens for position and computer name in
  the DAISI project setting (JSON rules tab).
- **Work Done**:
  - 2026-11-03: `Project` is written per `Data[]` entry, so per-picture
    values work. Tokens `{position}` (picture name prefix before `#`) and
    `{computer}` (destination computer name).
  - Implemented: `DaisiLogSettings.ProjectTokens`; unknown token listed in
    `MissingMandatoryValues`. `DaisiLogBuilder.Project` fills tokens,
    throws when position or computer is missing; `CheckProjects` runs in
    `ProcessUploadOrder` before copying. JSON rules tab: token buttons and
    example preview (picture `101#...`).
  - Tests: tokens per picture, picture without position, empty computer,
    no tokens unchanged, unknown tokens, processing fails before copy.
    113/113 passed, 344 ms. Desktop compiled to a temp folder: 0 warnings,
    0 errors (normal output locked by the running app, MSB3027).
  - Open: user check in GUI.
- **~time-spent so far**: ~0.25 hours

---

## T027: Computer Name in One Place Only
- **Status**: Closed
- **Description**: Computer name existed twice: Computer on the Upload tab
  and Computer ID on the JSON rules tab. User wants one place.
- **Work Done**:
  - 2026-11-03: Saved `DaisiLog.ComputerId` was empty, so no value lost.
  - Kept Computer on the Upload tab (paths, `{computer}` tokens and
    `Machine.ComputerID`). Removed `DaisiLogSettings.ComputerId` and the
    JSON rules field (note text in its place). Old value in a settings
    file is ignored.
  - Tests 113/113 passed, 305 ms. Desktop compiled to a temp folder:
    0 warnings, 0 errors.
- **time-spent**: ~0.1 hours

---

## T028: Project Token in DAISI Project
- **Status**: Closed
- **Description**: User wants `{project}` (Upload tab project name) as a
  DAISI project token, next to `{position}` and `{computer}` (T026).
- **Work Done**:
  - 2026-11-03: `ProjectTokens` gets `{project}`. `DaisiLogBuilder.
    Project` / `CheckProjects` take the destination settings; empty
    computer or project name with its token used fails before copying.
    JSON rules tab: third token button; preview follows the Project
    field. Moved `PicturePosition` comment back above its method (T026
    had placed `Project` between them).
  - Tests: `{project}/{computer}/{position}` per picture, empty computer
    and empty project. 114/114 passed, 255 ms. Desktop compiled to a temp
    folder: 0 warnings, 0 errors.
- **time-spent**: ~0.1 hours

---

## T029: Share Connection Once per Run
- **Status**: Closed
- **Description**: User asks whether the share is connected once or for
  every order; optimize if possible.
- **Work Done**:
  - 2026-11-03 finding: login (`WNetAddConnection2`) already ran once per
    app session (cached per root). But exists check and write test file
    ran for every ZIP (folder run of 50 ZIPs = 50 network round trips and
    50 test files).
  - Fix: `WindowsUploadDestinationAccess` remembers a successful check
    for 5 minutes (`TimeProvider`, ready time injectable for tests).
    `Forget(UploadDestinationSettings)` clears login and check; Desktop
    calls it on Test connection and after a failed order.
  - Tests: no recheck within ready time, recheck after expiry, Forget
    rechecks at once, failed check not cached. 117/117 passed.
- **time-spent**: ~0.15 hours

---

## T030: Version and Release 1.0.0
- **Status**: Closed
- **Description**: User wants a software version and release 1.0, then
  commit and push all.
- **Work Done**:
  - 2026-11-03: `Directory.Build.props` with `Version` 1.0.0 for all
    projects; window title `DaisiProvider 1.0.0`; Worker prints version.
    README section "Version and releases".
  - Debug build: Desktop output locked by running app ".NET Host
    (22292)" (MSB3027). Release build from scratch (`-c Release
    --no-incremental`): 0 warnings, 0 errors; tests 117/117, 396 ms;
    Desktop file version `1.0.0+7880b91...` (hash = last commit at build
    time).
  - Not tested in the GUI before release (user asked to release).
  - User said "push all": committed all pending work (T012-T030) as
    `4b37941` (gmail identity), annotated tag `v1.0.0`, pushed `master`
    (7880b91..4b37941) and the tag to origin.
- **time-spent**: ~0.15 hours

---

## T031: Running the App on Another PC
- **Status**: Open
- **Description**: User asks whether copying and running the app is
  enough to use it on another PC.
- **Work Done**:
  - 2026-11-03: Answered: needs .NET 10 Desktop Runtime, or a
    self-contained publish; settings file can be copied but the network
    password must be entered again (DPAPI LocalMachine). Offered a
    publish.
  - User: make both options available. Added publish profiles
    `SelfContained.pubxml` / `FrameworkDependent.pubxml` (win-x64,
    Release, output `publish\DaisiProvider-<version>-selfcontained|runtime`),
    VS Code tasks `publish-desktop-selfcontained` / `-runtime`,
    `publish/` in `.gitignore`.
  - Run 1: framework-dependent OK (0.4 MB, 11 files). Self-contained
    failed NU1301: runtime packs not cached, source "Nexus 3"
    (company feed) "No such host is known".
  - Run 2 with `-p:RestoreIgnoreFailedSources=true` (packs from
    nuget.org): OK, 139.8 MB, 406 files. Switch added to both tasks.
  - User approved commit and push (2026-11-03); same commit also holds
    the T030 push note.
  - Not yet started on another PC (user).
- **~time-spent so far**: ~0.2 hours

---

## Project Statistics
- **Total Tasks**: 31 (15 closed, 16 open)
- **Total Time**: ~7.8 hours
