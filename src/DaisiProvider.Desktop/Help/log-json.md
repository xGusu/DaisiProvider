# The log JSON

Every delivered order gets one log JSON file. It describes the order and each copied picture.

## What it is for

- The server treats it as the signal to move the order's images and log to the cloud.
- It feeds the Power BI dashboard.
- It makes the images available on the labelling web page. Images without a log JSON are not shown for labelling.

## When it is created

Last, after every file is copied. It is written as `<name>.json.tmp` and then renamed to `<name>.json`, so the server never reads a half-written file. After a failure no JSON is written.

Its name and folder come from the log template on the Destination page.

## Where the values come from

1. `order_report.json` inside the ZIP, when present.
2. The ZIP file name and the picture file names.
3. The **JSON rules** page and the **Computer** field.
4. The AI models root, when set: `<root>\<position>\meta.json`.

## Example

```
{
  "TimeStampUTC": "2026-10-29T02:52:51",
  "LocalTime": "2026-10-29T03:52:51",
  "LogVersion": "1.0",
  "Barcode": "K695531412G7101",
  "Identifier": "K695531412G7101",
  "Machine": { "Plant": "SAT", "Cam": "3", "ComputerID": "N026995" },
  "OrderInfo": { "Steering": "LL", "StructuralGroup": "BJ1JA01C09" },
  "Data": [
    {
      "ImagePath": "//server/share/N026995/Data/2026/10/29/K695531412G7101_20261029045749/101_1381_2026-10-29_03h52m51s099ms.bmp",
      "TestTime": "2026-10-29T02:52:51",
      "ModelID": "20260716_202858_1",
      "Project": "PROJECT/N026995/101",
      "ExecutionTime": 12,
      "ExecutionTimeUnit": "millisecond",
      "PredictionType": "Anomaly",
      "Prediction": { "AnomalyFound": true, "Threshold": 0.27, "Blobsize": 13000 }
    }
  ]
}
```

Times use `yyyy-MM-ddTHH:mm:ss`, whole seconds, without a zone. Values that are not available are left out, never filled with placeholders.

## Order fields

| Field | Required | Source |
| --- | --- | --- |
| `TimeStampUTC` | Always | Order start in UTC (see below) |
| `LocalTime` | Always | Same moment in this PC's time zone |
| `LogVersion` | Always | Fixed value `1.0` |
| `Barcode` | Always | `OrderId` from the report, else from the ZIP name |
| `Identifier` | Always | Same as `Barcode` |
| `Machine.Plant` | Always | JSON rules: Plant |
| `Machine.Cam` | If set | JSON rules: Cam |
| `Machine.Chamber` | If set | JSON rules: Chamber |
| `Machine.Equipment` | If set | JSON rules: Equipment |
| `Machine.ComputerID` | If set | Destination: Computer |
| `OrderInfo.Steering` | Report only | `ReportData.SteeringType` |
| `OrderInfo.StructuralGroup` | Report only | `ReportData.AssemblyGroup` |
| `Data` | Always | One entry per copied picture |

**Order start:** `StartTime` from the report. Without a report, the earliest capture time in the picture names. Without that, the time in the ZIP name. Times from names are local and are converted to UTC with this PC's time zone.

## Picture fields (one per entry in Data)

| Field | Required | Source |
| --- | --- | --- |
| `ImagePath` | Always | Full destination path of the picture, with `/` |
| `TestTime` | Always | Capture time in the picture name, in UTC |
| `ModelID` | If known | `TrainID` from the position's `meta.json` |
| `Project` | Always | JSON rules: DAISI project, tokens filled in |
| `ExecutionTime` | Real AI run | AI test duration in milliseconds |
| `ExecutionTimeUnit` | Real AI run | Fixed value `millisecond` |
| `PredictionType` | Always | See below |
| `Prediction` | Real Anomaly run | `AnomalyFound`, `Threshold`, `Blobsize` |

Picture names look like `<position>#<n>#<yyyy-MM-dd>_<HH>h<mm>m<ss>s<fff>ms.<ext>`. The position is the number before the first `#`. Position and capture time are read from the name in the ZIP; the copied file and `ImagePath` use the name with `#` replaced by `_`. If a name has no capture time, `TestTime` falls back to the order start plus the analysis times of the earlier pictures, or to the order start.

## PredictionType

| Case | PredictionType | Other fields |
| --- | --- | --- |
| JSON rules setting is Skipped | `Skipped` | `ModelID` if known; no `ExecutionTime`, no `Prediction` |
| Report says the AI test was skipped | `Skipped` | `ModelID` if known; no `ExecutionTime`, no `Prediction` |
| AI test really ran | From `meta.json`, else the setting | `ExecutionTime`, and `Prediction` for Anomaly |
| No result known (no report, or picture not in it) | From `meta.json`, else the setting | No `ExecutionTime`, no `Prediction` |

A test counts as skipped when its message starts with `AI test skipped`.

`Prediction` is written only for `Anomaly`, when the AI test really ran and `meta.json` gives `Threshold` and `Blobsize`. `AnomalyFound` is true when the test did not pass. Prediction details for `Classification` and `Segmentation` are not written yet.

## Options on the JSON rules page

| Option | Effect |
| --- | --- |
| Plant (mandatory) | 3 letters, e.g. `SAT`. Goes to `Machine.Plant`. |
| Cam, Chamber, Equipment | Go to `Machine`; left out when empty |
| DAISI project (mandatory) | Goes to `Project` for each picture. Must equal the project path in the DAISI Models section, otherwise pictures are not available for labelling. |
| Prediction type (mandatory) | Used when `meta.json` gives none. `Skipped` forces every picture to Skipped. |
| AI models root | Folder with one subfolder per position. Optional. |

**Tokens in DAISI project**, filled per picture:

| Token | Value |
| --- | --- |
| `{position}` | Number before `#` in the picture name |
| `{computer}` | Computer on the Destination page |
| `{project}` | Project on the Destination page |

Example: `{project}/{computer}/{position}` gives `Project/N026995/101`. If a token cannot be filled for a picture, the order fails before copying. Unknown tokens count as a missing setting.

## AI models root

Picture `124#...` uses the folder `124` exactly. Folders such as `124_B` and `backup` are ignored. A position without a folder simply gets no model values. A configured root that does not exist, or a broken `meta.json`, fails the order before copying.

## Without order_report.json

The report is optional. Without it the JSON is built from the ZIP name, the picture names and the settings. Lost in that case: `OrderInfo`, `ExecutionTime` and `Prediction`.

## Known limits

- `TestTime` should differ for every picture, but capture times have whole seconds here, so pictures of one order can share a value.
- `ImageCrop`, `Carline`, `Workorder` and `Modelyear` are not in the source data and are left out.
