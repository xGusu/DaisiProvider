# DaisiProvider

DaisiProvider is a Windows application for monitoring daily production
folders, copying order pictures from ZIP files to local or network storage,
and generating order reports. Current milestone provides a desktop UI for
testing ZIP validation, picture copying, and in-session progress.

## Architecture

- `DaisiProvider.Domain` contains upload state and order identity.
- `DaisiProvider.Application` contains upload processing and ports.
- `DaisiProvider.Infrastructure` implements ZIP handling and in-memory progress.
- `DaisiProvider.Worker` provides a one-ZIP command-line test entry point.
- `DaisiProvider.Desktop` provides the Upload test UI.
- `.vscode/launch.json` starts the Upload UI or command-line Worker.

## Test one ZIP

Start `DaisiProvider Upload GUI` in VS Code. Select a completed ZIP, source
folder date, local/network destination root, computer name, project name,
order ID, and order timestamp. The ZIP must contain `order_report.json` and
one or more PNG pictures under `raw`.

The GUI shows ZIP validation and copy progress. Select the overwrite checkbox
to restart copying from the first picture. VS Code also provides a command-
line test configuration, `Upload one ZIP (copy test)`.

To process multiple archives, select an archive root folder and choose
**Process folder**. The app scans its subfolders for ZIP files and processes
them sequentially. Archives must be stored as `<root>\<year>\<month>\<day>\`
and named `order_<order-id>_raw+result_<yyyyMMdd>_<HHmmss>.zip`. The order ID
and timestamp come from the filename; the source date comes from the
year/month/day folders. Each archive is processed independently, and failures
are listed after the remaining archives have been attempted.

The Worker copies pictures to:

`<root>\<computer>\<project>\data\<year>\<month>\<day>\<OrderId>_<timestamp>\`

Progress stays in memory while the GUI runs. Re-running a partially completed
copy resumes from its picture count. Closing the app clears progress; copied
files remain. Select the overwrite checkbox to restart from the first picture.

This milestone does not monitor folders continuously, generate JSON reports,
or run as a Windows service. Target JSON schema and field mapping remain
unresolved. Both GUI and Worker require order ID and timestamp as test inputs.
