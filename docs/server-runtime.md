# Server Runtime

The **OldSchoolForms.Server.Runtime** is the server-side counterpart to the
[JavaScript client runtime](js-runtime.md). It lets you build **client–server
applications** where the business logic runs as **ES6 JavaScript services** on a
server, and the desktop client consumes those services over a binary
**[CoreRemoting](https://github.com/theRainbird/CoreRemoting)** channel — no HTTP,
no ASP.NET.

Data is exchanged as **`DataTable` / `DataSet`** objects, which are ideal for
client–server scenarios because they carry their own schema, row state
(added / modified / deleted) and original values, enabling optimistic
concurrency and change tracking out of the box.

A key feature is **shared ES6 code**: validation and schema logic lives in a
`shared/` folder on the server and can be **downloaded by the client** so the
exact same JavaScript runs on both ends. This shared code is strictly separated
from the **server-only business services** — the client can never reach a service
implementation, only call it through the remote gateway.

> **Related documents:**
> - [JavaScript Runtime](js-runtime.md) — the client runtime this pairs with
> - [JavaScript API Reference](js-runtime-api.md) — `FormBase`, controls, host services
> - [DataGridView](DataGridView.md) — the grid control used to display tables

## Why this design

| Concern | Decision |
| --- | --- |
| Transport | CoreRemoting over raw TCP (WatsonTcp), RSA+AES message encryption. No HTTP/ASP.NET. |
| Data exchange | `System.Data.DataTable` / `DataSet` via NeoBinary DiffGram — full fidelity of row states, original values, primary keys and relations. |
| Service shape | A single generic gateway `IServiceGateway.Invoke(serviceId, method, args)`. No per-service compile-time interfaces on the client. |
| Business logic | ES6 classes/functions executed by Jint **on the server only**. |
| Shared logic | ES6 modules in `shared/`, served to clients over the same gateway, cached locally with SHA-256 verification. |
| Persistence | Pure service concern. The runtime only provides `ctx.data` factories and facades. |

## Architecture

```
                         ┌──────────────────────── Server ────────────────────────┐
                         │  Program.cs (CLI: <packageDir> [--host --port --dev])  │
                         │    └─ ServerRuntime                                     │
                         │         ├─ ServerManifest.Load → server.json             │
                         │         ├─ SharedCodeProvider   (shared/ → manifest+hash)│
                         │         ├─ ServiceRegistry      (instantiates services)  │
                         │         │    └─ JsServiceInstance (Jint engine per svc)  │
                         │         │         ├─ ServerModuleLoader (@shared/ + rel) │
                         │         │         └─ ServiceContext (ctx.log/config/data)│
                         │         ├─ ServiceDispatcher    (SemaphoreSlim per svc)  │
                         │         └─ ServiceGateway : IServiceGateway              │
                         │              └─ RemotingServer (CoreRemoting, NeoBinary) │
                         └────────────────────────────┬───────────────────────────┘
                                                      │  TCP + RSA/AES, DataTable/DataSet
                         ┌────────────────────────────▼───────────────────────────┐
                         │  Client (OldSchoolForms.Ui.Runtime)                      │
                         │    ├─ RemoteGatewayClient  (RemotingClient + proxy)      │
                         │    ├─ SharedCodeCache      (download + SHA-256 + offline)│
                         │    ├─ CompositeModuleLoader (@shared/ → cache, else app) │
                         │    └─ RemoteServices (ctx.remote: invoke / invokeAsync)  │
                         └────────────────────────────────────────────────────────┘
```

Key design points:

- **One Jint engine per service.** Services are isolated for fault containment.
  Because Jint engines are not thread-safe and CoreRemoting delivers calls on
  worker threads, every call is serialized through a per-service
  `SemaphoreSlim` (`ServiceDispatcher`).
- **Facades, never raw CLR.** JavaScript sees `JsDataTable` / `JsDataRow` /
  `JsDataSet` facades, never the underlying ADO.NET objects. The facades are the
  same on client and server, so shared validation code behaves identically on both.
- **Shared code is read-only to the client.** The `SharedCodeProvider` only ever
  serves files from the `shared/` directory (path-traversal guarded, extension
  whitelisted). Business services under `services/` are never exposed.
- **Offline-friendly.** The client caches the shared bundle by version with
  SHA-256 verification and falls back to the cache when the server is unreachable.

## Projects

| Project | Role |
| --- | --- |
| `src/OldSchoolForms.Shared` | Contracts (`IServiceGateway`, DTOs) and the JS facades (`JsDataTable`, `JsDataRow`, `JsDataSet`, `JsMarshal`). Referenced by both client and server. |
| `src/OldSchoolForms.Server.Runtime` | The server executable: manifest, service engine, registry, dispatcher, gateway, shared-code provider. |
| `src/OldSchoolForms.Ui.Runtime` | The client runtime (extended with `Remote/` for the gateway client, shared-code cache and `ctx.remote`). |

## Server package layout

A server package is a directory with a `server.json` manifest:

```
MyServer/
├── server.json
├── services/
│   ├── customers/index.js
│   └── orders/index.js
└── shared/
    ├── validation/customers.js
    └── data/schema.js
```

### `server.json`

```json
{
  "name": "Beispiel-Server",
  "version": "1.0.0",
  "host": "127.0.0.1",
  "port": 9090,
  "encryption": false,
  "services": [
    { "id": "customers", "script": "services/customers/index.js", "lifetime": "singleton", "description": "Kundenstamm" },
    { "id": "orders",    "script": "services/orders/index.js",    "lifetime": "singleton" }
  ],
  "shared": { "dir": "shared", "version": "1.0.0" }
}
```

| Field | Meaning |
| --- | --- |
| `host` / `port` | Bind address and TCP port. |
| `encryption` | RSA key exchange + AES for the channel (`true` recommended in production). |
| `services[].id` | The service id used by clients in `ctx.remote.invoke("<id>", ...)`. |
| `services[].script` | ES6 entry script (relative to the package root). |
| `services[].lifetime` | `singleton` (one instance) or `singlecall` (fresh instance per call). |
| `shared.dir` | Directory of shared ES6 modules served to clients. |
| `shared.version` | Bundle version; clients re-download when it changes. |

### Service scripts

A service is an ES6 class (`export default class`) or a function module. The
constructor / `init` receives a `ctx`:

```js
import { validateCustomerTable } from "@shared/validation/customers.js";

export default class CustomersService {
  constructor(context) {
    this.ctx = context;
  }

  list() {
    // build/return a DataTable via ctx.data factories
    return ctx.data.tableFromRows("Customers", [
      { id: 1, name: "Ada Lovelace", email: "ada@example.com", age: 36 }
    ]);
  }

  save(changes) {
    const problems = validateCustomerTable(changes);   // shared validation
    if (problems.length > 0) return { ok: false, problems };
    // ... merge changes into the store ...
    return { ok: true, table: store };
  }
}
```

#### The service `ctx`

| Member | Description |
| --- | --- |
| `ctx.log.info/warn/error/debug(msg)` | Server-side logging. |
| `ctx.config.appName` / `appVersion` / `serviceId` | Manifest values. |
| `ctx.data.newTable(name)` | Create an empty `JsDataTable`. |
| `ctx.data.newSet(name)` | Create an empty `JsDataSet`. |
| `ctx.data.tableFromRows(name, rows)` | Build a typed table from an array of objects. |
| `ctx.clock.now()` / `utcNow()` / `nowIso()` | Time helpers. |

Services may import shared modules with the `@shared/` prefix (resolved to the
server's `shared/` directory) and relative imports within the package.

## The `JsDataTable` facade

The same facade is used on the client and the server:

| Member | Description |
| --- | --- |
| `name` | Table name (get/set). |
| `columnNames` / `columnCount` | Schema introspection. |
| `rowCount` | Number of rows (includes logically deleted rows). |
| `getRow(i)` | `JsDataRow` facade at index `i`. |
| `addColumn(name, type)` | Add a typed column (`string`/`long`/`int`/`double`/`bool`/`decimal`/`datetime`/`guid`). |
| `add(values)` | Append a row from an array or an object map. |
| `deleteRow(row)` | Logical delete (state preserved for `getChanges()`). |
| `getChanges()` | New table with only added/modified/deleted rows. |
| `acceptChanges()` | Mark all rows unchanged. |
| `merge(other)` | Merge a same-schema table, preserving row states. |
| `toArray()` | JS array of plain row objects. |

`JsDataRow`: `get(col)`, `getOriginal(col)`, `set(col, value)`, `state`
(`"unchanged" | "added" | "modified" | "deleted"`), `toObject()`.

## Data binding with `BindingSource`

For live grids, filtering, sorting and master-detail editing, wrap a `JsDataTable`
(or a `JsDataSet` table) in a **binding source** via `ctx.data.createBindingSource(...)`.
Under the hood this is the framework's `BindingSource` over a `DataView`, so
filtering/sorting are live and the grid stays in sync with the underlying table.

```js
this.table = this.ctx.remote.invoke("customers", "list");
this.bs = this.ctx.data.createBindingSource(this.table);   // or createBindingSource(set, "Orders")

this.bs.sort   = "name ASC";                        // DataView sort
this.bs.filter = "age >= 18 AND name LIKE 'A*'";    // DataView RowFilter syntax

// Two-way bind edit boxes to the CURRENT row's columns:
this.bs.bind(this.controls.nameBox,  "text", "name");
this.bs.bind(this.controls.emailBox, "text", "email");
this.bs.bind(this.controls.ageBox,   "text", "age");

// Live grid bound to the filtered/sorted view (cell edits write back to the row):
this.controls.grid.bindSource(this.bs);

// Navigation (updates all bound controls):
this.bs.moveFirst(); this.bs.movePrevious(); this.bs.moveNext(); this.bs.moveLast();

this.bs.onCurrentChanged(() => this.refresh());
this.bs.onListChanged(() => this.refresh());
```

| `ctx.data.createBindingSource(...)` member | Description |
| --- | --- |
| `filter` (get/set) | `DataView.RowFilter` string (`""` clears). Throws if no view. |
| `sort` (get/set) | Sort expression, e.g. `"name ASC, age DESC"`. |
| `count` / `isEmpty` / `supportsFiltering` | View introspection. |
| `position` (get/set) | Current row index within the view. |
| `current` | `JsDataRow` of the current row (or `null`). |
| `getRow(i)` | `JsDataRow` at view index `i` (filtered/sorted order). |
| `moveFirst/Previous/Next/Last()` | Currency navigation. |
| `addNew()` | Pending new row (commit via `endNew`/`acceptChanges`). |
| `removeAt(i)` / `remove(jsRow)` | Remove from the view/table. |
| `find(col, value)` | View index of the first match (`-1` if none). |
| `bind(control, controlProp, column)` | Two-way bind a control property to a column. |
| `unbind(control, controlProp)` | Remove a binding. |
| `onCurrentChanged(cb)` / `onListChanged(cb)` | JS callbacks on currency/list changes. |

Editing a bound `TextBox` (or a grid cell) writes straight to the underlying
`DataRow`, marking it `modified` — so `getChanges()`/`save()` pick it up with no
extra code. Binding sources are owned by the module and are disposed automatically
when the module closes.

> Note: `rowCount` on the facade counts all physical rows (including logically
> deleted ones); `bs.count` reflects the live view (deleted rows hidden).

## Running the server

```bash
dotnet run --project src/OldSchoolForms.Server.Runtime -- samples/ServerSampleApp
# or with overrides
dotnet run --project src/OldSchoolForms.Server.Runtime -- samples/ServerSampleApp --host 0.0.0.0 --port 9090 --dev
```

Stop with `Ctrl+C`.

## Consuming services from the client

Add a `server` section to the client `app.json`:

```json
{
  "name": "Beispiel-App",
  "server": { "host": "127.0.0.1", "port": 9090, "encryption": false },
  "modules": [ "modules/remote" ]
}
```

At startup the client connects to the gateway and syncs the shared bundle into a
local cache (`%LocalAppData%/OldSchoolForms.Ui.Runtime/shared-cache/<app>/<version>`).
If the server is unreachable, the cached bundle is used (offline fallback).

In a module, `ctx.remote` is available:

```js
import { validateName } from "@shared/validation/customers.js";

export default class RemoteForm extends FormBase {
  constructor(ctx) {
    super(ctx);
    this.loadLayout();
  }

  load() {
    const table = this.ctx.remote.invoke("customers", "list");   // synchronous
    this.controls.grid.bindTable(table);
  }

  loadAsync() {
    this.ctx.remote.invokeAsync("customers", "list")             // Promise
      .then((table) => this.controls.grid.bindTable(table))
      .catch((err) => this.setStatus(err.message));
  }

  save() {
    const result = this.ctx.remote.invoke("customers", "save", [this.table]);
    if (!result.ok) {
      // result.problems: [{ row, column, message }, ...] from SHARED validation
    }
  }
}
```

| `ctx.remote` member | Description |
| --- | --- |
| `invoke(serviceId, method, args?)` | Synchronous call. Throws on failure. Returns marshalled value (`JsDataTable` for tables). |
| `invokeAsync(serviceId, method, args?)` | Returns a `Promise` resolved/rejected on the UI thread (pumped per frame). |
| `getServices()` | Catalog of available services (`{ id, description, lifetime, methods[] }`). |
| `isConnected()` | Connection state. |

Because the client imported the **same** `@shared/validation/customers.js` the
server uses, `validateName` on the client enforces identical rules to the
server-side `save()` — one source of truth, two execution sites.

## Data flow example (optimistic concurrency)

1. Client `invoke("customers", "list")` → server returns a `DataTable` (all rows
   `unchanged`).
2. Client edits rows via the facade (`row.set(...)`, `deleteRow(...)`) → row
   states become `modified` / `deleted`.
3. Client `invoke("customers", "save", [table])` → the **DiffGram** transfer
   preserves the changed states and original values across the wire.
4. Server reads `table.getChanges()`, applies them, validates with shared code,
   and returns the updated table.

## Error handling

`IServiceGateway.Invoke` returns a `RemoteCallResult`:

| `errorCode` | Meaning |
| --- | --- |
| `unknown_service` | No service with that id. |
| `unknown_method` | Service has no such method. |
| `invocation_error` | The service threw. |

`ctx.remote.invoke` throws an `Error` with the message; `invokeAsync` rejects with
an `Error` carrying a `code` property.

## Samples

- `samples/ServerSampleApp/` — a complete server package (customers + orders
  services, shared validation + schema).
- `samples/RuntimeExampleApp/modules/remote/` — the client module that consumes
  it and reuses the shared validation.

Run both to see the full round-trip:

```bash
# terminal 1
dotnet run --project src/OldSchoolForms.Server.Runtime -- samples/ServerSampleApp
# terminal 2
dotnet run --project src/OldSchoolForms.Ui.Runtime -- samples/RuntimeExampleApp
```

## Testing

```bash
dotnet test tests/OldSchoolForms.Server.Runtime.Tests   # manifest, engine, NeoBinary round-trip, gateway over TCP
dotnet test tests/OldSchoolForms.Client.Runtime.Tests   # shared-code sync, @shared resolution, ctx.remote sync/async
```
