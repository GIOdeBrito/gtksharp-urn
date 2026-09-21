# Release readiness — 1.0

Date: 2026-09-20
Scope: `UrnWrapper` Gtk# desktop app that runs programs inside `bwrap` sandboxes.
Question answered: is the program ready for a 1.0 version?

## Verdict

Functionally close, not yet 1.0-releasable.

By `1.0` we mean: versioned, build-verified, documented, and safe for a stranger to download, run, and trust with `bwrap` sandboxing.

## What is solid (no work needed)

* Core flow works: `Add / Edit / Remove / Search / Run`.
* Table model is sound: `ListStore` (Name=0, LastExecuted=1, Program=2) wrapped in `TreeModelFilter` for search, with `ConvertIterToChildIter` before touching the store.
* Action dispatch is sound: table `ButtonPressEvent` (left-click only), matched by clicked `TreeViewColumn`, rows resolved by case-insensitive Name (unique per Add/Edit validation).
* Security model is coherent and defense-in-depth:
  * `AppStorage.IsRealHomePath` / `TemplateExposesRealHome` guard the config home and program dir.
  * `LoadConfig` auto-migrates legacy/unsafe templates (`--ro-bind / /`, `--sysfs`) and requires the `%optionalArgs%` placeholder.
  * `RunProfile` re-validates everything before spawning.
  * Spawn uses `ProcessStartInfo` with `/bin/sh -c` + `UseShellExecute=false` + `ArgumentList` (no string interpolation).
  * Shell quoting via `QuoteForShell` / `ProfileCommand` keeps metacharacters inert.
* Persistence is robust:
  * Atomic saves (temp file + move), returns `false` on failure instead of throwing.
  * Corrupt files are moved aside to `*.bad.<timestamp>.json` and defaults are returned.
  * Legacy `command -> program + arguments` migration keeps old installs working.
  * JSON uses case-insensitive names, indented output via shared `JsonOptions`.
* Build path is defined: Docker-only via `docker compose up --build`, output lands in host `bin/`. `git status` is clean, no tags yet.

## What blocks 1.0

### 1. No version identity

* `Project/UrnWrapper.csproj` has no `<Version>`, `AssemblyVersion`, or `InformationalVersion`.
* No git tag.
* `MainWindow.WindowTitle` is just `"UrnWrapper"` with no About/version surface.

### 2. No user docs

Only `AGENTS.md` (contributor notes) exists. Missing for a stranger:

* Prerequisites (`bwrap`, Gtk3 runtime, display requirement).
* How to build (`docker compose up --build`) and run (`bin/UrnWrapper`).
* First-run behavior (empty `defaultHome` forces Config first; `items.json`/`config.json` live beside the binary and are auto-created).
* Permissions table meaning (Network, GPU/DRI, X11, Wayland single-socket, Audio, AppImage FUSE, AppImage extract-and-run).
* AppImage modes, including the warning that `--appimage-extract` runs the AppImage unsandboxed once with `HOME=defaultHome` to produce `squashfs-root/AppRun`.
* Security model statement (real `$HOME` is never exposed; what is migrated/refused and why).
* Corrupt-file and legacy-migration behavior.

### 3. No license / changelog

No `LICENSE`, no release notes. Cannot legally be `1.0` without this decision.

### 4. No recorded clean verification

* `bin/` currently holds developer-local state (absolute `/mnt/terafalso/...` home, `/home/gio/Downloads/...` profile, `curl | bash` test profile). This is gitignored local state and will not ship, but it means no fresh-build + first-run smoke test has been recorded as the 1.0 gate.
* Verification is defined as successful `dotnet publish` in the container. The GUI needs a display, so the binary must not be executed headless.

### 5. Small UX bugs worth fixing pre-1.0

* `MainWindow` guards Edit/Remove with static `isDialogOpen` but not Add, so multiple Add dialogs can open.
* Empty Name in Add/Edit silently returns, while empty Program shows an error dialog. Inconsistent.

### 6. Packaging undefined

* Runtime `items.json`/`config.json` beside the binary (`AppContext.BaseDirectory`) works, but needs a documented decision for 1.0 (writable location, updates wiping state, permissions).
* No `.desktop` file for a Gtk desktop app. Decide whether 1.0 needs it or defers to 1.1.

## Plan to 1.0

### Step 1 — Release hygiene

* Add `<Version>1.0.0</Version>` (+ `AssemblyVersion`, `InformationalVersion`) to `Project/UrnWrapper.csproj`.
* Surface version in UI (title suffix or Config/About row).
* Add `LICENSE` + minimal `CHANGELOG` / release notes.
* Tag `v1.0.0` after the verification gate passes.

### Step 2 — Docs (new `README.md`)

* Prerequisites, Docker-only build, run path, first-run Config flow.
* Permissions table, AppImage FUSE vs extract-and-run (with unsandboxed-extraction warning).
* Security invariants (must not be weakened), template placeholders (`%sandboxHome%`, `%programPath%`, `%optionalArgs%`, etc.).
* Runtime data location and recovery behavior.

### Step 3 — Pre-1.0 code fixes (minimal)

* `UI/MainWindow.cs`: apply `isDialogOpen` guard to the Add path.
* `UI/AddItemDialog.cs` / `UI/EditItemDialog.cs`: show an error on empty name, matching the Program validation.
* Keep style: tabs + Allman braces, guard clauses, no `else`/`switch` ladders, explicit `using`s (`ImplicitUsings` off), respect `Nullable enable`.

### Step 4 — Verification gate

* Run `docker compose up --build` from a clean tree and confirm publish succeeds to host `bin/`.
* Do not run `dotnet test` as a gate (no suite) and do not execute the binary headless.
* Manual smoke checklist with a display:
  * Add / Edit / Remove round-trip persists to `items.json`.
  * Search matches Name + Program, case-insensitive.
  * Run with exit code 0 stamps `lastExecuted` and saves; non-zero shows error and does not stamp.
  * Real-`$HOME` refused in Config and in Run (config home, program dir, expanded command).
  * Missing `%optionalArgs%` / legacy `--ro-bind / /` / `--sysfs` migrates to secure defaults.
  * Corrupt `items.json` / `config.json` backs up to `*.bad.<timestamp>.json` and returns defaults.
  * Missing `bwrap` on `PATH` shows a clear error.
  * Missing `defaultHome` / non-existent folder shows a clear error.

## Explicit non-goals for 1.0

* Move to XDG config dirs.
* Automated test suite or CI.
* Process kill/timeout/output-log management for long-running sandboxes.
* `.desktop` installer packaging (unless scope question below says otherwise).

## Open scope questions

1. Which license for 1.0 (MIT / GPL-3.0 / other)?
2. Minimal 1.0 = version + docs + two UX fixes, or also a `.desktop` file?
3. Version string from `csproj` only, or also a `git tag v1.0.0` as the release marker?
