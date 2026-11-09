# Overview

DaisiProvider delivers finished production orders to the DAISI share. Each order arrives on this PC as one ZIP file. The app copies the pictures to a local folder or network share and then writes one **log JSON** file for the order.

The log JSON travels with the images. It feeds the Power BI dashboard and makes the images available on the labelling web page. It is also the server's signal to move the order to the cloud, so it is always written last.

## Pages

| Page | What it is for |
| --- | --- |
| Monitor | Watches the archive folder and delivers new ZIPs by itself |
| Manual upload | Delivers one ZIP or a whole folder by hand |
| Destination | Where files go, network login, path layout |
| Copy rules | Which files are taken out of the ZIP |
| JSON rules | Values for the log JSON that are not in the ZIP |

**Save settings** at the bottom right saves all pages at once. While any
page differs from the saved settings the button turns orange and reads
**Save settings \***. Changing a value back to the saved one clears it. A
copy rule cell counts once you leave it.

## First setup

1. **JSON rules:** enter Plant and DAISI project.
2. **Destination:** choose Local PC path or Network share, then check Computer and the preview. For a share press **Test connection**.
3. **Copy rules:** the defaults fit the current archives (`raw` with bmp, png, jpg).
4. **Monitor:** choose the archive folder and press **Start**.

## Where the app keeps its data

| File | Content |
| --- | --- |
| `C:\ProgramData\DaisiProvider\settings.json` | All settings. The network password is encrypted for this PC. |
| `C:\ProgramData\DaisiProvider\processed.json` | List of finished ZIPs, so no order is sent twice |

Press **F1** on any page to open the help topic for that page.
