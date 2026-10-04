# OldSchoolForms.Ui.Runtime

Client runtime environment for OldSchoolForms UI using **JavaScript (ES6)** as the scripting language, embedded via [Jint](https://github.com/sebastienros/jint).

A prebuilt shell (menu bar, toolbar, left navigation sidebar, TabControl content area) is loaded from a JSON layout; function blocks and forms are implemented in JavaScript. App packages can be sourced locally from the file system or over a network (ZIP bundles + offline cache).

## Build & Run

```bash
dotnet build src/OldSchoolForms.Ui.Runtime                 # Build the runtime
dotnet test  tests/OldSchoolForms.Ui.Runtime.Tests         # Headless tests (52)
dotnet run --project src/OldSchoolForms.Ui.Runtime -- samples/RuntimeSampleApp
```

> **Headless limitation:** Like the demo, starting requires an X11/display server. Without a display the process stays stuck in the `Application.Run` loop (window setup blocks). The entire JS pipeline (manifest → engine → shell → module init) runs even without a display.

## Command line

```
OldSchoolForms.Ui.Runtime [path] [options]

  path              Path to the app package (directory containing app.json).
  --source <uri>    Load app from a network source (https://...) or file://.
  --dev             Dev mode: no cache, full stack traces, hot reload.
  -h, --help        Help.
```

Source resolution (priority): positional path → `--source` → `runtime.json` (`{"source": "..."}`) → `./app`.

A checked-out Git repo can be started directly:

```bash
dotnet run --project src/OldSchoolForms.Ui.Runtime -- /path/to/checkout/samples/RuntimeSampleApp --dev
```

## Architecture

```
Program.cs (CLI)
  └─ IAppSource (FileSystem | Remote+Cache)        Sources/
       └─ AppManifest.Load → ModuleDescriptor[]     Modules/
            └─ JsEngineHost (Jint + ES resolver)    Scripting/
                 ├─ AppSourceModuleLoader           (import ./ ../ @app/)
                 └─ TimerScheduler                 (setTimeout/Interval, pumped per frame)
            └─ RuntimeLayoutLoader                  Serialization/ (OldSchoolForms.Layout → Controls)
            └─ ShellBuilder                         Shell/ (Form + Menu/Toolbar/Sidebar/Tabs)
            └─ ModuleHost                           Modules/ (tab lifecycle, init/activate/dispose)
                 └─ HostServices                    Hosting/ (ctx.host: dialog, status, navigation, sandbox)
            └─ Facade: JsForm / JsControl / JsEvent Facade/ (JS-friendly wrapper, no raw CLR)
```

- **Timers** do not run on their own threads; they are pumped on the UI thread per frame via `Platform.OnFrame` (`engine.PumpTimers()`).
- **camelCase fallback:** JS accesses with `foo`, the CLR facade exposes `Foo` (MemberAccessor fallback).
- **No raw CLR:** JS only sees the curated facade (`JsControl`, `JsForm`, `HostServices`).

## App package

```
app/
  app.json                 Manifest
  app.js                   optional entry script (entry)
  layouts/shell.json       shell layout (OldSchoolForms.Layout)
  shared/util.js           shared ES modules (via @app/shared/util.js)
  modules/
    customers/
      module.json          module manifest
      layout.json          form layout
      index.js             module script
```

### `app.json`

```json
{
  "name": "Sample Runtime App",
  "version": "1.0.0",
  "shell": "layouts/shell.json",
  "entry": "app.js",
  "modules": [ "modules/customers", "modules/orders" ]
}
```

Modules are sorted by `order` (then `id`). A list entry may also be an object with `dir`/`id`/....

### `module.json`

```json
{ "id": "customers", "title": "Customers", "icon": "people.svg",
  "layout": "layout.json", "script": "index.js", "order": 1 }
```

### Shell layout

The shell layout must contain a `TabControl` named `contentTabs` (required). Optional: `MenuStrip`, `ToolStrip`, a `TreeView` (`navTree`) for navigation, and a `StatusStrip` (whose first `ToolStripStatusLabel` serves as the status line). See `samples/RuntimeSampleApp/layouts/shell.json`.

## JavaScript API

Forms are written as ES6 classes that extend the global `FormBase`. The constructor
acts as `init`; `activate`/`deactivate`/`dispose` are lifecycle methods. The class
loads its own layout via `this.loadLayout()` (WinForms `InitializeComponent` style),
which populates `this.controls`.

> **Dual auto-detection:** A module may also use the legacy function contract
> (`export function init/activate/deactivate/dispose`). The host detects the form
> automatically: a constructible `export default class` → class mode; otherwise a
> named `init` export → function mode (layout is pre-loaded in function mode).

### Entry script (`app.js`)

```js
export default class App extends FormBase {
    constructor(ctx) {
        super(ctx); // ctx = { form, host, app }
        this.setStatus("Welcome to " + this.app.name);
        this.openModule("customers");
    }
}
```

### Module script (`modules/<id>/index.js`)

```js
import { greet } from "@app/shared/util.js";

export default class KundenForm extends FormBase {
    constructor(ctx) {
        super(ctx); // ctx = { form, controls, host, tab, app }
        this.loadLayout(); // loads layout.json into this.tab, fills this.controls

        this.controls.addButton.on("click", () => {
            const r = this.host.showMessageBox("Really?", "Customers", "yesno", "question");
            this.setStatus("Result: " + r);
        });
        this.controls.searchBox.on("textchanged", (e) => {
            this.setStatus("Search: " + e.target.text);
        });
    }
    activate()   { /* tab was selected */ }
    deactivate() { /* tab left (clean up timers) */ }
    dispose()    { /* tab closed */ }
}
```

### Closable tabs

Every module tab shows a close (X) button. Clicking it closes the module through the
same path as `ctx.host.closeModule(id)`:

1. The JS `dispose` lifecycle callback runs.
2. All facade event handlers registered via `on(...)` are unwired.
3. All timers (`setTimeout`/`setInterval`) created while the module was the active
   execution scope are cancelled — a lingering `setInterval` cannot keep the closed
   module's JS instance alive.
4. The tab page and its entire control subtree are `Dispose()`d (this also releases
   shared resources such as the cursor-blink driver registration of text carets).

Closing the last tab leaves the shell with an empty content area. Explicit
`clearInterval` in `dispose`/`deactivate` (as in the sample) stays valid as
defense-in-depth but is no longer strictly required.

`this.loadLayout()` loads the module's default layout (`module.json` → `layout`).
`this.loadLayout("anderes.json")` loads an alternative layout file (relative to the
module dir; `@app/...` or a leading `/` resolve to the package root). Each call
replaces `this.controls` and the tab's controls, so it can also switch layouts.

### Layout per Code aufbauen (`createControl` + `add`)

Alternativ zum Laden einer Layout-Datei kann das UI direkt in der ES6-Klasse per
JavaScript erzeugt werden — ohne `layout.json`. `this.createControl(type, props)`
erzeugt eine Control-Instanz (gleiche Typnamen wie im Layout-Format), `add(...)`
ordnet sie einem Container unter. `props` unterstützt dieselben Werttypen wie das
Layout-JSON (Text, Dock, Farbe, Font, ...); ein `name` wird zusätzlich unter
`this.controls.<name>` registriert.

```js
export default class CodeUiForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        const root = this.createControl("Panel", { name: "root", dock: "Fill" });
        const label = this.createControl("Label", { name: "lbl", text: "Hallo", dock: "Top" });
        const btn = this.createControl("Button", { name: "btn", text: "Klick", dock: "Top" });
        btn.on("click", () => this.setStatus("geklickt"));
        root.add(label);
        root.add(btn);
        this.tab.add(root);   // bzw. this.form.add(...) für das Hauptfenster
    }
}
```

`add(...)` funktioniert auf jedem `ContainerControl` (Panel, GroupBox, TabPage, …);
Verschachtelung ist beliebig. Ein unbekannter Typ führt zu einer gemeldeten Warnung
(`createControl` liefert `undefined`), nicht zum Absturz. Siehe
`samples/RuntimeExampleApp/modules/codeui` als laufendes Beispiel.

### `ctx.controls.<name>`

Access to named controls of the module layout. `JsControl` members (PascalCase, reachable via camelCase fallback):

| Member | Description |
|---|---|
| `name`, `text` | Name / text |
| `visible`, `enabled` | Visibility / enabled state |
| `x`, `y`, `width`, `height` | Geometry |
| `dock` | Docking as string (`"fill"`, `"left"`, ...) |
| `find(name)` | Find child control recursively |
| `children()` | Child list |
| `focus()`, `invalidate()` | Focus / repaint |
| `on(event, cb)` | Register event handler |

**Events:** `click`, `dblclick`, `mousedown`, `mouseup`, `mousemove`, `mousewheel`, `keydown`, `keyup`, `keypress`, `textchanged`, `gotfocus`, `lostfocus`. The callback event (`JsEvent`) provides `type`, `target`, `x`, `y`, `button`, `delta`, `keyCode`, `keyChar`, `shift`, `control`, `alt` and `handled` (settable to cancel key events).

### `ctx.host` (HostServices)

| Method | Description |
|---|---|
| `showMessageBox(text, caption?, buttons?, icon?)` | Dialog; `buttons`: `ok`/`okcancel`/`yesno`/`yesnocancel`, `icon`: `none`/`info`/`warning`/`error`/`question`; returns `"ok"`/`"cancel"`/`"yes"`/`"no"` |
| `setStatus(text)` | Set status bar |
| `openModule(id)` / `closeModule(id)` | Navigation |
| `readText(path)` / `fileExists(path)` | Read access to the app package |
| `writeText(path, content)` / `readSandboxText(path)` | Sandbox write (`LocalApplicationData/OldSchoolForms.Ui.Runtime/<app>/`) |
| `log(msg)` | Log |

### Globals

`setTimeout`, `setInterval`, `clearTimeout`, `clearInterval`, `console` (`log`/`info`/`warn`/`error`).
Timers created during a module's lifecycle callbacks or event handlers are owned by
that module and are cancelled automatically when the module closes.

## Network / offline cache

Server contract (`manifest.json`):

```json
{
  "name": "Company App", "version": "2.1.0", "entry": "app.js",
  "shell": { "url": "shell.zip", "sha256": "..." },
  "modules": [ { "id": "customers", "version": "1.0.0", "url": "modules/customers.zip", "sha256": "..." } ]
}
```

On startup, `CachedAppSource` loads the manifest, compares the version with the cache (`LocalApplicationData/OldSchoolForms.Ui.Runtime/cache/<app>/<version>`), extracts ZIP bundles (SHA-256 verified) and drops a local `app.json`. It then reads against the local cache → offline-capable.

## Hot reload (`--dev`)

`FileSystemWatcher` on the package directory (debounced). If the `layout.json` or `index.js` of an **open** module changes, it is disposed and re-imported and re-initialized with a cache-busting specifier (`?v=N`).

## Tests

`tests/OldSchoolForms.Ui.Runtime.Tests` (xUnit, headless — Jint needs no display):

- `ModulePathTests` — path normalization/resolution (`./`, `../`, `@app/`)
- `CliOptionsTests` — CLI parsing
- `RemoteManifestTests` — server manifest parsing
- `RuntimeLayoutLoaderTests` — layout → controls, warnings, TabPages, DataGridView columns
- `JsEngineHostTests` — ES import, export calls, timers, console marshalling
- `ModuleHostTests` — module tab lifecycle against a real `TabControl` (no window)
