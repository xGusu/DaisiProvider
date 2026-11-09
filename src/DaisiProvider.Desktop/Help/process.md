# Process step by step

The same steps run for every ZIP, from the Monitor and from Manual upload.

1. **Wait for the ZIP.** The app waits until the producer has released the file. If it stays locked for too long, the order fails.
2. **Check the settings.** Plant and DAISI project must be filled in. Otherwise nothing starts.
3. **Read the ZIP.** The file name must be `order_<OrderId>_raw+result_<yyyyMMdd>_<HHmmss>.zip`. The copy rules select the files. `order_report.json` is read if the ZIP has one; a report that exists but cannot be read fails the order.
4. **Check the order ID.** With a report, the order ID in the ZIP name must equal `OrderId` in the report. If the positions in the copy rules select no file of this ZIP, the order ends here as **Nothing to copy**: nothing is copied, no log JSON is written and it is not retried.
5. **Build the paths** from the path layout on the Destination page.
6. **Prepare the destination.** A network share is logged in once. The root folder must exist and accept a small test file, which is deleted straight away. The app never creates the root itself, because a missing root usually means a wrong path. A good result is remembered for 5 minutes.
7. **Check for an earlier delivery.** If the log JSON already exists, the order counts as already delivered and is skipped. The overwrite option in Manual upload forces a new delivery.
8. **Check names and models.** Tokens in the DAISI project must be fillable for every picture. If an AI models root is set, each position's `meta.json` is read. A problem here stops the order before anything is copied.
9. **Copy the files** selected by the copy rules into one order folder, under their original names with `#` replaced by `_`.
10. **Write the log JSON, last.** It is written under a temporary name (`.json.tmp`) and then renamed, so the server never sees a half-written file.

## Why the order matters

The server takes an order as soon as it sees the log JSON. So the JSON is written only when every file has arrived, and never after a failure. If copying stops half-way, no JSON exists and the server ignores the incomplete folder.

## After a failure

No log JSON is written. Files already copied stay at the destination. The next attempt continues from where it stopped while the app stays open; after an app restart it starts again from the first file and overwrites.
