# Monitor

The monitor watches the local archive folder and delivers each new ZIP by itself. ZIPs are expected in day folders: `<folder>\yyyy\MM\dd\*.zip`.

## Settings

| Setting | Meaning |
| --- | --- |
| Monitored folder | Local root of the archive |
| Process ZIPs from | ZIPs in day folders before this date are never touched. Default: the day of the first setup. |
| Look back days | Day folders older than this are not read and not shown. Default 30. |
| Scan every (s) | Seconds between scans. Default 60, minimum 5. |

The scan starts at the later of the two dates. Example: today is 4 November, look back is 30 days and the from-date is 1 November. Only the folders from 1 to 4 November are read.

## Start and Stop

**Start** saves all settings and begins scanning. Settings changed on any page apply only after Stop and Start.

If the monitor was running when the app closed, it starts again with the app.

**Stop** cancels the current ZIP. No JSON is written for it, and it runs again after the next Start.

The monitor runs only while this app is open. While it runs, Manual upload is switched off so one ZIP cannot be processed twice.

## Queue

One row per ZIP, newest on top. Finished rows stay until 200 are reached.

| State | Meaning |
| --- | --- |
| Waiting | Found, not started yet |
| Waiting for ZIP unlock | The producer still has the file open |
| Validating | Reading the ZIP and checking it |
| Copying | Files column shows copied and total |
| Writing JSON | All files copied, log JSON being written |
| Complete | Delivered |
| Already delivered | The log JSON already existed, order skipped |
| Retry scheduled | Failed, next try at the time in Detail |
| Failed / manual retry | All retries used. Select the row and press **Retry selected**. |

## Retries

After a failure the ZIP is tried three more times, five minutes apart. Then it waits for **Retry selected**. Other ZIPs continue in the meantime. If the destination is unreachable for longer than that, many rows can end up as failed and each needs a retry. After an app restart, failed ZIPs get three new retries.

## No order is sent twice

The server moves delivered orders to the cloud, so the log JSON can disappear from the destination. Checking the destination is therefore not enough. Every finished ZIP is stored by file name in `processed.json`, and ZIPs on that list are skipped, also after a restart. Manual uploads are added too.

If this list cannot be read, the monitor processes nothing and shows the problem at the bottom of the window. It never treats the list as empty.

To send one order again, use Manual upload with the overwrite option.
