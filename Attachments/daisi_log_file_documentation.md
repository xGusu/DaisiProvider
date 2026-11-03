# DAISI Log File Documentation

Supplied by the user on 2026-11-02 (pasted text, kept as received apart
from Markdown formatting). Companion screenshot: `target_json_schema.png`.

---

You can send back this file after each time your deployed DAISI model was
executed. For example after one part was tested in an EOL station. It also
serves for debugging purposes. Additionally, we can provide you plenty of
information in an Power BI Dashboard automatically. (e.g. AI model
performance or number and type of parts processed by the station). In also
the input for the labling webpage - images with a log json can be displayed
on the webpage. Labeled images will be added to the trainings set by moving
them to the correct folder inside the s3 bucket.

## Explanation

**TimeStampUTC** [mandatory, string]
Timestamp of the start of the test procedure in UTC time. In
"yyyy-mm-ddThh:mm:ss" pattern. (e.g. "2022-07-22T23:24:04")

**LocalTime** [mandatory, string]
TimeStampUTC in local time and in same date string pattern

**Barcode** [mandatory, string]

**Identifier** [mandatory, string]
Unique identifier of the product. If the barcode is unique, such as in STO,
it is just as the barcode. In the MTO case the identifier can be the
combination of FP-number, Lot-number, and serial-number. Worst case, it has
to be some combination with unique timestamp.

**Machine** [mandatory, dict]

- **Plant** [mandatory, string]
  Manufacturing plant name as a 3 letter abbreviation.
- **Other** [optional, strings]
  Other attributes of the machine as optional additional information, e.g.
  cam, chamber, equipment id(=Betriebsmittel-Nr), computer id, etc.

**OrderInfo** [optional, dict]
Information from the MES that might be of interest.

- **FP-Nr** [optional, list [string]]
  FP number of the product or of all parts that the product exists of.
- **Other** [optional, string]
  Other information from MES, e.g. carline, workorder, steering, or
  structural group

**Data** [mandatory, list[ dict ]]
Logged output of the AI models. Each prediction (or future model)
corresponds to one dict.

- **ImagePath** [mandatory, string]
  Path of the saved image that was used for the prediction. Path should be
  with "/"s.
- **TestTime** [mandatory, string]
  UTC time of the start of the actual prediction of the model. Should be
  different for all AI predictions.
- **ModelID** [required if available, string]
  ID of the AI model for the prediction; same value as ModelID from the
  meta.json. Is the same value as the output folder in the DAISI subfolder
  in the oa-bucket.
- **Project** [mandatory, string]
  Name of the project. Must be the same as the path to your project in the
  DAISI Models section. Otherwise it will not be available for labeling.
- **ExecutionTime** [required if available, string]
  Necessary time for the execution. Consists of the load time of the model
  (can be 0 if the model stays in memory) plus the actual prediction time.
- **ExecutionTimeUnit** [required if available, string]
  E.g. "milliseconds" or "seconds"
- **PredictionType** [mandatory, string]
  "Anomaly", "Classification", or "Segmentation". Use "Skipped" for future
  models.
- **Prediction** [required if available, dict]
  Subdictionary with the output of the model. Schema depends on the
  PredictionType. The mandatory keys of the subdictionary depend on the
  PredictionType.
- **ImageCrop** [required if available, dict of string:integer
  combinations]
  If not specified the whole image is assumed as input. If a key
  ("x1","y1","x2","y2") is missing, their default value for the whole image
  is assumed.

## Prediction for Anomaly

- **AnomalyFound** [mandatory, bool]
  True if an anomaly has been found; false otherwise.
- **Threshold** [mandatory, float]
  Threshold that was used for the prediction.
- **Blobsize** [mandatory, integer]
  Blobsize (= size of the connected component) that was used for the
  prediction.
- **Coords** [optional, list[ list [ integer ]]]
  List with coordinates of the found bounding boxes. Each bounding box
  corresponds to a sublist [x1,y1,x2,y2] were (x1,y1) is the upper left
  corner and (x2,y2) is the lower right corner of the bounding box. The
  origin/upper left corner of the image shall be at (0,0) and x values
  increase toward the right of the image; y values increase toward the
  bottom of the image.

## Prediction for Object detection

- **ClassProbability** [mandatory, list[ list [ float ]]]
  For each prediction a list is returned that has the length of the class
  numbers. The probability is returned for each class (first list entry is
  the probability of the fist class, and so on). The list position with the
  hightest probability is the predicted class.
- **ClassName** [mandatory, list [ string ]]
  Returns a list of predicted class names
- **Coords** [optional, list[ list [ float ]]]
  Bounding boxes as for Anomaly, with float values.

## Prediction for Classification

- **LabelName** [mandatory, string]
  Name of the label that will be predicted by the model. Same as in the
  meta.json for the model.
- **ExpectedClass** [mandatory, string]
  Class from the MES data, i.e. what is wanted / the customer ordered.
- **AIClass** [mandatory, string]
  Class that the AI model predicts for the product.
- **ClassResult** [mandatory, bool]
  True if MES_Class and AI_Class are the same, otherwise false.
- **Probability** [optional, list[ float ]]
  List of the output probabilities of the model. Must have the same order
  as the Classes.
- **Classes** [optional, list[ string]]
  List of all possible output classes of the model. For each class the
  model predicts an probability, which is under the probability-key. The
  order of the probability and classes list must be the same.

If you want to create images of a newly added class for the purpose of
reteaching then you will skip the actual execution of the model (because
the model will always predict something wrong for a class that it doesn't
know). But it's also not "Skipped" as in the next section, because you
obviously know the correct class during the teaching process. For such
cases set the values as: ModelID = None, AIClass = None,
ClassResult = True.

## "Skipped" PredictionType

If the AI model is not yet created or live, but images should already be
gathered and labelled, then use this PredictionType. The "Project"-key must
already be specified and the folder in the bucket with the same name must
be already created.

## File name and location

The name of the log file shall be the combination of the Identifier and the
TimeStampUTC in a digit-only pattern, separated by a "_", e.g.:

`K05666151BK4701_20220901074055.json`

The final save path of the log file on the shared network folder will look
like:
`\\srvsat02\predqual-sat-ingoing\N026995\hv_qg\logs\2022\10\10\K05666151BK4701_20220901074055.json`

The path for the images will look like:
`\\srvsat02\predqual-sat-ingoing\N026995\hv_qg\data\2022\10\10\K05666151BK4701_20220901074055\image1.png`

### Path segments

(Added from a second user paste, 2026-11-02.)

| Example | Explanation |
|---|---|
| `\\srvsat02\predqual-sat-ingoing\` | the shared network folder |
| `N026995\` | possibly several computers will collaborate in one project; they have a folder each |
| `hv_qg\` | a folder for this project. Should be agreed upon and similar to the folder structure in the bucket |
| `data\` or `logs\` | only 2 options |
| `2022\11\12\` | year \ month \ date |
| test_name, e.g. `K05666151BK4701_20220901074055` | See above how the name is created. The log-json will be saved under this name. The corresponding data for this test, e.g. images, will be saved in a folder with the same name. The name of all data files can be freely chosen but should make sense, e.g. the Duncan project used CAM1.png, CAM2.png, CAM3.png, … |
