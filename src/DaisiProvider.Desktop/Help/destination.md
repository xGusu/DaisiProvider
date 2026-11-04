# Destination and paths

## Local PC path or network share

- **Local PC path:** a root folder on this PC. Used mainly for tests.
- **Network share:** a UNC path such as `\\server\share`, plus user and password.

No drive letter is needed. Windows logs in to the UNC path directly. The login happens once and stays open for later orders.

Leave the user empty to connect with the Windows account the app runs under. The password is stored encrypted and can be read only on this PC. After moving the settings file to another PC, enter it again.

**Test connection** logs in with the values on screen and checks that the path exists and is writable.

The same root check runs before orders are processed, for local folders too.

## Computer and project

**Computer** is used in the paths, in the `{computer}` tokens and as `Machine.ComputerID` in the log JSON. **Project** is needed only where `{project}` is used.

## Path layout

Two templates define where things go: one for the files folder, one for the log JSON file. They apply to both local and network destinations. Click a token to insert it at the cursor of the box you last used. The preview shows an example order.

| Token | Value |
| --- | --- |
| `{root}` | Destination root or network path |
| `{computer}` | Computer field |
| `{project}` | Project field |
| `{yyyy}` `{MM}` `{dd}` | UTC date of the order start |
| `{OrderId}` | Order ID |
| `{OrderTime}` | Time from the ZIP file name, as `yyyyMMddHHmmss` |

The default layout:

```
{root}\{computer}\Data\{yyyy}\{MM}\{dd}\{OrderId}_{OrderTime}
{root}\{computer}\Logs\{yyyy}\{MM}\{dd}\{OrderId}_{OrderTime}.json
```

Rules:

- A template starts with `{root}`.
- It contains `{OrderId}`; otherwise orders would overwrite each other.
- The log template ends in `.json`.
- `\` and `/` both separate folders.
- Unknown tokens, `..`, empty folder names and invalid characters are refused.
- A computer or project value cannot add folders.
- An empty template uses the default.

**Reset to default** restores both templates.

`{OrderTime}` is the producer's local end time from the ZIP name. The DAISI documentation asks for the UTC start time in this name; using the ZIP time was a project decision.

The folders below `{root}` are created when needed. The server must be able to find the layout you choose, so agree on changes with the DAISI team first.
