# OldSchoolForms.Ui.Runtime

Client-Laufzeitumgebung für OldSchoolForms UI mit **JavaScript (ES6)** als Skriptsprache, eingebettet via [Jint](https://github.com/sebastienros/jint).

Eine vorgefertigte Shell (Menüleiste, Toolbar, Navigations-Sidebar links, TabControl-Content-Bereich) wird per JSON-Layout geladen; Funktionsbausteine und Formulare werden in JavaScript implementiert. App-Pakete können lokal vom Dateisystem oder über ein Netzwerk (ZIP-Bundles + Offline-Cache) bezogen werden.

## Build & Run

```bash
dotnet build src/OldSchoolForms.Ui.Runtime                 # Build der Laufzeit
dotnet test  tests/OldSchoolForms.Ui.Runtime.Tests         # Headless-Tests (52)
dotnet run --project src/OldSchoolForms.Ui.Runtime -- samples/RuntimeSampleApp
```

> **Headless-Limitierung:** Wie die Demo benötigt das Starten einen X11-/Display-Server. Ohne Display bleibt der Prozess im `Application.Run`-Loop stehen (Fensteranlage blockiert). Die gesamte JS-Pipeline (Manifest → Engine → Shell → Modul-Init) läuft auch ohne Display.

## Kommandozeile

```
OldSchoolForms.Ui.Runtime [pfad] [optionen]

  pfad              Pfad zum App-Paket (Verzeichnis mit app.json).
  --source <uri>    App aus Netzwerkquelle (https://...) oder file:// laden.
  --dev             Dev-Modus: kein Cache, volle Stacktraces, Hot Reload.
  -h, --help        Hilfe.
```

Quell-Ermittlung (Priorität): positionaler Pfad → `--source` → `runtime.json` (`{"source": "..."}`) → `./app`.

Ein ausgechecktes Git-Repo lässt sich direkt starten:

```bash
dotnet run --project src/OldSchoolForms.Ui.Runtime -- /pfad/zum/checkout/samples/RuntimeSampleApp --dev
```

## Architektur

```
Program.cs (CLI)
  └─ IAppSource (FileSystem | Remote+Cache)        Sources/
       └─ AppManifest.Load → ModuleDescriptor[]     Modules/
            └─ JsEngineHost (Jint + ES-Resolver)    Scripting/
                 ├─ AppSourceModuleLoader           (import ./ ../ @app/)
                 └─ TimerScheduler                 (setTimeout/Interval, pro Frame gepumpt)
            └─ RuntimeLayoutLoader                  Serialization/ (OldSchoolForms.Layout → Controls)
            └─ ShellBuilder                         Shell/ (Form + Menu/Toolbar/Sidebar/Tabs)
            └─ ModuleHost                           Modules/ (Tab-Lebenszyklus, init/activate/dispose)
                 └─ HostServices                    Hosting/ (ctx.host: Dialog, Status, Navigation, Sandbox)
            └─ Facade: JsForm / JsControl / JsEvent Facade/ (JS-freundliche Hülle, kein rohes CLR)
```

- **Timer** laufen nicht in eigenen Threads, sondern werden über `Platform.OnFrame` pro Frame auf dem UI-Thread gepumpt (`engine.PumpTimers()`).
- **camelCase-Fallback:** JS greift mit `foo` zu, die CLR-Fassade expose `Foo` (MemberAccessor-Fallback).
- **Kein rohes CLR:** JS sieht nur die kuratierte Fassade (`JsControl`, `JsForm`, `HostServices`).

## App-Paket

```
app/
  app.json                 Manifest
  app.js                   optionales Einstiegs-Skript (entry)
  layouts/shell.json       Shell-Layout (OldSchoolForms.Layout)
  shared/util.js           gemeinsam genutzte ES-Module (via @app/shared/util.js)
  modules/
    customers/
      module.json          Modul-Manifest
      layout.json          Formular-Layout
      index.js             Modul-Skript
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

Module werden nach `order` (dann `id`) sortiert. Ein Listeneintrag kann auch ein Objekt mit `dir`/`id`/... sein.

### `module.json`

```json
{ "id": "customers", "title": "Kunden", "icon": "people.svg",
  "layout": "layout.json", "script": "index.js", "order": 1 }
```

### Shell-Layout

Das Shell-Layout muss einen `TabControl` mit dem Namen `contentTabs` enthalten (Pflicht). Optional: `MenuStrip`, `ToolStrip`, ein `TreeView` (`navTree`) für die Navigation und eine `StatusStrip` (deren erstes `ToolStripStatusLabel` als Statuszeile dient). Siehe `samples/RuntimeSampleApp/layouts/shell.json`.

## JavaScript-API

### Einstiegs-Skript (`app.js`)

```js
export function init(ctx) {
    // ctx = { form, host, app }
    ctx.host.setStatus("Willkommen bei " + ctx.app.name);
    ctx.host.openModule("customers");
}
```

### Modul-Skript (`modules/<id>/index.js`)

```js
import { greet } from "@app/shared/util.js";

export function init(ctx) {
    // ctx = { form, controls, host, tab, app }
    ctx.controls.addButton.on("click", () => {
        const r = ctx.host.showMessageBox("Wirklich?", "Kunden", "yesno", "question");
        ctx.host.setStatus("Ergebnis: " + r);
    });
    ctx.controls.searchBox.on("textchanged", (e) => {
        ctx.host.setStatus("Suche: " + e.target.text);
    });
}
export function activate()   { /* Tab wurde ausgewählt */ }
export function deactivate() { /* Tab verlassen (Timer aufräumen) */ }
export function dispose()    { /* Tab geschlossen */ }
```

### `ctx.controls.<name>`

Zugriff auf benannte Steuerelemente des Modul-Layouts. `JsControl`-Mitglieder (PascalCase, via camelCase-Fallback erreichbar):

| Mitglied | Beschreibung |
|---|---|
| `name`, `text` | Name / Text |
| `visible`, `enabled` | Sichtbarkeit / Aktivierung |
| `x`, `y`, `width`, `height` | Geometrie |
| `dock` | Docking als String (`"fill"`, `"left"`, ...) |
| `find(name)` | Kind-Steuerelement rekursiv finden |
| `children()` | Kindliste |
| `focus()`, `invalidate()` | Fokus / Neuzeichnen |
| `on(event, cb)` | Event-Handler registrieren |

**Events:** `click`, `dblclick`, `mousedown`, `mouseup`, `mousemove`, `mousewheel`, `keydown`, `keyup`, `keypress`, `textchanged`, `gotfocus`, `lostfocus`. Das Callback-Event (`JsEvent`) liefert `type`, `target`, `x`, `y`, `button`, `delta`, `keyCode`, `keyChar`, `shift`, `control`, `alt` und `handled` (setzbar zum Abbruch bei Key-Events).

### `ctx.host` (HostServices)

| Methode | Beschreibung |
|---|---|
| `showMessageBox(text, caption?, buttons?, icon?)` | Dialog; `buttons`: `ok`/`okcancel`/`yesno`/`yesnocancel`, `icon`: `none`/`info`/`warning`/`error`/`question`; Rückgabe `"ok"`/`"cancel"`/`"yes"`/`"no"` |
| `setStatus(text)` | Statusleiste setzen |
| `openModule(id)` / `closeModule(id)` | Navigation |
| `readText(path)` / `fileExists(path)` | Lesezugriff aufs App-Paket |
| `writeText(path, content)` / `readSandboxText(path)` | Sandbox-Schreiben (`LocalApplicationData/OldSchoolForms.Ui.Runtime/<app>/`) |
| `log(msg)` | Log |

### Globals

`setTimeout`, `setInterval`, `clearTimeout`, `clearInterval`, `console` (`log`/`info`/`warn`/`error`).

## Netzwerk / Offline-Cache

Serververtrag (`manifest.json`):

```json
{
  "name": "Firmen-App", "version": "2.1.0", "entry": "app.js",
  "shell": { "url": "shell.zip", "sha256": "..." },
  "modules": [ { "id": "customers", "version": "1.0.0", "url": "modules/customers.zip", "sha256": "..." } ]
}
```

`CachedAppSource` lädt beim Start das Manifest, vergleicht die Version mit dem Cache (`LocalApplicationData/OldSchoolForms.Ui.Runtime/cache/<app>/<version>`), extrahiert ZIP-Bundles (SHA-256-verifiziert) und legt ein lokales `app.json` ab. Liest danach gegen den lokalen Cache → offline-fähig.

## Hot Reload (`--dev`)

`FileSystemWatcher` auf dem Paketverzeichnis (debounced). Ändert sich `layout.json` oder `index.js` eines **geöffneten** Moduls, wird es disposed und mit cache-busting Specifier (`?v=N`) neu importiert und initialisiert.

## Tests

`tests/OldSchoolForms.Ui.Runtime.Tests` (xUnit, headless — Jint braucht kein Display):

- `ModulePathTests` — Pfadnormalisierung/-auflösung (`./`, `../`, `@app/`)
- `CliOptionsTests` — CLI-Parsing
- `RemoteManifestTests` — Server-Manifest-Parsing
- `RuntimeLayoutLoaderTests` — Layout → Controls, Warnungen, TabPages, DataGridView-Spalten
- `JsEngineHostTests` — ES-Import, Export-Calls, Timer, Console-Marshalling
- `ModuleHostTests` — Modul-Tab-Lebenszyklus gegen echten `TabControl` (ohne Fenster)
