# Deployment, Cache & Dev Mode

This document covers running apps from a network source, the offline cache, and the development workflow (hot reload, dev mode, error reporting). See the [overview](js-runtime.md) for the general architecture.

## Source types

The runtime reads app packages through the `IAppSource` abstraction:

| Source | Description |
|---|---|
| `FileSystemAppSource` | Reads directly from a local directory (the package root). Used for local development and loose packages |
| `RemoteAppSource` | Reads the server manifest and downloads ZIP bundles over HTTP(S) |
| `CachedAppSource` | Wraps `RemoteAppSource` and mirrors the package into a local cache; reads always go to the cache, making the app offline-capable |

`RemoteAppSourceFactory.Create(uri)` picks the right implementation:

- `http://` / `https://` → `CachedAppSource` (with cache root `LocalApplicationData/OldSchoolForms.Ui.Runtime/cache`)
- `file://` → `FileSystemAppSource`
- anything else → `FileSystemAppSource`

## Server contract

A network-hosted app exposes a `manifest.json` at its base URL:

```json
{
  "name": "Company App",
  "version": "2.1.0",
  "entry": "app.js",
  "shell": { "url": "shell.zip", "sha256": "ab12..." },
  "modules": [
    {
      "id": "customers",
      "version": "1.0.0",
      "url": "modules/customers.zip",
      "sha256": "cd34..."
    }
  ]
}
```

| Field | Description |
|---|---|
| `name` / `version` | App identity; the version drives cache invalidation |
| `entry` | Entry script path (inside the shell bundle), optional |
| `shell` | ZIP bundle with the shell layout and shared files (`url`, optional `sha256`) |
| `modules[]` | One ZIP bundle per module (`id`, `url`, optional `version`, optional `sha256`) |

All property lookups are case-insensitive. URLs may be absolute or relative to the manifest URL.

## Offline cache

On startup with a network source, `CachedAppSource.SyncAsync` runs:

1. Fetch `manifest.json`.
2. Compute the target directory: `LocalApplicationData/OldSchoolForms.Ui.Runtime/cache/<app name>/<version>/`.
3. If a `.synced` marker with the same version exists, the cache is considered up to date — **no downloads**.
4. Otherwise the target directory is recreated and each bundle (shell + every module) is downloaded and extracted.
5. If a `sha256` is declared for a bundle, the downloaded bytes are hashed and compared; a mismatch **aborts** with an error (integrity protection).
6. A local `app.json` is generated from the remote manifest (shell → `layouts/shell.json`, modules → `modules/<id>`), and the `.synced` marker is written.

After sync, all reads go to the local cache — the app runs fully **offline**. If the manifest fetch fails but a cache already exists, the runtime logs `Offline: using cache` and continues with the cached version.

Progress is reported to the console (`[sync] ...`).

> On Linux, `LocalApplicationData` is typically `~/.local/share`; on Windows `%LOCALAPPDATA%`.

## Dev mode (`--dev`)

Dev mode changes the runtime behavior to support a fast inner loop:

| Aspect | Production | Dev mode |
|---|---|---|
| Script timeout | 10 s | 120 s |
| Error output | Message only | Message + full stack trace |
| Module import caching | Normal | Cache-busting query (`?v=N`) on every (re)import |
| Hot reload | Off | On (file system watcher) |

### Hot reload

In dev mode with a file-system source, a `HotReloadWatcher` monitors the package directory recursively:

- File **changed / created / renamed** events are collected and **debounced** (300 ms default).
- Paths under `bin/`, `obj/` and `.git/` are ignored.
- When a changed file matches the `index.js` (script) or `layout.json` (layout) of an **open** module, that module is reloaded:
  1. `dispose` is called on the current instance.
  2. All JS event handlers are detached.
  3. The tab is removed.
  4. The module is re-imported (with a fresh cache-busting specifier) and re-initialized.

Only open modules are reloaded; changes to closed modules are ignored until you open them. The watcher never crashes the app — handler exceptions are swallowed.

```bash
# Typical dev loop
dotnet run --project src/OldSchoolForms.Ui.Runtime -- ./my-app --dev
# edit my-app/modules/customers/index.js → the open tab reloads automatically
```

## Sandbox data storage

JavaScript cannot write to the file system directly. Writes go through `ctx.host.writeText` / `readSandboxText` into a per-app sandbox:

```
LocalApplicationData/OldSchoolForms.Ui.Runtime/<sanitized app name>/
```

- Paths are normalized (`\` → `/`, leading `/` stripped) and combined with the sandbox root.
- Any resolved path outside the sandbox throws an unauthorized-access error (path traversal protection).
- Parent directories are created automatically; files are UTF-8.

Read access to the **package itself** (`ctx.host.readText` / `fileExists`) is read-only and package-relative.

## Logging & error channels

| Prefix | Source |
|---|---|
| `[runtime]` | Startup lifecycle (source, manifest, errors) |
| `[sync]` | Cache synchronization progress |
| `[js:log]` / `[js:info]` / `[js:warn]` / `[js:error]` | JS `console.*` calls |
| `[js-error]` | Exceptions from JS execution (events, timers, lifecycle, layout warnings surfaced through the engine) |
| `[shell]` | Shell layout warnings |
| `[hotreload]` | File change / module reload events |
| `[host]` | `ctx.host.log(...)` calls |

Fatal startup errors (missing `app.json`, missing shell layout, unparsable manifests) print `[runtime] Error: ...` and exit with code `1`. Errors inside running JS never kill the app — they are reported and the UI stays alive.
