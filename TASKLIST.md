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
- **Status**: Open
- **Description**: Git identity differs from the identity specified in
  `SESSION_BASELINE.md`; ask user before changing local Git configuration.
- **Work Done**:
  - Found configured identity `Vanderdi Gustav OA133`
    / `Gustav.Vanderdi@draexlmaier.com`.
  - Baseline specifies `Gustav Vanderdi`
    / `Vanderdi.Gustav@gmail.com`.
- **~time-spent so far**: 0 hours

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
  - Repository currently has no solution or project files.
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
  - Upload wireframe should be based on user's path breakdown and these
    samples; no plan or implementation approved yet.
  - User accepted the Upload page direction; detailed requirements and plan
    are still in progress. Planning only; no implementation approved.
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
- **~time-spent so far**: ~0.25 hours

---

## Project Statistics
- **Total Tasks**: 4 (1 closed, 3 open)
- **Total Time**: ~0 hours
