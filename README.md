# DaisiProvider

DaisiProvider is a Windows application for monitoring daily production
folders, copying order pictures from ZIP files to local or network storage,
and writing a DAISI log JSON for each order. It is a desktop application
that watches a local archive folder while it is open, or delivers ZIPs by
hand. Press F1 in the app for the full user help, including every field of
the log JSON.

## Version and releases

The version is set once in `Directory.Build.props` and applies to every
project. The window title and the Worker show it. A release raises it
there, is built in Release configuration from scratch, and the commit is
tagged `vX.Y.Z`.

### Installing on another PC

Two publish profiles exist for the Desktop app (VS Code: Run Task):

- `publish-desktop-selfcontained` -> `publish\DaisiProvider-<version>-selfcontained\`
  (about 140 MB). Includes .NET; copy the folder to any 64-bit Windows PC
  and start `DaisiProvider.Desktop.exe`. Nothing to install.
- `publish-desktop-runtime` -> `publish\DaisiProvider-<version>-runtime\`
  (under 1 MB). The PC needs the .NET 10 Desktop Runtime (x64).

Both tasks skip NuGet sources that cannot be reached (e.g. the company
feed off the company network). `publish/` is not stored in git.

### Signature

Both publish tasks sign `DaisiProvider.Desktop.exe` and the own
`DaisiProvider.*.dll` files with a self-signed code-signing certificate
(`CN=Gustav Vanderdi`, 5 years) through `assets/signing/sign-publish.ps1`,
then check every file. The Microsoft .NET files are already signed by
Microsoft. A timestamp from `timestamp.digicert.com` keeps the signature
valid after the certificate expires; without internet the files are
signed without one. The `-unsigned` tasks publish without signing.

The certificate's private key lives only in the Windows certificate store
of the publishing PC (current user, not exportable), never in git. A PC
that publishes runs `assets/signing/new-certificate.ps1` once; that
creates a new certificate there, so each publishing PC signs with its own
key and its own `.cer`.

Self-signed means Windows trusts the signature only where the public
certificate `assets/signing/DaisiProvider-codesign.cer` is installed.
Elsewhere the files still show the signature, but as not trusted. To
trust it on another PC: double-click the `.cer`, **Install
Certificate**, Current User, place it in **Trusted Root Certification
Authorities**, and again in **Trusted Publishers**.

On the new PC the settings live in
`C:\ProgramData\DaisiProvider\settings.json`. The file can be copied, but
the network password must be entered again (it is encrypted for one PC
only), and Computer must be changed to the new PC's name.

### Release history

- 1.0.0: first release. Upload of one ZIP or an archive folder to a local
  or network destination, configurable copy rules, path layout and DAISI
  log JSON.
- 1.1.0: folder monitor with retries and a persistent finished list,
  compact window with left menu, F1 help.
- 1.2.0: `#` in copied file names becomes `_`; copy rules can be limited
  to picture positions.
- 1.2.1: Save button turns orange while settings are unsaved; company
  logo in the left menu; own app icon; author and company in the exe
  details. The 1.2.1 packages were rebuilt after the tag with a fix that
  shows the icon on the window and taskbar too; their file version names
  the later commit.
- 1.2.2: icon on window and taskbar; packages signed with a self-signed
  certificate.
- 1.2.3: monitor shows each scan's range and counts and lists ZIPs
  finished earlier as grey rows; a new setup processes the whole look
  back range.

### Branding and icon

The company logo is kept as received in
`assets/branding/draexlmaier-logo-white.svg`. WPF cannot show SVG, so its
paths are copied into `App.xaml` as the `CompanyLogo` drawing, shown at
the top of the left menu; a new logo means copying the paths again. The
app icon `assets/icon/daisiprovider.ico` (a ZIP with an arrow into a
folder) is drawn by `assets/icon/make-icon.ps1`; change the script and run
it with Windows PowerShell to redraw it. Company and copyright in the exe
file details come from `Directory.Build.props`.

## Architecture

- `DaisiProvider.Domain` contains upload state and order identity.
- `DaisiProvider.Application` contains upload processing, copy rules, the
  DAISI log builder and ports.
- `DaisiProvider.Infrastructure` implements ZIP and `order_report.json`
  reading, file copying, log JSON writing, the settings file and in-memory
  progress.
- `DaisiProvider.Worker` provides a one-ZIP command-line test entry point.
- `DaisiProvider.Desktop` provides the window. Its user help lives in
  `src/DaisiProvider.Desktop/Help/*.md`: Markdown files built into the app
  and shown with F1. Keep them in step with the code.
- `tests/DaisiProvider.Tests` holds xUnit tests for copy rules, ZIP and
  report reading, the log builder, the processing order, the settings
  file and the monitor, including an end-to-end run with real files.
- `.vscode/launch.json` starts the desktop app or command-line Worker.

## What happens to one ZIP

1. Wait until the ZIP is no longer locked.
2. Read the ZIP file name, which must be
   `order_<OrderId>_raw+result_<yyyyMMdd>_<HHmmss>.zip`, and
   `order_report.json` if the ZIP has one. The report is optional: without
   it the log is built from the ZIP name, the picture names and the
   settings. A report that exists but cannot be read fails the order. With
   a report, the order ID in the name must equal `OrderId` in the report.
3. Build the destination paths from the path layout (see "Path layout"
   below). Default, the same for a local root and a network share:
   - files: `<root>\<computer>\Data\<yyyy>\<MM>\<dd>\<OrderId>_<yyyyMMddHHmmss>\`
   - log: `<root>\<computer>\Logs\<yyyy>\<MM>\<dd>\<OrderId>_<yyyyMMddHHmmss>.json`
   - `<yyyyMMddHHmmss>` is the time from the ZIP file name (the
     producer's local end time), as the user decided. The DAISI
     documentation asks for the UTC start time here instead.
   - the date folders use the UTC date of the order start (see below).
   For a network share the app first logs in with the saved user and
   password. No drive letter is needed: Windows connects to the UNC path
   directly. The connection is made once and stays open for later orders. Then, for both
   kinds, the configured root (local root or network path) must exist and
   accept a small test file that is deleted right away. The root is never
   created by the app, because a missing root usually means a wrong path.
   Otherwise the order stops before anything is copied. A successful
   check is remembered for 5 minutes, so a folder run does not test the
   destination again for every ZIP. After a failed order, and on **Test
   connection**, the destination is checked again at once.
4. If the log JSON already exists, the order counts as already delivered
   and is skipped, because the server may already have moved it to the
   cloud. The overwrite checkbox forces a new delivery.
5. Copy the files selected by the copy rules.
6. Write the log JSON, last. The JSON is the server's signal to move the
   order to the cloud, so it is written only when every file is copied,
   never after a failure. It is written under `<name>.json.tmp` and then
   renamed, so the server never sees a half-written file.

## Copy rules and settings

What gets copied from a ZIP is decided by a list of copy rules that the
user defines. Each rule names one folder inside the ZIP (empty means the
ZIP root) and the file types to copy from it, as plain extensions such as
`bmp, png`. `*` copies every file in that folder. Only files directly in
that folder are matched, not files in deeper subfolders. Matching ignores
case. Disabled rules are skipped. Rules are not derived from ZIP contents.

A rule can also list positions, such as `101, 124`; empty means all. The
position is the number before the first `#` in the file name, so a station
can deliver only the camera positions it is responsible for. A file of
another position, or without a position, is then not copied by that rule.
A ZIP whose files fit the rules but contain none of the listed positions
is finished as "Nothing to copy": nothing is copied, no log JSON is
written, and it is recorded as finished so the monitor does not retry it.
A ZIP with no file for the rules at all still fails, because that points
to a wrong rule or a broken ZIP.

All selected files are copied into one order folder under their original
names, with every `#` replaced by `_`. Older pictures carry `#`, which is
risky in web addresses; newer ones will not, so the replacement only keeps
old archives working. Position, capture time and the match against
`order_report.json` still read the name inside the ZIP. If two selected
files end up with the same name (e.g. `101#1.bmp` and `101_1.bmp`), the
ZIP fails validation.

Without a settings file the defaults are: `raw` with `bmp, png, jpg`
(enabled) and `result` with `txt` (disabled). The real production
archives store pictures as BMP under `raw` and one `<picture>_result.txt`
per picture under `result`.

The window has a left menu with five pages: **Monitor**, **Manual
upload**, **Destination**, **Copy rules** and **JSON rules**, which holds
the DAISI log values that are not inside the ZIP.

The destination is either a **Local PC path** (root folder) or a
**Network share** (UNC path, user, password). **Test connection** logs in
to the share and checks that the path exists and is writable. Computer and
project names apply to both kinds.

### Path layout

The Destination page holds two templates, one for the files folder and one for
the log JSON file. Both are shared by Local and Network. Token buttons
insert at the cursor; the preview shows an example order.

| Token | Value |
| --- | --- |
| `{root}` | local root or network path |
| `{computer}` | computer name |
| `{project}` | project name (needed only when used) |
| `{yyyy}` `{MM}` `{dd}` | UTC date of the order start |
| `{OrderId}` | order ID |
| `{OrderTime}` | ZIP name time, `yyyyMMddHHmmss` |

Rules: a template starts with `{root}`, contains `{OrderId}` (otherwise
orders would overwrite each other), and the log template ends in `.json`.
`\` and `/` both separate folders. Every folder or file name produced is
checked on its own, so a value can neither add folders nor climb out of
the root. Unknown tokens are refused. An empty template uses the default.

**Save settings** stores all pages in
`%ProgramData%\DaisiProvider\settings.json`. ProgramData was chosen so
a later Windows service can read the same file without a logged-in user.
The network password is stored encrypted with Windows DPAPI for this
machine: the app and a later service on this PC can read it, a copy of
the file on another PC cannot. The command-line Worker reads the same file
but always takes a local destination from its arguments.

## Folder monitor

The ZIPs are always local; only the copy target may be on the network. So
the monitor scans `<folder>\yyyy\MM\dd\*.zip` at a settable interval
(default 60 s) rather than using file events, and a scan also picks up
ZIPs created while the app was closed. It reads only the day folders from
the later of **Process ZIPs from** and today minus **Look back days**
(default 30). Older folders are neither read nor shown, which also keeps
the scan work small. A new setup starts with **Process ZIPs from** at
today minus the look back days, so the first Start covers the whole look
back range; a date saved earlier is kept.

ZIPs run one at a time, oldest first, through the same steps as a manual
upload. After a failure a ZIP is retried three times, five minutes apart,
and then waits for a manual retry.

The destination cannot tell whether an order was delivered, because the
server moves delivered orders to the cloud and the log JSON disappears. So
finished ZIPs are stored by file name in
`%ProgramData%\DaisiProvider\processed.json`, written with a temporary file
and rename. If that list cannot be read the monitor processes nothing,
since an empty list would resend every order in range.

A monitor that skips everything looks idle, so each scan reports its date
range and how many ZIPs it found, how many finished earlier and how many
are new. ZIPs on the finished list are shown once per Start as greyed
"Finished earlier" rows with their result and time.

**Start** saves all settings and takes a snapshot of them; changes apply
after Stop and Start. A running monitor starts again with the app. Stop
cancels the current ZIP, which writes no JSON and runs again later. While
the monitor runs, Manual upload is off so one ZIP cannot run twice.

## DAISI log JSON

The log JSON travels with the images to the DAISI share, feeds a Power BI
dashboard and makes the images available on the labelling web page.

Order start (`TimeStampUTC`, `LocalTime`, date folders): `StartTime` from
`order_report.json`; without a report the earliest picture capture time;
without either the ZIP name time. Picture names and ZIP name carry local
time and are converted to UTC with the PC's time zone.

`Barcode` and `Identifier`: order ID from the report, else from the ZIP
name. `OrderInfo.Steering` and `OrderInfo.StructuralGroup` exist only with
a report.

`TestTime` per picture: the capture time in the picture name
(`<position>#<n>#<yyyy-MM-dd>_<HH>h<mm>m<ss>s<fff>ms.<ext>`), converted to
UTC. Several pictures can share the same second.

Values from the **JSON rules** tab: `Machine.Plant` (mandatory),
`Machine.Cam`, `.Chamber`, `.Equipment`, `Project` (mandatory, must equal
the DAISI Models path), and the prediction type. Processing refuses to
start while a mandatory value is missing. `Machine.ComputerID` is the
computer name from the Destination page; the name is kept in that one
place only.

The DAISI project may contain tokens that are filled per picture:
`{position}` (number before `#` in the picture name), `{computer}` and
`{project}` (computer and project names from the Destination page), e.g.
`{project}/{computer}/{position}`. If a token cannot
be filled for any picture of the order, the order fails before copying,
because a wrong project makes the picture unavailable for labelling.
Unknown tokens count as a missing setting.

`ImagePath` is the full destination path of the picture with `/`, e.g.
`//srvlme04/presqual-ingoing/<computer>/Data/2026/10/29/<order>/<file>`
for a share or `D:/Data/uploadtest/<computer>/Data/...` for a local root
(default layout).

One `Data[]` entry is written per copied picture (bmp, png, jpg, jpeg, tif,
tiff, gif). `PredictionType` per picture:

- AI test reported as skipped: `Skipped`, no execution time, no
  `Prediction`.
- AI test really ran: from the model's `meta.json`, else the setting;
  execution time; `Prediction` when the model provides it (below).
- No test result known (no report, or picture missing in it): from the
  model's `meta.json`, else the setting; no execution time, no
  `Prediction`.

`ImageCrop` is left out.

Optional **AI models root** (JSON rules tab), e.g.
`D:\Ai\Models\BMW_XNF_AssemblyBoard`: it holds one folder per position
number with a `meta.json` (see `Attachments/model_meta_structure.md`). A
picture `124#...` uses folder `124` exactly; folders such as `124_B` and
`backup` are ignored. When the folder exists the entry gets `ModelID`
(= `Meta.TrainID`), also for skipped pictures. When the AI test really
ran it also gets `PredictionType` from `meta.json` and, for `Anomaly`, a
`Prediction` with `AnomalyFound` (= test not passed), `Threshold` and
`Blobsize`. A configured root that does not exist, or a broken
`meta.json`, fails the order before copying.

Requirement sources:

- `Attachments/daisi_log_file_documentation.md`: official field
  descriptions, prediction types, file name and location rules.
- `Attachments/target_json_schema.png`: example report with colour-coded
  mandatory, required-if-available and optional keys.
- `Attachments/target_json_schema.md`: the combined requirements, the
  source of each value, decisions, conflicts and open questions.
- `Attachments/order_report_structure.md`: the source data inside each
  ZIP.

## Test

Start `DaisiProvider Upload GUI` in VS Code (F5). On **JSON rules** enter
at least Plant and DAISI project, then save. On **Manual upload** select a
ZIP and choose **Copy this ZIP**, or select an archive root folder and
choose **Process folder**, which processes every ZIP below it one after
another and lists failures at the end. On **Monitor** choose the archive
folder and press **Start**.

Progress stays in memory while the GUI runs. Re-running a partially copied
order resumes from its file count. Closing the app clears progress; copied
files remain.

VS Code also offers `Upload one ZIP (command line)`. Unit tests run with
`dotnet test DaisiProvider.sln`.

The app does not yet copy to several destinations or run as a Windows
service, so the monitor works only while the window is open.
