# Copy rules

Copy rules decide which files are taken out of each ZIP. You define them; they are not derived from the ZIP contents.

Each rule has:

| Field | Meaning |
| --- | --- |
| Enabled | Disabled rules are skipped |
| ZIP folder | Folder inside the ZIP. Empty means the ZIP root. |
| File types | Plain extensions such as `bmp, png`. `*` copies every file in the folder. |
| Positions | Picture positions to copy, such as `101, 124`. Empty copies all positions. |

Defaults: `raw` with `bmp, png, jpg` (enabled) and `result` with `txt` (disabled).

## How matching works

- Only files directly in the folder match, not files in deeper subfolders.
- Upper and lower case do not matter.
- `bmp`, `.bmp` and `*.bmp` mean the same.
- The position is the number before the first `#` in the file name, e.g. `124` in `124#1294#2026-...bmp`. Result files such as `124#1294#..._result.txt` carry it too. When a rule lists positions, a file of another position, or a file without a position, is not copied by that rule.
- If the ZIP has files for the rules but none of the listed positions, the order is finished as **Nothing to copy**: no files, no log JSON, no retry. It goes on the finished list like a delivered order.

All selected files land in one order folder under their original names, except that every `#` becomes `_` (older pictures carry `#`, which is risky in web addresses). If two selected files end up with the same name, for example `101#1.bmp` and `101_1.bmp`, the ZIP fails validation. The same happens when no rule is enabled or no file matches.

## Effect on the log JSON

The JSON gets one `Data` entry per copied **picture** (bmp, png, jpg, jpeg, tif, tiff, gif). Other files, such as `txt`, are copied but get no entry. If no picture is copied, the order fails, because a log JSON needs at least one entry. The copied files stay at the destination in that case.

Rule changes reach the monitor after Stop and Start.
