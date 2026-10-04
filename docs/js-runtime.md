# JavaScript Runtime

The **OldSchoolForms.Ui.Runtime** is a client runtime environment that uses **JavaScript (ES6)** as its application language, embedded via [Jint](https://github.com/sebastienros/jint).

A prebuilt shell (menu bar, toolbar, left navigation sidebar, `TabControl` content area) is loaded from a JSON layout file; the application logic — forms, event handlers, navigation — is implemented in JavaScript. App packages can be sourced locally from the file system or over a network (ZIP bundles + offline cache).

> **Related documents:**
> - [JavaScript API Reference](js-runtime-api.md) — `FormBase`, controls, events, host services, globals
> - [App Package Format](js-runtime-packages.md) — `app.json`, `module.json`, shell layout, module resolution
> - [Deployment, Cache & Dev Mode](js-runtime-deployment.md) — network sources, offline cache, hot reload

## Features

- **JavaScript-first development** — no recompilation of the .NET host; apps are pure JSON + ES6 modules.
- **Curated facade, no raw CLR** — JavaScript only sees the stable `JsForm` / `JsControl` / `JsEvent` / `HostServices` API, never the underlying CLR object model.
- **Shared layout format** — layouts use the same `OldSchoolForms.Layout` JSON format as the designer, loaded directly into a live control hierarchy.
- **Module system** — each feature module lives in its own directory and opens in its own tab with a full lifecycle (`init` → `activate` → `deactivate` → `dispose`).
- **Network + offline** — apps can be served from a web server as ZIP bundles with SHA-256 verification and mirrored into a local cache for offline use.
- **Hot reload** — in dev mode, changed scripts and layouts of open modules are automatically re-imported and re-initialized.
- **Deterministic timers** — `setTimeout`/`setInterval` are pumped on the UI thread once per frame; no background threads touch the UI.

## Architecture

```
Program.cs (CLI)
  └─ IAppSource (FileSystem | Remote + Cache)          Sources/
       └─ AppManifest.Load → ModuleDescriptor[]         Modules/
            └─ JsEngineHost (Jint + ES module loader)  Scripting/
                 ├─ AppSourceModuleLoader              (import ./ ../ @app/)
                 └─ TimerScheduler                    (setTimeout/Interval, pumped per frame)
            └─ RuntimeLayoutLoader                     Serialization/ (OldSchoolForms.Layout → Controls)
            └─ ShellBuilder                            Shell/ (Form + Menu/Toolbar/Sidebar/Tabs)
            └─ ModuleHost                              Modules/ (tab lifecycle, init/activate/dispose)
                 └─ HostServices                       Hosting/ (ctx.host: dialogs, status, navigation, sandbox)
            └─ Facade: JsForm / JsControl / JsEvent    Facade/ (JS-friendly wrappers, no raw CLR)
```

Key design points:

- **Timers** do not run on their own threads; they are pumped on the UI thread per frame via `Platform.OnFrame` (`engine.PumpTimers()`).
- **camelCase fallback:** JavaScript accesses members with lowercase names (`foo`), while the CLR facades expose PascalCase members (`Foo`). The engine's member accessor falls back to the PascalCase name automatically.
- **No raw CLR:** JavaScript only sees the curated facade (`JsControl`, `JsForm`, `HostServices`). This keeps the interop surface small and stable.

## Build & Run

```bash
dotnet build src/OldSchoolForms.Ui.Runtime                 # Build the runtime
dotnet test  tests/OldSchoolForms.Ui.Runtime.Tests         # Headless tests
dotnet run --project src/OldSchoolForms.Ui.Runtime -- samples/RuntimeSampleApp
```

> **Headless limitation:** Like the demo app, starting the UI requires an X11/display server. Without a display the process stays stuck in the `Application.Run` loop (window setup blocks). The entire JS pipeline (manifest → engine → shell → module init) runs even without a display, which is why the unit tests are fully headless.

## Command line

```
OldSchoolForms.Ui.Runtime [path] [options]

  path              Path to the app package (directory containing app.json).
  --source <uri>    Load app from a network source (https://...) or file://.
  --dev             Dev mode: no cache, full stack traces, hot reload.
  -h, --help        Show help.
```

Source resolution (priority):

1. Positional path argument
2. `--source <uri>` option
3. `runtime.json` in the current directory (`{ "source": "..." }`)
4. `./app`

A checked-out Git repository (or any loose package directory) can be started directly:

```bash
dotnet run --project src/OldSchoolForms.Ui.Runtime -- /path/to/checkout/samples/RuntimeSampleApp --dev
```

## Quick start

A minimal app package:

```
app/
  app.json                 Manifest
  app.js                   Optional entry script
  layouts/shell.json       Shell layout (OldSchoolForms.Layout)
  shared/util.js           Shared ES modules (imported via @app/shared/util.js)
  modules/
    customers/
      module.json          Module manifest
      layout.json          Module form layout
      index.js             Module script
```

Entry script (`app.js`):

```js
export default class App extends FormBase {
    constructor(ctx) {
        super(ctx); // ctx = { form, host, app }
        this.setStatus("Welcome to " + this.app.name);
        this.openModule("customers");
    }
}
```

Module script (`modules/customers/index.js`):

```js
import { greet } from "@app/shared/util.js";

export default class CustomersForm extends FormBase {
    constructor(ctx) {
        super(ctx); // ctx = { form, controls, host, tab, app }
        this.loadLayout(); // loads layout.json into this.tab, fills this.controls

        this.controls.addButton.on("click", () => {
            const r = this.host.showMessageBox("Really?", "Customers", "yesno", "question");
            this.setStatus("Result: " + r);
        });
    }

    activate()   { /* tab was selected */ }
    deactivate() { /* tab left (clean up timers) */ }
    dispose()    { /* tab closed */ }
}
```

See [js-runtime-api.md](js-runtime-api.md) for the full API and [js-runtime-packages.md](js-runtime-packages.md) for the package format.

## Sample applications

- `samples/RuntimeSampleApp` — customers/orders sample with shared utilities, menu, toolbar, sidebar navigation and status bar.
- `samples/RuntimeExampleApp` — broader examples: counter, dashboard, dialogs, notes (sandbox file access) and a **code-built UI** module (`modules/codeui`) that builds its interface entirely in JavaScript without a layout file.

## Testing

`tests/OldSchoolForms.Ui.Runtime.Tests` (xUnit, headless — Jint needs no display):

| Test class | Covers |
|---|---|
| `ModulePathTests` | Path normalization/resolution (`./`, `../`, `@app/`) |
| `CliOptionsTests` | CLI parsing |
| `RemoteManifestTests` | Server manifest parsing |
| `RuntimeLayoutLoaderTests` | Layout → controls, warnings, TabPages, DataGridView columns |
| `JsEngineHostTests` | ES import, export calls, timers, console marshalling |
| `ModuleHostTests` | Module tab lifecycle against a real `TabControl` (no window) |

## Source layout

All runtime code lives in `src/OldSchoolForms.Ui.Runtime/`:

| Folder | Contents |
|---|---|
| `Sources/` | `IAppSource` abstraction: `FileSystemAppSource`, `RemoteAppSource`, `CachedAppSource` |
| `Modules/` | `AppManifest`, `ModuleDescriptor`, `ModuleRegistry`, `ModuleHost` |
| `Scripting/` | `JsEngineHost` (Jint), `AppSourceModuleLoader`, `ModulePath`, `TimerScheduler` |
| `Serialization/` | `RuntimeLayoutLoader` (layout JSON → live controls) |
| `Shell/` | `ShellBuilder` (main window from shell layout) |
| `Hosting/` | `HostServices` (`ctx.host`) |
| `Facade/` | `JsForm`, `JsControl`, `JsEvent` |
| `Dev/` | `HotReloadWatcher` |
