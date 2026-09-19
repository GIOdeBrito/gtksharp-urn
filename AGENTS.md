# AGENTS.md

Gtk# desktop app (`UrnWrapper`). No tests, CI, lint, or additional packages.

## Build (Docker only)

Host has no .NET SDK. Build inside the `project-builder` container:

```sh
docker compose up --build
```

What that runs (`entrypoint.sh` inside container, `set -e`):

```sh
dotnet restore
dotnet publish "UrnWrapper.csproj" -c Release -o /src/publish
```

- `docker-compose.yml` mounts `./Project:/src`, so container `/src/publish` is host `Project/publish/`. Do not change the output path.
- Verification = successful publish. There is no test suite (`dotnet test` has nothing to run) and the GUI needs a display, so do not try to execute the binary headless.

## Project layout

- `Project/Program.cs` — entrypoint only (`Main` → `UI.MainWindow.Build`).
- `Project/Models/` — `SandboxProfile`, `Config` records.
- `Project/Persistence/AppStorage.cs` — `items.json`/`config.json` paths, load/save, shared `JsonOptions`. The only persistence layer.
- `Project/UI/` — `MainWindow` (window, table, columns, search wiring), `AddItemDialog` (add flow + validation), `ProfileSearch`, `ProfileFormatting`, `StoreColumns` (ListStore indices 0/1/2).
- `Project/UrnWrapper.csproj` — `net8.0`, `GtkSharp 3.24.24.95`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings disable`.
- `Dockerfile` / `docker-compose.yml` / `entrypoint.sh` — build container only.
- `Project/bin/`, `Project/obj/`, `Project/publish/` — build artifacts, gitignored. Never commit.

## Runtime data gotchas

- `items.json` and `config.json` live beside the binary (`AppContext.BaseDirectory`, i.e. `Project/publish/` after a container build) and are auto-created with defaults if missing. Treat them as local state, not source to edit by hand.
- Loads tolerate corrupt files: the bad file is moved aside to `*.bad.<timestamp>.json` and defaults are returned. Saves are atomic (temp file + move) and return `false` on failure instead of throwing.
- `config.json` values are currently reserved for future bwrap template expansion and intentionally unused; `Program.Main` loads it only to ensure the file exists.
- The Run/Edit/Remove cells in the Actions column are visual only (no click handlers) by design in this cleanup scope.
- JSON uses case-insensitive property names, indented output (`JsonOptions` in `AppStorage`).

## Constraints

- `ImplicitUsings` is off: add explicit `using`s. `Nullable` is on: respect nullability annotations.
- Code style in file is tabs + Allman braces; match it. Prefer guard clauses; no `else`/`switch` ladders.
