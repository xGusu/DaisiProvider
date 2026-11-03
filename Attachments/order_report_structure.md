# Source: order_report.json Structure

Derived 2026-11-02 from the 50 archives in `D:\Data\Archives` (read only).
Values below are illustrative, not real order data.

## Where data lives in each ZIP

- `order_report.json` (ZIP root): the only machine-readable data source.
- `raw/<picture>.bmp`: the pictures.
- `result/<picture stem>_result.txt`: human-readable copy of the matching
  `Results[]` entry. Adds no data beyond `order_report.json`.

## Order level

| Field | Type | Example |
|---|---|---|
| `OrderId` | string | `K0000000000000000000` |
| `StartTime` | ISO 8601 UTC | `2026-10-21T14:01:43.62Z` |
| `EndTime` | ISO 8601 UTC | `2026-10-21T14:02:08.21Z` |
| `TotalDuration` | TimeSpan `hh:mm:ss.fffffff` | `00:00:24.5914422` |
| `TotalImages` | int | `6` |
| `PassedCount` | int | `0` |
| `FailedCount` | int | `6` |
| `AverageAnalysisTime` | TimeSpan | `00:00:00.0121827` |
| `MinAnalysisTime` | TimeSpan | `00:00:00.0000095` |
| `MaxAnalysisTime` | TimeSpan | `00:00:00.0730121` |
| `ZipPath` | string | path of the ZIP when it was created |
| `Results` | array | one entry per picture, see below |
| `ExpectedPositions` | string array | `"124;A000*1B.ET03"` = position;code |
| `ReportData.AssemblyBoardNr` | string | `00000/00000` |
| `ReportData.AssemblyGroup` | string | `XX0XX00X00` |
| `ReportData.SteeringType` | string | `LL` |
| `TestSummaries.<name>` | object | `TestName`, `PassedCount`, `FailedCount`, `AverageDuration` |

## Per picture: `Results[]`

| Field | Type | Notes |
|---|---|---|
| `OrderId` | string | same as order |
| `ImagePath` | string | old machine path; separators `\` or `/` vary. Only the leaf filename is usable. |
| `Success` | bool | |
| `Tests[]` | array | `TestName`, `Passed` (bool), `Data` (string), `Duration` (TimeSpan) |
| `Error` | string or null | |
| `CorrelationId` | GUID string | |
| `AnalysisDuration` | TimeSpan | |

## Picture filename

Format: `<position>#<number>#<yyyy-MM-dd>_<HH>h<mm>m<ss>s<fff>ms.bmp`,
e.g. `124#1294#2026-09-01_19h29m06s739ms.bmp`.

- `<position>` matches the number before `;` in one `ExpectedPositions`
  entry for all 1275 pictures in all 50 archives. That gives each picture
  its position code (e.g. `A000*1B.ET03`).
- Meaning of the second number is unknown.
- The timestamp is the capture time.
- Positions total 1596, pictures 1275: some positions have no picture.

## Survey facts (all 50 archives)

- Every `Results[]` entry has at least one entry in `Tests[]` (test `AI`).
- In the inspected sample, all AI tests were skipped ("no Inference model
  configured"), `Passed=false`, `Success=false`.
