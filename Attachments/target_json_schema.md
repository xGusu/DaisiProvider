# Target: Order JSON Report

Sources:

- `target_json_schema.png`: user screenshot (2026-11-02). Colour legend:
  blue = mandatory key, green = required if available, purple = optional
  key; red = string, orange = integer or float, black = boolean.
- `daisi_log_file_documentation.md`: official field descriptions
  (2026-11-02). Where the two disagree, see "Conflicts" below.
- Source data inside each ZIP: `order_report_structure.md`.

## Purpose

The log JSON goes with the images to the DAISI share. It feeds a Power BI
dashboard and the labelling web page; labelled images become training
data. Images without a log JSON are not shown for labelling.

## Rules

- **Mandatory**: always written. If no value can be found, no report is
  written and the order fails.
- **Required if available**: written when a value exists, key left out
  otherwise.
- **Optional**: written only when a value exists; key left out otherwise.
- Mandatory keys inside `Prediction` depend on `PredictionType` and apply
  only when `Prediction` is written.
- Unavailable non-mandatory fields are left out, never filled with
  placeholders.
- Timestamps use `yyyy-MM-ddTHH:mm:ss` (no fraction, no zone suffix).
- Paths inside the JSON use `/`.

## Write order: JSON is the trigger

The log JSON is the signal for the server to pick up the order and move
images and log to the cloud (user, 2026-11-02). Therefore:

- The JSON is written only after every image of the order has been
  copied completely to that destination.
- The JSON is written atomically: first to a temporary name the server
  ignores, then renamed to the final `.json` name, so the server never
  reads a half-written file.
- With several destinations, each destination gets its JSON only after
  its own images are complete.
- If copying fails, no JSON is written for that destination.

## File name and location

- File name: `<Identifier>_<TimeStampUTC digits>.json`, e.g.
  `K05666151BK4701_20220901074055.json`.
- Log: `<share>\<computer>\<project>\logs\<yyyy>\<MM>\<dd>\<file>`.
- Images: `<share>\<computer>\<project>\data\<yyyy>\<MM>\<dd>\`
  `<Identifier>_<TimeStampUTC digits>\<image>`.
- Example share layout: `\\<server>\<share>\N026995\hv_qg\...`, so the
  computer folder is a computer ID and the project folder a short project
  name.

## Fields

### Order level

| Key | Group | Type | Source |
|---|---|---|---|
| `TimeStampUTC` | mandatory | string | `StartTime` (start of test procedure), UTC, seconds |
| `LocalTime` | mandatory | string | `StartTime` in station local time |
| `LogVersion` | mandatory (screenshot only) | string | constant `1.0` |
| `Barcode` | mandatory | string | `OrderId` |
| `Identifier` | mandatory | string | `OrderId` (unique barcode, STO case) |
| `Machine` | mandatory (doc) / optional (screenshot) | object | settings |
| `Machine.Plant` | mandatory (doc) | string, 3 letters | setting, e.g. `SAT` |
| `Machine.Cam`, `.Chamber`, `.Equipment`, `.ComputerID` | optional | string | settings; `ComputerID` = computer name on the Upload tab (T027) |
| `OrderInfo` | optional | object | written when any child has a value |
| `OrderInfo.FP-Nr` | optional | string array | not in ZIP, left out |
| `OrderInfo.Steering` | optional | string | `ReportData.SteeringType` |
| `OrderInfo.StructuralGroup` | optional | string | `ReportData.AssemblyGroup` |
| `OrderInfo.Carline`, `.Workorder`, `.Modelyear` | optional | string | not in ZIP, left out |
| `Data` | mandatory | array | one entry per copied picture |

### Per picture: `Data[]`

| Key | Group | Type | Source |
|---|---|---|---|
| `ImagePath` | mandatory | string, `/` | path of the copied image (base: open Q1) |
| `TestTime` | mandatory | string, UTC | start of the prediction; open Q2 |
| `ModelID` | required if available | string | not in ZIP; left out today |
| `Project` | mandatory | string | setting; must equal the project path in DAISI Models |
| `ExecutionTime` | required if available | number (see conflicts) | AI test `Duration` in ms |
| `ExecutionTimeUnit` | required if available | string | `millisecond`, written with `ExecutionTime` |
| `PredictionType` | mandatory | string | `Skipped` when the AI test was skipped / no model; else setting (`Anomaly`, `Classification`, `Segmentation`) |
| `Prediction` | required if available | object | left out when `Skipped` |
| `ImageCrop` | required if available | object `X1,Y1,X2,Y2` integers | not in ZIP; left out (whole image assumed) |

### `Prediction` by `PredictionType`

- **Anomaly**: `AnomalyFound` (bool, mandatory), `Threshold` (float,
  mandatory), `Blobsize` (int, mandatory), `Coords` (int boxes
  `[x1,y1,x2,y2]`, optional).
- **Object detection**: `ClassProbability` (list of float lists,
  mandatory), `ClassName` (string list, mandatory), `Coords` (float boxes,
  optional).
- **Classification**: `LabelName`, `ExpectedClass`, `AIClass` (strings,
  mandatory), `ClassResult` (bool, mandatory), `Probability` (float list,
  optional), `Classes` (string list, optional). Reteach case:
  `ModelID = None`, `AIClass = None`, `ClassResult = true`.
- **Skipped**: no `Prediction`. `Project` must exist and the bucket
  folder with that name must already be created.

Today `order_report.json` has none of the Anomaly/Classification values
(`Threshold`, `Blobsize`, classes...). Until the producer adds them, only
`Skipped` can be written correctly.

## Source data with no target key

- `ExpectedPositions` code per picture (e.g. `A000*1B.ET03`).
- `ReportData.AssemblyBoardNr`.
- Order counts and timings, `CorrelationId`, test `Data` message.

## Conflicts between screenshot and documentation

1. `Machine` and `Machine.Plant`: optional in screenshot, mandatory in
   documentation. Proposal: follow documentation (stricter).
2. `ExecutionTime`: integer `12` in screenshot, "string" in
   documentation. Proposal: integer, as the example shows.
3. `ExecutionTimeUnit`: `millisecond` in screenshot, `milliseconds` in
   documentation text. Proposal: `millisecond`, as the example shows.
4. `LogVersion`: in screenshot, not in documentation. Proposal: write it.

## Decisions (user, 2026-11-02)

- `TestTime`: order `StartTime` plus the summed `AnalysisDuration` of the
  earlier pictures in `Results[]` order. Unique, approximate; replace when
  the producer adds a real per-picture start time.
- Order folder and JSON name: `<OrderId>_<yyyyMMddHHmmss>` with the time
  from the ZIP file name (producer's local end time), user decision
  2026-11-02 (T018), replacing the earlier StartTime UTC choice (T016).
  Deviates from the DAISI rule "Identifier + TimeStampUTC digits";
  `TimeStampUTC` inside the JSON stays the UTC start time. ZIP name order
  ID must equal the report `OrderId`.
- `yyyy\MM\dd` folders for `data` and `logs`: date of `StartTime` in UTC.
- Server/share name in these docs may stay; repository is private.
- `ImagePath` (user decision 2026-11-03, T023): full destination path
  with `/`, following the path layout template (T025). Default for share
  and local root: `<root>/<computer>/data/yyyy/MM/dd/<order>/<file>`,
  e.g. `//<server>/<share>/<computer>/data/...` (no project folder).
- Defaults applied until the open questions are answered: source file
  names (with `#`, `.bmp`) are kept; an existing log JSON marks the order
  as delivered and it is skipped unless overwrite is chosen; skipped = AI
  test message starts with `AI test skipped`; position code dropped;
  conflict proposals 1-4 applied.
- Implementation choice: a `Skipped` picture gets no `ExecutionTime`,
  because no model ran.

## Model folder as source (T020, user decisions 2026-11-02)

- Setting "AI models root"; picture `<position>#...` -> folder
  `<root>\<position>\meta.json`, exact name only.
- `ModelID` = `Meta.TrainID`, written whenever the folder exists, also
  for `Skipped` pictures.
- Test really ran: `PredictionType` = `Meta.PredictionType`; for
  `Anomaly`: `Prediction.AnomalyFound` = AI test not passed,
  `Threshold` and `Blobsize` (rounded to integer) from `meta.json`.
- `Project` stays the setting; user asks DAISI team whether
  `Meta.Project` should be used.

## Without order_report.json (T021, user decisions 2026-11-02)

- Report is optional (fallback); a present but unreadable report fails.
- `TimeStampUTC`/`LocalTime`: report `StartTime`, else earliest picture
  capture time, else ZIP name time (local -> UTC).
- `Barcode`/`Identifier`: report `OrderId`, else ZIP name order ID.
- `TestTime`: always the picture capture time from the file name
  (local -> UTC); fallback start + earlier analysis durations.
- Picture without known test result: `PredictionType` from model
  `meta.json`, else setting; no `ExecutionTime`, no `Prediction`.

## Known gap: TestTime not unique (T017)

In all 50 real archives the summed analysis times are milliseconds to a
few seconds, so with the second-resolution format several pictures of one
order get the same `TestTime`. The spec says it "should be different".
Options: spread pictures evenly between `StartTime` and `EndTime`, use
the capture time from the picture file name, or have the producer add a
real per-picture start time.

## Open questions

1. `ImagePath` form: answered by user 2026-11-03 (see Decisions, T023).
2. Picture file names: free to choose per spec, and we keep the source
   names. These contain `#` (e.g. `124#1294#2026-...ms.bmp`). In a web
   URL `#` starts a fragment, so the labelling page or the bucket could
   break on them. Keep, or rename on copy (e.g. replace `#` with `_`)?
   Pictures are also `.bmp` while the spec examples use `.png`.
3. Temporary JSON name `<name>.json.tmp`: does the server react only to
   `*.json`?
4. Re-copy of an order whose JSON already exists (server may already
   have moved it): skip, or allow a duplicate?
5. Skipped detection: an AI test counts as skipped when its `Data`
   message starts with `AI test skipped`. Acceptable, or should the
   producer add an explicit flag?
6. Position code: drop, or put somewhere?
