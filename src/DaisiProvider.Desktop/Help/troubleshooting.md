# Troubleshooting

Messages appear in the Detail column on the Monitor page, in the status text on Manual upload, or at the bottom of the window.

| Message | What to do |
| --- | --- |
| Fill in on the JSON rules page | Enter Plant and DAISI project, then save |
| Destination folder does not exist | Check the path. Create the root folder; the app does not create it. |
| Destination folder is not writable | The user has no write access to that folder |
| Could not connect | Check server name, user and password with Test connection |
| Windows already has a connection under another user | Close it with `net use \\server\share /delete` or use the same user |
| No answer from the server within 30 s | Server or network down. Check the network. |
| Saved network password cannot be read on this PC | The settings file came from another PC. Enter the password and save. |
| ZIP remained locked | The producer still has the ZIP open. It is retried. |
| ZIP name order ID differs | ZIP name and `order_report.json` belong to different orders. Check the ZIP. |
| Picture has no position | `{position}` is used but the picture name has no number before `#` |
| Finished order list is not valid JSON | `processed.json` is damaged; the monitor stops processing until it is fixed. Keep a copy and repair it, or nothing is protected against sending twice. |
| Monitored folder does not exist | Check the folder on the Monitor page |

## Nothing happens on the Monitor page

- Is the state **Running**? Check the time of the last scan.
- Are the ZIPs in `yyyy\MM\dd` folders below the monitored folder? The month always has two digits: `11`, or `09`, never `9`.
- Is the day inside the scan range? See **Process ZIPs from** and **Look back days**.
- Is the ZIP already on the finished list? Then it does not appear at all.

## An order must be sent again

Use Manual upload with **Overwrite**.

## Many rows failed at once

Usually the destination was unreachable. Fix the cause, press **Test connection** on the Destination page, then retry each row.
