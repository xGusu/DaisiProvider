# Monitor

The monitor watches the local archive folder and delivers each new ZIP by itself. ZIPs are expected in day folders: `<folder>\yyyy\MM\dd\*.zip`.

## Settings

| Setting | Meaning |
| --- | --- |
| Monitored folder | Local root of the archive |
| Process ZIPs from | ZIPs in day folders before this date are never touched. Default: today minus look back days, so a new setup reads the whole look back range. A date saved earlier stays until you change it. |
| Look back days | Day folders older than this are not read and not shown. Default 30. |
| Scan every (s) | Seconds between scans. Default 60, minimum 5. |

The scan starts at the later of the two dates. Example: today is 4 November, look back is 30 days and the from-date is 1 November. Only the folders from 1 to 4 November are read.

## Start and Stop

**Start** saves all settings and begins scanning. Settings changed on any page apply only after Stop and Start.

If the monitor was running when the app closed, it starts again with the app.

**Stop** cancels the current ZIP. No JSON is written for it, and it runs again after the next Start.

The monitor runs only while this app is open. While it runs, Manual upload is switched off so one ZIP cannot be processed twice.

## Queue

One row per ZIP, in this order:

1. The ZIP being processed now (always on top).
2. Waiting ZIPs, the next one first (oldest day first).
3. Retry scheduled and failed ZIPs.
4. ZIPs finished since Start, newest first. The newest 200 are kept.
5. Finished earlier (grey), newest first: hidden unless **Show finished earlier** is ticked. The newest 200 are kept.

Dates are always written `yyyy-MM-dd`, also in **Process ZIPs from**.

Below the Start button each scan reports what it saw, e.g. `Last scan 10:55:01: 2026-11-04 to 2026-11-09, 3 ZIPs, 3 finished earlier, 0 new`. If the monitor seems idle, this line tells why: no ZIP in the range, or all of them finished earlier.

| State | Meaning |
| --- | --- |
| Waiting | Found, not started yet |
| Waiting for ZIP unlock | The producer still has the file open |
| Validating | Reading the ZIP and checking it |
| Copying | Files column shows copied and total |
| Writing JSON | All files copied, log JSON being written |
| Complete | Delivered |
| Already delivered | The log JSON already existed, order skipped |
| Nothing to copy | No file of the positions in the copy rules, nothing delivered |
| Retry scheduled | Failed, next try at the time in Detail |
| Failed / manual retry | All retries used. Select the row and press **Retry selected**. |
| Finished earlier (grey) | On the finished list before this Start. Detail shows result and time. Not sent again. |

## Retries

After a failure the ZIP is tried three more times, five minutes apart. Then it waits for **Retry selected**. Other ZIPs continue in the meantime. If the destination is unreachable for longer than that, many rows can end up as failed and each needs a retry. After an app restart, failed ZIPs get three new retries.

## No order is sent twice

The server moves delivered orders to the cloud, so the log JSON can disappear from the destination. Checking the destination is therefore not enough. Every finished ZIP is stored by file name in `processed.json`, and ZIPs on that list are skipped, also after a restart. Manual uploads are added too.

If this list cannot be read, the monitor processes nothing and shows the problem at the bottom of the window. It never treats the list as empty.

To send one order again, use Manual upload with the overwrite option.
