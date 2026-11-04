# Copy rules

Copy rules decide which files are taken out of each ZIP. You define them; they are not derived from the ZIP contents.

Each rule has:

| Field | Meaning |
| --- | --- |
| Enabled | Disabled rules are skipped |
| ZIP folder | Folder inside the ZIP. Empty means the ZIP root. |
| File types | Plain extensions such as `bmp, png`. `*` copies every file in the folder. |

Defaults: `raw` with `bmp, png, jpg` (enabled) and `result` with `txt` (disabled).

## How matching works

- Only files directly in the folder match, not files in deeper subfolders.
- Upper and lower case do not matter.
- `bmp`, `.bmp` and `*.bmp` mean the same.

All selected files land in one order folder under their original names. If two selected files have the same name, the ZIP fails validation. The same happens when no rule is enabled or no file matches.

## Effect on the log JSON

The JSON gets one `Data` entry per copied **picture** (bmp, png, jpg, jpeg, tif, tiff, gif). Other files, such as `txt`, are copied but get no entry. If no picture is copied, the order fails, because a log JSON needs at least one entry. The copied files stay at the destination in that case.

Rule changes reach the monitor after Stop and Start.
