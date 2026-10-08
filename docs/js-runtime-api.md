# JavaScript API Reference

This document describes the API available to JavaScript code running inside the OldSchoolForms JS Runtime. See the [overview](js-runtime.md) for architecture and CLI.

All facade members are defined in PascalCase on the CLR side but are reachable from JavaScript in camelCase (e.g. `showMessageBox`, `setText`) thanks to the engine's camelCase fallback. Event names and JSON keys are case-insensitive.

## The `FormBase` class

Forms and modules are written as ES6 classes that extend the global `FormBase`. The class is injected into the engine by `JsEngineHost` before any script runs.

```js
export default class MyForm extends FormBase {
    constructor(ctx) {
        super(ctx); // required: stores the context
        // initialization (acts as `init`)
    }

    activate()   { /* called when the tab is selected */ }
    deactivate() { /* called when the tab is left */ }
    dispose()    { /* called when the tab is closed */ }
}
```

- The **constructor acts as `init`** — it runs once when the module is opened.
- `activate` / `deactivate` / `dispose` are lifecycle methods with default no-op implementations; override only what you need. They are invoked through the prototype chain, so inheritance works as expected.
- Always call `super(ctx)` first — it wires up `this.controls`, `this.host`, `this.form`, `this.tab` and `this.app`.

### The context object (`ctx`)

The host passes a context object to the constructor (and to the legacy `init` export):

| Property | Description |
|---|---|
| `ctx.form` | The main window facade ([`JsForm`](#the-jsform-facade)) |
| `ctx.controls` | Named controls of the current layout (see below). Empty object until `loadLayout()` is called in class mode; pre-populated in function mode |
| `ctx.host` | Host services ([`HostServices`](#hostservices-ctxhost)) |
| `ctx.tab` | The module's `TabPage` as a [`JsControl`](#the-jscontrol-facade) facade |
| `ctx.app` | `{ name, version }` from the app manifest |

### `this.loadLayout(path?)`

Loads a layout file into the module's tab and (re)builds `this.controls`. This is the JavaScript equivalent of WinForms' `InitializeComponent`.

```js
this.loadLayout();                  // module's default layout (module.json → "layout")
this.loadLayout("details.json");    // alternative file, relative to the module dir
this.loadLayout("@app/shared/common.json"); // package-root path
this.loadLayout("/shared/common.json");     // leading "/" also resolves to the package root
```

Each call **replaces** `this.controls` and the tab's existing controls, so you can switch layouts at runtime. Returns the new controls object. Throws if called where no layout host is available (e.g. the entry script's own form).

### `this.createControl(typeName, props?)`

Creates a control instance from code — no layout file needed. `typeName` uses the same type names as the layout format (`"Button"`, `"Panel"`, `"TextBox"`, ...). `props` supports the same value types as layout JSON (text, dock, colors, fonts, sizes, ...). A `name` in `props` is additionally registered under `this.controls.<name>`.

```js
export default class CodeUiForm extends FormBase {
    constructor(ctx) {
        super(ctx);
        const root = this.createControl("Panel", { name: "root", dock: "fill" });
        const label = this.createControl("Label", { name: "lbl", text: "Hello", dock: "top" });
        const btn = this.createControl("Button", { name: "btn", text: "Click", dock: "top" });
        btn.on("click", () => this.setStatus("clicked"));
        root.add(label);
        root.add(btn);
        this.tab.add(root);   // or this.form.add(...) for the main window
    }
}
```

`add(...)` works on any container control (Panel, GroupBox, TabPage, ...); nesting is unlimited. An unknown type produces a reported warning and `createControl` returns `undefined` — it does not crash. See `samples/RuntimeExampleApp/modules/codeui` for a running example.

### Convenience methods on `FormBase`

| Method | Description |
|---|---|
| `find(name)` | Shortcut for `this.controls[name]` |
| `setStatus(text)` | Shortcut for `this.host.setStatus(text)` |
| `openModule(id)` | Shortcut for `this.host.openModule(id)` |
| `closeModule(id)` | Shortcut for `this.host.closeModule(id)` |

## Dual auto-detection: class mode vs. function mode

A module may also use the legacy **function contract** instead of a class:

```js
export function init(ctx)  { /* layout already loaded, controls populated */ }
export function activate() { }
export function deactivate() { }
export function dispose() { }
```

The host detects the form automatically:

| Detection | Mode | Layout handling |
|---|---|---|
| `export default class` (constructible) | **Class mode** | The class calls `this.loadLayout()` itself; `this.controls` starts empty |
| Named `init` export (no constructible default) | **Function mode** | The layout is pre-loaded by the host before `init(ctx)` is called |

If a module has no script at all, the layout is still loaded so the tab shows content.

## The `JsControl` facade

`ctx.controls.<name>` gives access to the named controls of the current layout. Every control is wrapped in a `JsControl` facade:

| Member | Type | Description |
|---|---|---|
| `name` | get/set | Name of the control |
| `text` | get/set | Text of the control |
| `visible` | get/set | Visibility |
| `enabled` | get/set | Enabled state |
| `x`, `y` | get/set | Position |
| `width`, `height` | get/set | Size |
| `dock` | get/set | Docking as string: `"none"`, `"left"`, `"right"`, `"top"`, `"bottom"`, `"fill"` |
| `find(name)` | method | Recursively find a child control by name (returns facade or `null`) |
| `children()` | method | Array of direct child facades |
| `add(child)` | method | Add a child facade (target must be a container); returns the child for chaining |
| `focus()` | method | Focus the control (sets it as the container's active control) |
| `invalidate()` | method | Mark the control for repaint |
| `on(event, callback)` | method | Register an event handler (see below) |
| `offAll()` | method | Remove all registered handlers (called automatically on module teardown) |

### Events

`on(event, callback)` supports the following event names (case-insensitive):

| Event | Raised when |
|---|---|
| `click` | Single click |
| `dblclick` | Double click |
| `mousedown` / `mouseup` | Mouse button pressed / released |
| `mousemove` | Mouse moves over the control |
| `mousewheel` | Mouse wheel scrolled |
| `keydown` / `keyup` | Key pressed / released |
| `keypress` | Character key pressed |
| `textchanged` | The control's `text` property changed |
| `gotfocus` / `lostfocus` | Control received / lost focus |

An unknown event name or a non-function callback throws an error.

### The event object (`JsEvent`)

The callback receives a `JsEvent` instance:

| Property | Type | Description |
|---|---|---|
| `type` | string | Event type (e.g. `"click"`) |
| `target` | JsControl | The control that raised the event |
| `x`, `y` | number | Mouse coordinates (mouse events) |
| `button` | string | Pressed mouse button (`"left"`, `"right"`, ... / `"none"`) |
| `delta` | number | Mouse wheel delta |
| `keyCode` | string | Key code as string (keyboard events, e.g. `"Enter"`) |
| `keyChar` | string | Pressed character (`keypress` only) |
| `shift`, `control`, `alt` | boolean | Modifier keys |
| `handled` | get/set boolean | Set to `true` in the callback to cancel further processing of keyboard events |

```js
this.controls.searchBox.on("textchanged", (e) => {
    this.setStatus("Search: " + e.target.text);
});

this.controls.input.on("keydown", (e) => {
    if (e.keyCode === "Escape") {
        e.handled = true; // swallow the key
        this.host.closeModule("search");
    }
});
```

## The `JsForm` facade

`ctx.form` wraps the main window:

| Member | Type | Description |
|---|---|---|
| `title` | get/set | Window title |
| `width`, `height` | get/set | Window size |
| `close()` | method | Close the window |
| `minimize()` / `maximize()` / `restore()` | method | Window state operations |
| `find(name)` | method | Find a control in the whole window by name (recursive) |
| `add(child)` | method | Add a control directly to the form (code-built UI) |
| `onClosing(callback)` | method | Register a closing callback; set `e.handled = true` in the callback to **cancel** the close |
| `offAll()` | method | Remove all registered callbacks |

```js
this.form.onClosing((e) => {
    const r = this.host.showMessageBox("Really quit?", "Confirm", "yesno", "question");
    if (r === "no") e.handled = true; // cancel closing
});
```

## `HostServices` (`ctx.host`)

| Method | Description |
|---|---|
| `showMessageBox(text, caption?, buttons?, icon?)` | Modal dialog. `buttons`: `"ok"` (default), `"okcancel"`, `"yesno"`, `"yesnocancel"`. `icon`: `"none"` (default), `"info"`, `"warning"`, `"error"`, `"question"`. Returns the result as a lowercase string: `"ok"`, `"cancel"`, `"yes"`, `"no"`. `caption` defaults to the app name |
| `setStatus(text)` | Set the status bar text (first `ToolStripStatusLabel` of the shell's `StatusStrip`) |
| `openModule(id)` | Open (or select) a module tab |
| `closeModule(id)` | Close a module tab |
| `readText(path)` | Read a UTF-8 text file from the app package (read-only access) |
| `hasFile(path)` | Check whether a file exists in the package |
| `writeText(path, content)` | Write a UTF-8 text file to the app's **sandbox** folder |
| `readSandboxText(path)` | Read a text file from the sandbox; returns `null` if absent |
| `log(message)` | Write a message to the runtime log (`[host] ...`) |

The sandbox directory is `LocalApplicationData/OldSchoolForms.Ui.Runtime/<app name>/`. Paths are normalized and confined to the sandbox — any attempt to escape it (e.g. `../../`) throws an unauthorized-access error.

## Globals

The engine exposes browser-style globals:

| Global | Description |
|---|---|
| `setTimeout(callback, ms)` | One-shot timer; returns a numeric ID |
| `setInterval(callback, ms)` | Repeating timer; returns a numeric ID |
| `clearTimeout(id)` / `clearInterval(id)` | Cancel a timer |
| `console` | `log` / `info` / `warn` / `error` — output as `[js:<level>] ...` |

Timer semantics:

- Timers are **not** threads. They are collected by a `TimerScheduler` and fired from the UI frame loop (`Platform.OnFrame` → `engine.PumpTimers()`), so all callbacks run deterministically on the UI thread.
- Due timers fire in due-time order, once per frame. A repeating interval's next due time is rescheduled from the current pump (`max(interval, 1 ms)`).
- Negative or `NaN` delays are clamped to `0`.
- A throwing callback is reported to the engine's error channel and does not break the pump.
- Clean up your timers in `deactivate()`/`dispose()` — the engine does not do it for you.

## camelCase fallback

The CLR facades expose PascalCase members (`ShowMessageBox`, `Text`, `Dock`). The engine's member accessor first tries the exact name, then — if the JS name starts with a lowercase letter — retries with an upper-cased first character. Therefore all of these work:

```js
this.host.showMessageBox("Hi");   // → ShowMessageBox
control.text = "Hello";           // → Text
control.dock = "fill";            // → Dock
```

This also means PascalCase access works for members that exist with that exact name; the fallback only kicks in when the direct lookup fails.

## Error handling

- Errors thrown inside JS callbacks (events, timers, lifecycle calls) are caught and routed to the engine's `Error` event, printed as `[js-error] <context>: <message>`. In `--dev` mode the stack trace is included.
- Layout loading problems (unknown types/properties) are reported as warnings, not fatal errors — the rest of the layout still loads.
- The runtime process exits with code `1` on fatal startup errors (missing manifest, missing shell layout, etc.).
