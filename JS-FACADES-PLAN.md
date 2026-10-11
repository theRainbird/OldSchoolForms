# Plan: JS-Fassaden für alle OldSchoolForms-Controls und Reports

Ziel: Das volle Spektrum der Controls und des Report-Subsystems aus der JS-Runtime (`OldSchoolForms.Ui.Runtime`, Jint) nutzbar machen.

## Beschlüsse

- **Facade-Stil:** Typisierte Facaden (`JsCheckBox : JsControl` usw.) über eine Registry — kein generischer Reflection-Bridge auf die rohe CLR-Oberfläche.
- **Report-Viewer:** `host.openReport(report)` öffnet den `ReportViewer` als Modul-Tab in der Shell.
- **Report-JSON:** Ja — zusätzlich zur programmatischen JS-Erzeugung ein JSON-Format `OldSchoolForms.Report` mit Round-Trip.
- **Vorgehen:** Phasenweise mit Review-Gates (Phase 1+2 zuerst).

## Ist-Zustand

- Runtime vorhanden: `JsEngineHost` (Jint, camelCase-Fallback), `ModuleHost` (Lifecycle), `RuntimeLayoutLoader` (JSON-Layouts), Facaden: `JsControl` (generisch), `JsForm`, `JsEvent`, `JsToolStripItem`, `JsBindingSource`, `JsDataTable`/`JsDataSet`/`JsDataRow` (in `OldSchoolForms.Shared.Scripting`).
- **Gap 1:** `JsControl` expose nur Basis-Properties (Text, Visible, Enabled, X/Y/Width/Height, Dock) und generische Events. Controlspezifisches (`Checked`, `SelectedIndex`, `Items`, `Value/Maximum`, `TabPages`, `Nodes`, Grid-Columns/Rows, Calendar-Appointments, Kanban-Cards, Pivot-Felder, Diagram-Series, …) ist aus JS nicht erreichbar.
- **Gap 2:** Reports sind reines C#: keine Facaden, kein JSON-Format, kein JS-Zugang. `ReportRenderEngine` und `ReportExportPdf` sind headless; `ReportViewer`/`ReportPreviewControl` sind normale Controls.
- Alle Control-Typen sind bereits über `RuntimeLayoutLoader.CreateControl` → `LayoutJsonSerializer.ResolveType` (KnownTypes im Designer-Projekt) resolvbar; `ReportViewer` fehlt dort.

## Architektur

`JsControl` bleibt die gemeinsame Basis (wird entsiegelt; `On`/`OffAll` werden `virtual`, `protected`-Ctor für Subklassen). Für jedes Control mit spezifischem API kommt eine typisierte Facade. Eine neue **`JsFacadeRegistry`** (Runtime/Facade) mappt CLR-Typ → Facade-Factory mit Vererbungssuche und Fallback auf `JsControl`/`JsToolStripItem`. Sie wird zentral verwendet in:

- `ModuleHost.LoadLayoutIntoTab` (Layout-Controls + Strip-Items)
- `createControl` (programmatic JS-Erzeugung)
- `JsControl.Find` / `JsControl.Children`
- `JsForm.Find`

Ein `JsMarshal`-Helper kapselt die Wertkonvertierung: JS ↔ DateTime (ISO-8601), Color (Hex `#RRGGBB`), Enums (Strings), JS-Arrays → `IList`/`List<T>`.

## Phasen

### Phase 1 — Facade-Infrastruktur

- `JsControl` unsealen; `On`/`OffAll` virtual; `protected`-Ctor für Subklassen.
- `JsFacadeRegistry`: `Wrap(host, obj)` mit Type-Vererbungssuche; Fallback `JsControl` bzw. `JsToolStripItem`.
- `JsMarshal`: DateTime/Color/Enum/Array-Konvertierung.
- Wiring in `ModuleHost` (`LoadLayoutIntoTab`, `createControl`), `JsControl.Find/Children`, `JsForm.Find`.
- Tests: Registry-Fallback, camelCase-Zugriff, Event-Vererbung und Teardown (`OffAll`).

### Phase 2 — Basic-Controls

Je Facade + spezifische Events + Tests:

| Facade | Spezifisches API |
|---|---|
| `JsButton` | `performClick()` |
| `JsCheckBox` / `JsRadioButton` | `checked`, Event `checkedchanged` |
| `JsTextBox` / `JsMemoBox` | `selectionStart/Length`, `selectAll()`, `copy/cut/paste`, `wordWrap` (Memo), `useSystemPasswordChar` |
| `JsComboBox` / `JsListBox` | `items` (add/insert/remove/clear), `selectedIndex/SelectedItem/SelectedValue`, `displayMember/valueMember`, `dropDownStyle`, Event `selectedindexchanged` |
| `JsProgressBar` | `minimum/maximum/value`, `orientation`, Event `valuechanged` |
| `JsSpinner` | `active`, `dotCount`, `animationInterval`, `dotColor` |
| `JsFontPicker` | `selectedFontFamily`, `fontSize`, `previewText` |
| `JsLabel` | `textAlign` |
| `JsPictureBox` | `sizeMode` (normal/stretch/zoom/center) |
| `JsDateTimePicker` | `value` (ISO), `minDate/maxDate`, `format`, `customFormat`, `checked`, `showUpDown`, Events `valuechanged`, `checkedchanged` |

### Phase 3 — Container

| Facade | Spezifisches API |
|---|---|
| `JsPanel` / `JsGroupBox` | `borderStyle` |
| `JsSplitPanel` | `orientation`, `splitterDistance`, `panel1MinSize/panel2MinSize`, Events `splitterMoving/splitterMoved` |
| `JsTabControl` | `tabPages`, `selectedIndex`, `selectedTab`, `addTabPage()`, `removeTabPage()`, `tabHeight`, Events `selectedindexchanged`, `tabcloserequested` |
| `JsTreeView` / `JsTreeNode` | `nodes` (add/remove/clear), `selectedNode`, `expandAll/collapseAll/ensureVisible`, Event `afterselect`; TreeNode: `text`, `children`, `isExpanded`, `tag` |
| `JsMenuStrip` / `JsToolStrip` | `addItem(type, props)`, Item-Facaden: `JsToolStripButton` (`checked`, `checkOnClick`, `dropDownItems`), `JsToolStripLabel` (`isLink`), `JsToolStripTextBox` |
| `JsStatusStrip` | `items`, `addLabel()` |

### Phase 4 — Advanced Data

| Facade | Spezifisches API |
|---|---|
| `JsDataGridView` | `columns.add({headerText, dataPropertyName, editType, width, format, readOnly, items, …})`, `rows`, `addRow(…)`, `selectedRowIndex`, `readOnly`, `beginEdit/endEdit`, `clear`, Gruppierung (`addGroupColumn`), Events `cellclick`, `cellvaluechanged`, `cellformatting`, `selectionchanged` |
| `JsCalendarView` | `viewType` (month/week/day), `selectedDate`, `appointments` (add/remove/update: subject, start/end, allDay, categoryColor), `allowEdit`, Events `dateselected`, `appointmentselected`, `viewchanged`; `goToToday/Next/Previous()` |
| `JsKanbanBoardView` | `columns` (name, stateValue, width), Member-Mappings (display/description/state/tag/…), `allowDrop/allowReorder`, Events `cardclick`, `cardmoved` (cancelable) |
| `JsPivotTable` | `fields` (dataPropertyName, usage: row/column/value/filter, aggregation: sum/count/avg/min/max, format), `rebuildMatrix()`, `setFilter/clearFilters`, `expandAll/collapseAll`, Events `cellclick`, `drilldowntoggled`, `selectionchanged` |
| `JsDiagramView` | `chartType` (bar/pie/doughnut/line/area/gauge), `series` aus `[{label, value, color}]`, `xAxis/yAxis` (min/max/title/gridlines), `trendLine`, `showLegend`, `title` |
| `JsHtmlBox` | `html`, `readOnly`, `showToolbar`, Events `linkclick` (cancelable), `contentchanged`, `applyFormat()` |

### Phase 5 — Reports

**JSON-Format:** `ReportJsonSerializer` + `RuntimeReportLoader` im Runtime-Projekt, Format-ID `OldSchoolForms.Report`:
- `pageSetup` (paperSize: A3/A4/A5/Letter/Legal, landscape, margins in cm)
- Bänder (reportHeader/pageHeader/detail/pageFooter/reportFooter) mit Controls
- Gruppen (nested), CrossTab-Felder, Chart-Series
- Wiederverwendung von `JsonValueConverter` (Farben, Fonts, Enums); Round-Trip-Save

**JS-Fassaden:**
- `JsReport`: `name`, `pageSetup` (cm-Zahlen), `dataSource` (nimmt `JsDataTable`/`JsDataSet`-Tabelle/JS-Array of Objects), Bänder via `report.detail.controls.add(...)`, `groups.add(field, {headerText})` mit `header`/`footer`
- Report-Control-Facaden: `JsReportLabel`, `JsReportTextBox` (`dataField`/`expression` mit `{Field}`-Syntax/`format`), `JsReportImage`, `JsReportLine`, `JsReportCheckBox`, `JsReportChart` (chartType, series), `JsReportCrossTab` (fields mit usage/aggregation)
- Headless: `report.exportPdf(path)`, `report.exportPdfBytes()`, `report.pageCount()` via `ReportRenderEngine`/`ReportExportPdf`

**Integration:**
- `ctx.report.create(name)` und `ctx.report.load(path)` (Factory im `ModuleHost`-Context, Lifecycle-gebunden)
- `host.openReport(report)` → neuer Tab mit `ReportViewer` (Toolbar, Print, Export); Tab-Close entfernt den Viewer
- `ReportViewer` via `RuntimeLayoutLoader.Register` registrieren (nicht in Designer-KnownTypes)

### Phase 6 — Sample, i18n, Doku

- `RuntimeExampleApp`: Modul `controls-showcase` (volles Spektrum) + Modul `reports` (programmatisch + JSON-Report + Viewer-Tab)
- `LangRes`-Keys für alle neuen Fehlermeldungen (de/en)
- `AGENTS.md` aktualisieren: Facaden-Abschnitt + Report-JSON-Format

## Validierung (je Phase)

- `dotnet build` + `dotnet test` als Gate
- Facaden-Tests nach Muster `JsEngineHostTests`: JS-Schnipsel ausführen → CLR-State assertieren
- Phase-Gates: Nach Phase 1+2 Review durch den User, dann 3–6
