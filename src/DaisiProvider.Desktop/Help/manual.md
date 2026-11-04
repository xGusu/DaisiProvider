# Manual upload

Delivers ZIPs by hand, with the same steps as the monitor. Not available while the monitor runs.

- **Copy this ZIP** delivers one ZIP.
- **Process folder** delivers every ZIP below the chosen folder, one after another. Failures are listed at the end; the others continue. The look back and from-date limits do not apply here.
- **Overwrite** copies again from the first file and rewrites the log JSON, even if the order was already delivered. Use it to send an order again. The server treats it as a new delivery.
- **Cancel** stops after the current step. No log JSON is written for the unfinished ZIP.

Without overwrite, an order whose log JSON still exists at the destination is skipped as already delivered. An order whose JSON has left for the cloud is **not** detected here and is sent again. The finished list protects the monitor only.

Each delivered ZIP is added to the finished list, so the monitor will not send it again.

Progress is kept in memory. While the app stays open, a partly copied order continues from its file count. After closing the app it starts from the first file.
