# UrnWrapper 1.0.0

Gtk# desktop app that runs programs inside `bwrap` sandboxes.

Each profile is a name plus a program, optional arguments, and per-profile permission toggles. Running a profile expands a `bwrap` command template and spawns it sandboxed, with the real `$HOME` never exposed.

## Prerequisites

* Linux with `bwrap` on `PATH`.
* Gtk 3 runtime with a display (the GUI cannot run headless).
* Docker only for building (host needs no .NET SDK).

## Build

```sh
docker compose up --build
```

This runs inside the `project-builder` container (`set -e`):

```sh
dotnet restore
dotnet publish "UrnWrapper.csproj" -c Release -o /urnoutput/bin
chmod -R 755 /urnoutput/bin
chown -R 1000:1000 /urnoutput/bin
```

Published output lands in host `bin/` (not `Project/publish/`). Do not change the output path.

## Run

```sh
./bin/UrnWrapper
```

The window title shows the current version (for example `UrnWrapper 1.0.0`).

## First-run setup

1. Open `Config`.
2. Set `Default home` to an isolated folder (it will be bound to `/home/sandbox` inside every sandbox).
3. The folder must exist and must never be the real home. A fresh install ships with an empty home, so `Run` refuses to start until this is set.

`config.json` and `items.json` live beside the binary (`AppContext.BaseDirectory`, i.e. host `bin/` after a container build) and are auto-created with defaults if missing. Treat them as local state. Saves are atomic (temp file + move). A corrupt file is moved aside to `*.bad.<timestamp>.json` and defaults are returned.

## Profiles

* `Add`: name (unique, case-insensitive), program (absolute path or binary name, with `Browse...` helper), optional arguments, permission toggles.
* `Edit`: same fields; unchanged saves are a no-op.
* `Remove`: asks for confirmation first.
* `Run`: left-click the Run column. On exit code 0 the row stamps `Last executed`; non-zero shows an error and does not stamp.
* `Search`: filters by name and program, case-insensitive.

Empty name or empty program is rejected with an error. Duplicate names are rejected. Only one dialog can be open at a time.

## Sandbox permissions

Defaults for new profiles: Network on, GPU on, X11 on, Wayland on, Audio off, AppImage FUSE off, extract-and-run off.

| Toggle | What it does |
| --- | --- |
| Network (`--share-net`) | Share host network inside the sandbox. Off isolates networking. |
| GPU / DRI (`/dev/dri`) | Exposes GPU device nodes for hardware acceleration. |
| X11 (`/tmp/.X11-unix` + `DISPLAY`) | Exposes the X11 socket and `DISPLAY`. |
| Wayland (socket + `WAYLAND_DISPLAY`) | Binds only the single Wayland socket (not the whole runtime dir) and sets `WAYLAND_DISPLAY`. Missing sockets are a safe no-op via `bind-try`. |
| Audio (Pulse/PipeWire) | Binds audio sockets under `XDG_RUNTIME_DIR` and sets `PULSE_SERVER=unix:/run/pulse/native`. |
| AppImage / FUSE (`fusermount`) | Exposes `/dev/fuse` plus distro `fusermount` helpers and `/etc/fuse.conf` for Type2 AppImages. Increases kernel surface; enable only for AppImages. |
| AppImage extract-and-run (no FUSE) | Extracts `squashfs-root` to an isolated cache under the default home and runs `AppRun` inside the sandbox. Suppresses FUSE. Enable only for AppImages. |

Wayland and Audio stay independent: Wayland binds only its socket, Audio binds only its sockets.

## AppImage modes

* `FUSE` mode: needs the AppImage/FUSE toggle. Runs the `.AppImage` directly.
* `Extract-and-run` mode: needs the extract toggle. On first run, UrnWrapper executes the AppImage once **unsandboxed** with `HOME=defaultHome` and `--appimage-extract` into a content-hashed cache dir (`<defaultHome>/.cache/appimage/<sha256>/squashfs-root`), then sandboxes the extracted `AppRun`. Later runs reuse the cache. Extraction failure reports the first 500 chars of output. A stale staging dir (`<key>.staging`) is recreated on the next run.
* If the extracted tree already lives under the default home, its program dir is not double-bound; `/usr/bin` is used as the fallback bind instead.

## Security model (do not weaken)

The sandbox must never expose the real `$HOME`.

* `Default home` must not equal, contain, or sit under the real `$HOME`. A parent of the real home (for example `/home`) is also refused because binding it would expose the home.
* A program inside the real home is refused at run time. Move it outside.
* Templates containing the legacy `--ro-bind / /`, the invalid `--sysfs` flag, or a literal real-home path are treated as insecure and auto-migrated to secure defaults on load. `Run` re-checks all of this before spawning and refuses with an error dialog.
* Templates must hide the filesystem (`--tmpfs /home` and friends) and must contain `%optionalArgs%` and `%programArgs%`. Older templates without them are upgraded to secure defaults.
* Spawning uses `/bin/sh -c` via `ProcessStartInfo` with `UseShellExecute=false` + `ArgumentList` (no string interpolation). Every substituted value is single-quote escaped; program arguments are split shell-word aware and re-quoted so metacharacters stay inert.

## Command template placeholders

Advanced use only. The default template in `config.json` supports:

`%defaultHomeDir%`, `%sandboxHome%` (`/home/sandbox`), `%sandboxConfig%`, `%sandboxCache%`, `%sandboxData%`, `%programPath%`, `%programDir%`, `%programArgs%`, `%optionalArgs%`, plus legacy `%xdgRuntimeDir%`, `%display%`, `%waylandDisplay%`.

Adding a new placeholder means touching both `AppStorage.DefaultCommandTemplate` and `RunProfile.ExpandCommand`. If the template is ever reset, use `Config -> Reset secure defaults`.

## Troubleshooting

* `Set Default home in Config first.` — fresh install default; pick an isolated folder.
* `Default home must never be the real home.` — pick a folder outside `$HOME`.
* `bwrap was not found on PATH.` — install `bubblewrap`.
* `Sandbox exited with code N.` — the sandboxed program failed; no timestamp is stamped.
* `Could not save items.json / config.json.` — check write permissions beside the binary; in-memory state is rolled back.
* `Moved corrupt ... to *.bad.*.json` on stderr — the bad file was preserved; defaults were loaded.
