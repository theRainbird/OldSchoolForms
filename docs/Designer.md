# Form Designer

The OldSchoolForms Form Designer is a visual drag-and-drop designer for creating form layouts. It provides a toolbox, a design surface with selection chrome and snap guides, a property grid for inline editing, undo/redo support, and round-trip code generation via Roslyn.

## Namespace

`OldSchoolForms.Ui.Designer`

Sub-namespaces:
- `OldSchoolForms.Ui.Designer.Services` — Selection, Drag, Snap, Toolbox, Undo
- `OldSchoolForms.Ui.Designer.PropertyGrid` — Property grid with inline editors
- `OldSchoolForms.Ui.Designer.Roslyn` — Code generation and parsing
- `OldSchoolForms.Ui.Designer.Standalone` — Standalone application entry point

## Basic Usage

```csharp
// Launch the standalone designer
Application.Run(new DesignerForm());

// Or embed the design surface in your own host
var surface = new DesignSurface();
surface.BeginDesignForm(); // or BeginDesignUserControl()

// Access services
surface.Selection.Select(designItem);
surface.Undo.Undo();
surface.Toolbox.CreateControl("Button");
```

## Architecture Overview

The designer is built as a service-oriented architecture around the `DesignSurface`. Each responsibility is delegated to a dedicated service class. This keeps the surface focused on coordination and rendering.

```
┌──────────────────────────────────────────────────────────────────┐
│                        DesignerForm                              │
│  ┌──────────┐  ┌─────────────────────────┐  ┌────────────────┐  │
│  │ Toolbox  │  │     DesignSurface       │  │  PropertyGrid  │  │
│  │ Control  │  │                         │  │                │  │
│  │          │  │  ┌───────────────────┐  │  │  ┌──────────┐  │  │
│  │  Drag →  │──│  │   DesignForm or   │  │  │  │ Property │  │  │
│  │          │  │  │ DesignUserControl │  │  │  │  GridRow │  │  │
│  └──────────┘  │  │  ┌─────────────┐  │  │  │  └──────────┘  │  │
│                │  │  │  Controls   │  │  │  │                │  │
│                │  │  └─────────────┘  │  │  └────────────────┘  │
│                │  └───────────────────┘  │                       │
│                └─────────────────────────┘                       │
│                                                                  │
│  Services: SelectionService │ DragService │ SnapService          │
│            UndoService     │ ToolboxService                     │
│                                                                  │
│  Roslyn:   CodeGenerator │ FormParser │ DesignerFileManager     │
└──────────────────────────────────────────────────────────────────┘
```

## Project Structure

| Project | Description |
|---------|-------------|
| `OldSchoolForms.Ui.Designer` | Core designer library (surface, services, adapters, property grid) |
| `OldSchoolForms.Ui.Designer.Roslyn` | Code generation and parsing via Roslyn |
| `OldSchoolForms.Ui.Designer.Standalone` | Standalone application entry point |
| `OldSchoolForms.Ui.Designer.Tests` | Designer unit tests |

## Core Components

### DesignSurface

The `DesignSurface` is a `ContainerControl` that hosts and renders controls in design mode. It provides grid rendering, selection chrome, and mouse interaction by coordinating the various services.

| Property | Type | Description |
|---|---|---|
| `Selection` | `SelectionService` | The selection service |
| `Drag` | `DragService` | The drag/move/resize service |
| `Toolbox` | `ToolboxService` | The toolbox (control palette) |
| `Undo` | `UndoService` | Undo/redo stack |
| `Items` | `IReadOnlyList<DesignItem>` | All design items on the surface |
| `RootControl` | `ContainerControl?` | The root container (DesignForm or DesignUserControl) |
| `RootItem` | `DesignItem?` | The design item wrapping the root control |

| Event | Description |
|---|---|
| `SelectionChanged` | Raised when the selection changes |
| `ContentModified` | Raised when content is added, deleted, moved, or resized |

**Methods:**

| Method | Description |
|---|---|
| `BeginDesignForm()` | Starts designing a new Form — creates a `DesignForm` as root |
| `BeginDesignUserControl()` | Starts designing a new UserControl — creates a `DesignUserControl` as root |

### DesignItem

Wraps a control placed on the design surface. Tracks selection state, parent item, and original bounds for undo.

| Property | Type | Description |
|---|---|---|
| `Control` | `Control` | The underlying control being designed |
| `Selected` | `bool` | Whether this item is currently selected |
| `ParentItem` | `DesignItem?` | The parent design item (null for top-level) |
| `OriginalBounds` | `Rectangle` | Bounds before the current operation (for undo) |

### DesignForm

Simulates a form being designed. Renders a title bar, drop shadow, and hosts child controls in its client area. Unlike a real `Form`, this is a lightweight container that does not create a native window.

| Property | Type | Default | Description |
|---|---|---|---|
| `FormTitle` | `string` | `"Form1"` | Title shown in the simulated title bar |
| `FormClientArea` | `Rectangle` | — | Client area below the title bar where controls are placed |

### DesignUserControl

Simulates a `UserControl` being designed. Renders a bordered client area without a title bar. Suitable for designing reusable composite controls.

## Services

### SelectionService

Manages selection of controls on the design surface. Supports single and multi-selection via Ctrl+Click.

| Property | Type | Description |
|---|---|---|
| `SelectedCount` | `int` | Number of selected items |
| `PrimarySelection` | `DesignItem?` | The first selected item (null if none) |
| `SelectedItems` | `IReadOnlyList<DesignItem>` | All selected items |

| Method | Description |
|---|---|
| `Select(DesignItem)` | Selects a single item, clears others |
| `ToggleSelection(DesignItem)` | Toggles selection (Ctrl+Click) |
| `DeselectAll()` | Clears all selections |
| `IsSelected(DesignItem)` | Checks if an item is selected |
| `SelectAll(IEnumerable<DesignItem>)` | Selects all given items |
| `GetSelectionBounds()` | Gets the bounding rectangle of all selected items |

| Event | Description |
|---|---|
| `SelectionChanged` | Raised when the selected set changes |

### DragService

Handles mouse-driven move and resize operations. Tracks drag state, applies bounds changes in real time, supports reparenting controls to different containers.

| Property | Type | Description |
|---|---|---|
| `IsDragging` | `bool` | Whether a drag is in progress |
| `ActiveHandle` | `ResizeHandle` | The active resize handle |
| `CurrentSnap` | `SnapResult` | Current snap result with guide lines |
| `MinResizeSize` | `Size` | Minimum size when resizing (default 10x10) |

| Method | Description |
|---|---|
| `BeginMove(Point)` | Starts a move operation for selected controls |
| `BeginResize(Point, ResizeHandle)` | Starts a resize operation |
| `Update(Point)` | Updates drag position during mouse move |
| `Complete()` | Completes the drag operation |

| Event | Description |
|---|---|
| `DragStarted` | Raised when a drag begins |
| `DragCompleted` | Raised when a drag ends |
| `DragReparented` | Raised when controls are reparented |

### SnapService

Detects alignment between a dragged control and its siblings. Produces snap offsets and visual guide lines (smart guides) during drag operations.

| Constant | Value | Description |
|---|---|---|
| `SnapThreshold` | 6 | Pixel threshold for triggering a snap |

**Algorithm:**

1. Compare the dragged control's left, center, right edges and top, center, bottom edges against all sibling edges
2. If two edges are within `SnapThreshold` pixels, compute the offset to align them
3. Generate guide lines showing the alignment relationship
4. Apply the snap offset to the drag position

| Type | Description |
|---|---|
| `SnapResult` | Contains `SnapX`, `SnapY` offsets and `GuideLines` list |
| `GuideLine` | Start and end points of a visual alignment guide |

### ToolboxService

Manages the palette of controls available for placement on the design surface. Provides factory methods to create control instances.

| Method | Description |
|---|---|
| `Register(ToolboxItem)` | Registers a new control type |
| `CreateControl(ToolboxItem)` | Creates a control instance from a toolbox item |
| `CreateControl(string)` | Creates a control by type name |

**Registered controls:**

| Category | Controls |
|---|---|
| Basic | Button, Label, TextBox, CheckBox, RadioButton, ComboBox, ListBox, ProgressBar, DateTimePicker, PictureBox, FontPicker, MemoBox, Spinner |
| Containers | Panel, GroupBox, TabControl, SplitPanel, FlowLayoutPanel, MenuStrip, ToolStrip, TableLayoutPanel |
| Advanced | DataGridView, TreeView, CalendarView, HtmlBox, DiagramView, PivotTable, KanbanBoardView, StatusStrip |
| Separators | SeperatorControl |

### UndoService

Provides undo/redo for designer operations. Stores snapshots of control state before each mutation.

| Property | Type | Description |
|---|---|---|
| `CanUndo` | `bool` | Whether undo is available |
| `CanRedo` | `bool` | Whether redo is available |

| Method | Description |
|---|---|
| `PushSnapshot(params Control[])` | Captures bounds/text snapshot before a mutation |
| `PushPropertyChange(Control, string, object?)` | Captures a property change for undo |
| `Undo()` | Reverts the most recent snapshot |
| `Redo()` | Re-applies the most recent undo |
| `Clear()` | Clears all undo/redo history |

**Snapshot types:**

- **Bounds/Text snapshot** — captures `Bounds` and `Text` for move/resize/delete operations
- **Property snapshot** — captures a single property name and value for property grid edits

The undo stack has a configurable maximum depth of 100 entries.

## Design Adapters

The adapter pattern allows controls to expose design-mode interaction without the `DesignSurface` hardcoding specific control types.

### IControlDesignAdapter

Interface for design-mode interaction adapters. Controls with special design-time UI (tab headers, dropdowns, etc.) can provide an adapter.

| Method | Description |
|---|---|
| `TryHitRegion(Point)` | Checks if a point is in an interaction region |
| `OnMouseDown(Point, MouseEventArgs)` | Handles mouse down for the interaction |
| `OnMouseMove(Point, MouseEventArgs)` | Handles mouse move for the interaction |
| `OnMouseUp(Point, MouseEventArgs)` | Handles mouse up for the interaction |
| `OnMouseWheel(Point, MouseEventArgs)` | Handles mouse wheel for the interaction |
| `OnKeyDown(KeyEventArgs)` | Handles key down for the interaction |

### ControlDesignAdapterRegistry

Maps control types to their adapter factories. Used by `DesignSurface` to find adapters without hardcoding control types.

### TabControlDesignAdapter

Design adapter for `TabControl` that enables tab switching via tab header clicks and overflow dropdown navigation while in design mode.

## Property Grid

The property grid displays and edits properties of the currently selected design item. It supports inline editing for common types and dedicated editors for complex properties.

### PropertyGrid

Main property grid control that connects to `SelectionService` and observes selection changes.

| Property | Type | Description |
|---|---|---|
| `TargetControl` | `Control?` | The control being edited |

| Method | Description |
|---|---|
| `FilterByCategory(string?)` | Shows only properties of a category |
| `RefreshValues()` | Re-reads all property values from the target |

| Event | Description |
|---|---|
| `PropertyCommitted` | Raised after a dedicated value editor closes |
| `PropertyValueChanged` | Raised after an inline value changes (carries old/new values) |

**Supported inline editors:**

| Type | Editor Behavior |
|---|---|
| `string` | Text input |
| `int`, `float` | Numeric input |
| `bool` | Checkbox toggle |
| `Color` | Color picker with palette, RGB sliders, and system colors |
| `Point` | Two-field editor (X, Y) |
| `Size` | Two-field editor (Width, Height) |
| `Font` | Font picker |
| `Enum` | Dropdown list |
| `Anchor`, `Dock` | Dropdown selection |

### PropertyService

Uses reflection to enumerate public properties of a control and wraps them in `PropertyDescriptor` instances. Filters out hidden properties (internal framework state) and categorizes the remaining ones.

**Category order:**
1. Darstellung (Appearance)
2. Verhalten (Behavior)
3. Data
4. Layout
5. Design
6. Sonstiges (Other)

### PropertyDescriptor

Wraps `PropertyInfo` for display and editing. Supports value change notification for undo tracking.

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Property name |
| `Category` | `string` | Category for grouping |
| `PropertyType` | `Type` | The CLR type of the property |
| `IsReadOnly` | `bool` | Whether the property can be edited |
| `Editor` | `IValueEditor?` | Dedicated editor (null for inline editing) |

## Code Generation & Parsing

### CodeGenerator

Generates C# code for a `InitializeComponent` method based on controls placed on the `DesignSurface`. Produces a standard .NET Windows Forms-style partial class with:

- Field declarations for each control
- Property assignments (Location, Size, Text, Name, BackColor, etc.)
- Container hierarchy setup
- `SuspendLayout()` / `ResumeLayout()` calls

**Usage:**

```csharp
var generator = new CodeGenerator(surface);
string code = generator.Generate("MyApp", "MainForm");
```

### FormParser

Parses a C# `InitializeComponent` method and reconstructs the control hierarchy on a `DesignSurface`. This enables the full round-trip designer workflow: code → design surface → code.

**Usage:**

```csharp
var parser = new FormParser();
var warnings = parser.Parse(sourceCode, surface);
```

**Type resolution:**

The parser uses a `TypeResolver` delegate to map type names to CLR types. The built-in resolver handles all standard OldSchoolForms controls. Custom resolvers can be provided for third-party or user controls.

### DesignerFileManager

Manages the pair of source files for a designed form:

- **User-code file** (e.g., `MainForm.cs`) — Contains the constructor and user event handlers
- **Designer file** (e.g., `MainForm.Designer.cs`) — Contains the auto-generated `InitializeComponent`

| Method | Description |
|---|---|
| `Load(DesignSurface)` | Loads and parses the designer file onto the surface |
| `Save(DesignSurface)` | Generates and saves the designer file |
| `CreateNew(string, string, string)` | Creates a new file pair from scratch |

The file manager automatically derives the designer file path: `MainForm.cs` → `MainForm.Designer.cs`.

## Standalone Application

### DesignerForm

The main window of the standalone designer application. Hosts:

- **MenuStrip** — File, Edit, View, Format, Layout menus
- **Toolbox Panel** (left, 220px) — `ToolboxControl` with categorized controls
- **Design Surface** (center) — `DesignSurface` with scrollbars
- **Property Panel** (right, 276px) — `PropertyGrid` for the selected control
- **StatusStrip** (bottom) — Shows current position and selection info

### ToolboxControl

Visual toolbox displaying available controls grouped by category. Supports:

- Category filtering
- Mouse hover highlighting
- Click selection and drag initiation

### Entry Point

```csharp
// OldSchoolForms.Ui.Designer.Standalone/Program.cs
Application.Run(new DesignerForm());
```

## Resize Handles

The `ResizeHandle` enum defines 8 resize handles as flags:

| Value | Description |
|---|---|
| `None` | No handle |
| `TopLeft` | Top-left corner |
| `TopCenter` | Top edge |
| `TopRight` | Top-right corner |
| `MiddleLeft` | Left edge |
| `MiddleRight` | Right edge |
| `BottomLeft` | Bottom-left corner |
| `BottomCenter` | Bottom edge |
| `BottomRight` | Bottom-right corner |

Handle size is 7 pixels. The handles are rendered as white squares with a border at each position.

## Interaction Flow

### Placing a New Control

1. User drags a control from the `ToolboxControl`
2. `ToolboxService.CreateControl()` instantiates the control with a default name and size
3. `DesignSurface` tracks the pending drop item and shows a placement preview
4. On mouse-up, the control is added to the `RootControl`'s `Controls` collection and wrapped in a `DesignItem`
5. `UndoService.PushSnapshot()` captures the pre-add state
6. `DesignSurface.ContentModified` is raised

### Moving a Control

1. User clicks on a selected control and drags
2. `DragService.BeginMove()` captures the starting positions of all selected items
3. On each mouse move, `DragService.Update()` applies the delta to all items
4. `SnapService.ComputeSnap()` checks for alignment with sibling controls and applies offsets
5. Guide lines are rendered during the drag
6. On mouse-up, `DragService.Completes()` and `UndoService.PushSnapshot()` records the final positions

### Editing a Property

1. User selects a property in the `PropertyGrid`
2. `PropertyGridRow` renders an inline editor based on the property type
3. User edits the value (e.g., picks a color, types text, selects from dropdown)
4. `PropertyDescriptor.SetValue()` writes the value to the control via reflection
5. `PropertyValueChanged` event fires with old and new values
6. `UndoService.PushPropertyChange()` captures the change for undo
7. `DesignSurface.Invalidate()` triggers a repaint

### Undo/Redo

1. Before any mutation, the affected controls are passed to `UndoService.PushSnapshot()`
2. The service creates `UndoSnapshot` instances capturing bounds, text, and optionally a single property
3. On `Undo()`, the snapshot is popped, current state is pushed to the redo stack, and the snapshot is restored
4. On `Redo()`, the process is reversed

### Round-Trip Code Generation

1. User finishes designing and saves
2. `DesignerFileManager.Save()` calls `CodeGenerator.Generate()` to produce C# code
3. The generated `InitializeComponent` method is written to `MainForm.Designer.cs`
4. When reopening, `DesignerFileManager.Load()` calls `FormParser.Parse()` to reconstruct the hierarchy
5. Controls are instantiated via `TypeResolver`, properties are assigned, and the `DesignSurface` is populated

## Design Patterns

| Pattern | Where Used |
|---|---|
| **Service-oriented** | `DesignSurface` delegates to Selection, Drag, Snap, Undo, Toolbox services |
| **Adapter** | `IControlDesignAdapter` for design-mode interaction with specific controls |
| **Observer** | `SelectionChanged`, `ContentModified`, `PropertyValueChanged` events |
| **Memento** | `UndoSnapshot` captures state for undo/redo |
| **Factory** | `ToolboxService.CreateControl()` creates control instances |
| **Strategy** | `TypeResolver` delegate for custom type resolution in the parser |
| **Template Method** | `ControlDesignAdapter` base class with default no-op behavior |

## Architecture Notes

- The designer reuses the same rendering pipeline as the main framework — all rendering goes through the `Graphics` command-list pattern and `SkiaRenderer`
- `DesignForm` and `DesignUserControl` are lightweight containers that do not create native windows; they render their chrome (title bar, borders, shadows) using the same `Graphics` API
- The property grid uses reflection (`PropertyInfo`) to enumerate and edit properties, avoiding the need for design-time attributes on every control
- The `IControlDesignAdapter` + `ControlDesignAdapterRegistry` pattern ensures the `DesignSurface` can support complex design-time interaction for any control without coupling
- Code generation produces standard .NET partial class patterns, making the output compatible with any IDE or build system that understands `InitializeComponent`
- The designer is marked as "under heavy development and not ready for production use" per the project README
