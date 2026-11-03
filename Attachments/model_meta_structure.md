# Source: AI Model Folders and meta.json

Derived 2026-11-02 from `D:\Ai\Models\BMW_XNF_AssemblyBoard` on the
development PC (read only). Proposed by the user as a source for the
prediction part of the DAISI log (T020).

## Folder layout

```
<models root>\
  101\          one folder per position number
  102\
  102_B\        variant of position 102 (suffix _B, _Fritz, ...)
  113_Fritz\
  ...
  backup\       older models (e.g. 102_before16092026, 115_SF)
```

Each model folder holds: `meta.json`, `model.onnx`, `preprocessing.onnx`,
`preprocessing_meta.json`, `input.png`, `output.csv`, `blob_vs_thr.csv`,
`acc_for_blob_vs_thr_dict.json`, `Report.pdf`.

The folder number matches the `<position>` prefix of the picture file
names (`124#1294#...bmp`) and the `ExpectedPositions` numbers in
`order_report.json`.

## meta.json (relevant keys)

| Key | Example | Possible DAISI log use |
|---|---|---|
| `Meta.TrainID` | `20260716_202858_1` | `ModelID` (equals last segment of `S3Prefix`) |
| `Meta.Project` | `BMW_XNF_AssemblyBoard_C066836_BJ1JA01C08_101` | `Project`? |
| `Meta.PredictionType` | `Anomaly` | `PredictionType` |
| `Meta.Station` | `BMW_XNF_AssemblyBoard` | |
| `Meta.TestCase` | `C066836` | |
| `Meta.Variante` | `BJ1JA01C08` | compare with `ReportData.AssemblyGroup`? |
| `Meta.S3Prefix` | `s3://<bucket>/BMW_XNF_AssemblyBoard/C066836/BJ1JA01C08/101/20260716_202858/20260716_202858_1` | |
| `Meta.CreationDate` | `2026-07-16T20:50:16` | |
| `Inputs.Image.LayerInfo[Name=ImageCrop]` | `{"Y":251,"X":291}` | crop size only, no position: not enough for `ImageCrop` X1/Y1/X2/Y2 |
| `Outputs.Result.LayerInfo[Name=Threshold].Threshold` | `0.27` | `Prediction.Threshold` |
| `Outputs.Result.LayerInfo[Name=Blobsize].Blobsize` | `13000.0` | `Prediction.Blobsize` (integer in log) |

Other keys: `AIVersion`, `ModelInfix`, `DocumentVersion`,
`TensorflowVersion`, `OnnxOpset`, `OnnxVersion`, `ModelPrecission`,
`CudaVersion`, input/output layer names and shapes.

## Survey of all 20 meta.json files

- All `PredictionType` = `Anomaly`.
- `Project` is the long form `<Station>_<TestCase>_<Variante>_<position>`
  in most folders, but only `BMW_XNF_AssemblyBoard` in `113_Fritz`, `114`
  and several backups. Folder `113` has project suffix `_113_B`.
- `Variante` is `BJ1JA01C08` in most, `BJ1JA01C07` in `114` and older
  backups. The sample order report has `AssemblyGroup` `BJ1JA01C09`.
- Threshold 0.05-0.54, Blobsize 304-54047 (stored as float).
- Folders with suffix (`102_B`, `115_B`, `116_B`) have the same position
  and project as the plain folder but a different TrainID, Threshold and
  Blobsize.

## Not in meta.json

`AnomalyFound` (mandatory in `Prediction`) and `Coords` are results of an
actual model run; they must come from the run (e.g. `order_report.json`),
not from the model folder.
