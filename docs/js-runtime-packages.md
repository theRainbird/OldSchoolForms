# App Package Format

An OldSchoolForms JS app is a **package**: a directory (or ZIP bundle) containing a manifest, layouts and JavaScript modules. This document describes the package structure and manifest formats. See the [overview](js-runtime.md) and the [JavaScript API](js-runtime-api.md).

All paths inside a package are **relative to the package root** and use forward slashes (`/`).

## Package structure

```
app/
  app.json                 Manifest (required)
  app.js                   Optional entry script
  layouts/
    shell.json             Shell layout (referenced by the manifest)
  shared/
    util.js                Shared ES modules (imported via @app/shared/util.js)
  modules/
    customers/
      module.json          Module manifest (required per module)
      layout.json          Module form layout
      index.js             Module script
    orders/
      module.json
      layout.json
      index.js
```

## `app.json` — the app manifest

```json
{
  "name": "Sample Runtime App",
  "version": "1.0.0",
  "shell": "layouts/shell.json",
  "entry": "app.js",
  "modules": [
    "modules/customers",
    "modules/orders"
  ]
}
```

| Property | Default | Description |
|---|---|---|
| `name` | `"OldSchoolForms App"` | Application name (window title, sandbox folder name, dialog captions) |
| `version` | `"1.0.0"` | Application version (used for remote sync) |
| `shell` | `"layouts/shell.json"` | Relative path of the shell layout |
| `entry` | *(none)* | Relative path of the entry script (optional) |
| `modules` | `[]` | List of feature modules |

All property lookups are **case-insensitive**.

### Module list entries

Each entry in `modules` is either:

- a **string** — the module directory (a `module.json` must exist inside it), or
- an **object** — a full inline descriptor with `dir`, `id`, `title`, `icon`, `layout`, `script`, `order`, `version` (see below).

Modules are **sorted by `order`**, then by `id` (case-insensitive). The sort order determines the navigation order in the sidebar.

A missing or unparsable `app.json` is a fatal startup error.

## `module.json` — the module manifest

```json
{
  "id": "customers",
  "title": "Customers",
  "icon": "people.svg",
  "layout": "layout.json",
  "script": "index.js",
  "order": 1
}
```

| Property | Default | Description |
|---|---|---|
| `id` | *(required)* | Unique module ID used for navigation (`openModule("customers")`) |
| `title` | *(falls back to `id`)* | Display name in the tab and navigation |
| `icon` | *(none)* | Optional icon reference name |
| `layout` | `"layout.json"` | Layout file, relative to the module directory |
| `script` | `"index.js"` | Script file, relative to the module directory |
| `order` | `0` | Sort order in the navigation |
| `version` | *(none)* | Optional version info (for remote synchronization) |

A module manifest without an `id` is rejected. A missing `module.json` in a referenced directory is a fatal error.

## The shell layout

The shell layout is an `OldSchoolForms.Layout` document loaded into the main `Form`. The runtime looks up specific controls by name:

| Control | Name | Required? | Purpose |
|---|---|---|---|
| `TabControl` | `contentTabs` | **Required** | Content area; each open module gets a tab here |
| `TreeView` | `navTree` | Optional | Left navigation sidebar; auto-populated from the module registry, selection opens the module |
| `StatusStrip` | `statusStrip` | Optional | Status bar; its **first** `ToolStripStatusLabel` is used by `setStatus()` |
| `MenuStrip` | `menuStrip` | Optional | Menu bar |
| `ToolStrip` | `toolStrip` | Optional | Toolbar |

For the optional controls, if the named lookup fails the shell builder also falls back to the first control of that type found anywhere in the form. If `contentTabs` is missing, startup fails.

Example shell layout (excerpt from `samples/RuntimeSampleApp/layouts/shell.json`):

```json
{
  "format": "OldSchoolForms.Layout",
  "version": 1,
  "rootType": "Form",
  "rootSize": { "width": 1000, "height": 700 },
  "controls": [
    {
      "type": "MenuStrip",
      "name": "menuStrip",
      "props": { "dock": "Top" },
      "items": [
        {
          "type": "ToolStripMenuItem",
          "props": { "text": "&File" },
          "dropDownItems": [
            { "type": "ToolStripMenuItem", "props": { "text": "Exit" } }
          ]
        }
      ]
    },
    {
      "type": "StatusStrip",
      "name": "statusStrip",
      "props": { "dock": "Bottom" },
      "items": [
        { "type": "ToolStripStatusLabel", "props": { "text": "Ready" } }
      ]
    },
    {
      "type": "Panel",
      "name": "sidebar",
      "props": { "dock": "Left", "size": { "width": 220, "height": 600 } },
      "children": [
        { "type": "TreeView", "name": "navTree", "props": { "dock": "Fill" } }
      ]
    },
    {
      "type": "TabControl",
      "name": "contentTabs",
      "props": { "dock": "Fill" }
    }
  ]
}
```

## Layout document format

Module layouts use the same `OldSchoolForms.Layout` JSON format as the designer (see [Designer.md](Designer.md)). Key structure:

```json
{
  "format": "OldSchoolForms.Layout",
  "version": 1,
  "rootSize": { "width": 800, "height": 600 },
  "controls": [
    {
      "type": "Panel",
      "name": "root",
      "props": { "dock": "fill" },
      "children": [
        { "type": "Label", "name": "lblTitle", "props": { "text": "Customers", "dock": "top" } }
      ]
    }
  ]
}
```

- `format` / `version` — a mismatch produces warnings but does not stop loading.
- `rootSize` — applied to the target container (the tab).
- `controls` — array of control nodes. Each node has `type`, optional `name`, optional `props`, optional `children`.
- `props` values support the same conversions as the designer: strings, numbers, booleans, colors, points, sizes, padding, fonts and enums (case-insensitive strings like `"fill"`, `"top"`).
- Special nodes:
  - `TabPage` children of a `TabControl` are added as tabs.
  - `DataGridView` nodes may carry a `columns` array (`valueType`, `items`, `trueValue`, `falseValue` plus regular column properties).
  - `ToolStrip` / `MenuStrip` nodes carry `items` (with nested `dropDownItems`); `StatusStrip` nodes carry `items` of `ToolStripStatusLabel`.
- Unknown control types and unsupported properties are **skipped with a warning** — the rest of the layout still loads.
- Only named controls (`name` present) end up in `ctx.controls`.

## Module resolution (imports)

ES module imports inside the package are resolved by `AppSourceModuleLoader` / `ModulePath`:

| Specifier form | Resolves to | Example |
|---|---|---|
| `./file.js` | Relative to the importing module's directory | `import x from "./helper.js"` |
| `../file.js` | Up one directory from the importing module | `import x from "../shared/util.js"` |
| `@app/...` | Package root | `import { greet } from "@app/shared/util.js"` |
| `/...` | Package root | `import x from "/shared/util.js"` |
| Bare specifier (`lib.js`) | Package root | `import x from "lib.js"` |

Rules:

- All internal module keys are normalized to forward-slash, package-relative paths (`modules/customers/index.js`).
- `.` and `..` segments are resolved; a path that would escape the package root throws an error.
- `require` is **not** registered — only ES module syntax is available.
- The same `@app/` / leading-`/` rules apply to layout paths passed to `this.loadLayout(...)`.
