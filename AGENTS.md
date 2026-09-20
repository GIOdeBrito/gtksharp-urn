# AGENTS.md

Gtk# desktop app (`UrnWrapper`): a GUI that runs programs inside `bwrap` sandboxes. No tests, CI, lint, or additional packages.

## Build (Docker only)

Host has no .NET SDK. Build inside the `project-builder` container:

```sh
docker compose up --build
```

What that runs (`entrypoint.sh` inside container, `set -e`):

```sh
dotnet restore
dotnet publish "UrnWrapper.csproj" -c Release -o /urnoutput/bin
chmod -R 755 /urnoutput/bin
chown -R 1000:1000 /urnoutput/bin
```

- `docker-compose.yml` mounts `./Project:/src` and `./bin:/urnoutput/bin`, so the published output lands in host `bin/` (not `Project/publish/`). Do not change the output path.
- Verification = successful publish. There is no test suite (`dotnet test` has nothing to run) and the GUI needs a display, so do not try to execute the binary headless.

## Project layout

- `Project/Program.cs` — entrypoint only (`Main` → `AppStorage.LoadConfig()` → `UI.MainWindow.Build` → `Application.Run`).
- `Project/Models/` — `SandboxProfile` (name, command, lastExecuted, options), `Config` (defaultHome, defaultCommand), `SandboxOptions` (6 permission toggles).
- `Project/Persistence/AppStorage.cs` — the only persistence layer: `items.json`/`config.json` paths, load/save, shared `JsonOptions`, and the bwrap `DefaultCommandTemplate`.
- `Project/UI/` — `MainWindow` (window, table, search, action-click dispatch), `AddItemDialog`/`EditItemDialog`/`RemoveItemDialog`/`ConfigDialog`, `RunProfile` (bwrap execution + template expansion), `SandboxPermissionControls` (permission checkboxes), `ProfileSearch`, `ProfileFormatting`, `StoreColumns` (ListStore indices 0/1/2).
- `Project/UrnWrapper.csproj` — `net8.0`, `GtkSharp 3.24.24.95`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings disable`.
- `bin/`, `Project/bin/`, `Project/obj/`, `Project/publish/` — build artifacts, gitignored. Never commit.

## Architecture notes

- Table = `ListStore` (Name=0, LastExecuted=1, Command=2) wrapped in a `TreeModelFilter` for search. Only Name and LastExecuted are displayed; Command feeds search and the Run action. Action-column clicks must convert the filter iter to the child iter (`ConvertIterToChildIter`) before touching the store.
- Run/Edit/Remove are wired through the table's `ButtonPressEvent` (left-click only), matched by clicked `TreeViewColumn`. A static `isDialogOpen` flag guards against reentrant dialogs; rows are resolved by case-insensitive Name (unique per Add/Edit validation).
- Run flow: `RunProfile.Run` expands the `config.json` `defaultCommand` template (placeholders like `%sandboxHome%`, `%programPath%`, `%optionalArgs%`) and spawns `/bin/sh -c` via `ProcessStartInfo` with `UseShellExecute=false` + `ArgumentList` (no string interpolation). On exit code 0 it stamps `lastExecuted` and saves.
- Security invariants (do not weaken): the sandbox must never expose the real `$HOME`. `AppStorage.IsRealHomePath` / `TemplateExposesRealHome` guard the config home and program dir; `LoadConfig` auto-migrates legacy/unsafe templates (`--ro-bind / /`, `--sysfs`) and requires the `%optionalArgs%` placeholder; `RunProfile` re-validates everything before spawning.
- Adding a new template placeholder means touching both `AppStorage.DefaultCommandTemplate` and `RunProfile.ExpandCommand`.

## Runtime data gotchas

- `items.json` and `config.json` live beside the binary (`AppContext.BaseDirectory`, i.e. host `bin/` after a container build) and are auto-created with defaults if missing. Treat them as local state, not source to edit by hand.
- Loads tolerate corrupt files: the bad file is moved aside to `*.bad.<timestamp>.json` and defaults are returned. Saves are atomic (temp file + move) and return `false` on failure instead of throwing.
- JSON uses case-insensitive property names, indented output (`JsonOptions` in `AppStorage`).

## Constraints

- `ImplicitUsings` is off: add explicit `using`s. `Nullable` is on: respect nullability annotations.
- Code style in file is tabs + Allman braces; match it. Prefer guard clauses; no `else`/`switch` ladders.