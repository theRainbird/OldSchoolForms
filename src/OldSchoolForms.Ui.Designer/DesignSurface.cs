using System;
using System.Collections.Generic;
using System.Linq;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Reports;
using OldSchoolForms.Ui.Designer.Services;
using OldSchoolForms.Ui.Reports;
using OldSchoolForms.Ui.Theming;
using Graphics = OldSchoolForms.Ui.Rendering.Graphics;

namespace OldSchoolForms.Ui.Designer;

/// <summary>
/// The design surface is a <see cref="ContainerControl"/> that hosts and renders
/// controls in design mode. It provides grid rendering, selection chrome,
/// and mouse interaction (selection, move, resize) by coordinating
/// <see cref="SelectionService"/>, <see cref="DragService"/>, and other services.
/// </summary>
public class DesignSurface : ContainerControl
{
    private readonly List<DesignItem> _designItems = new();
    private readonly SelectionService _selectionService;
    private readonly DragService _dragService;
    private readonly ToolboxService _toolboxService;
    private readonly UndoService _undoService;

    private DesignItem? _rootItem;
    private ContainerControl? _rootControl;
    private Point _rootOffset = Point.Empty;

    internal readonly ScrollBarEngine _vscroll = new();
    internal readonly ScrollBarEngine _hscroll = new();
    private Point _scrollOffset = Point.Empty;
    private ScrollBarContext? _scrollBarContext;

    private Point _lastMouseDown;
    private bool _hasMouseMovedSinceDown;
    private bool _leftButtonPressed;
    private ResizeHandle _pendingResizeHandle = ResizeHandle.None;

    private ToolboxItem? _pendingDropItem;

    private readonly ControlDesignAdapterRegistry _adapterRegistry = new();
    private IControlDesignAdapter? _activeAdapter;

    private const int GridSize = 8;
    private bool _showGrid = true;
    private bool _snapToGrid = true;

    /// <summary>
    /// Raised when the selection on the design surface changes.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Raised when the design surface content is modified (add, delete, move, resize).
    /// </summary>
    public event EventHandler? ContentModified;

    /// <summary>
    /// Gets the selection service for this design surface.
    /// </summary>
    public SelectionService Selection => _selectionService;

    /// <summary>
    /// Gets the drag service for this design surface.
    /// </summary>
    public DragService Drag => _dragService;

    /// <summary>
    /// Gets the toolbox service for this design surface.
    /// </summary>
    public ToolboxService Toolbox => _toolboxService;

    /// <summary>
    /// Gets the undo service for this design surface.
    /// </summary>
    public UndoService Undo => _undoService;

    /// <summary>
    /// Gets the list of all design items on the surface.
    /// </summary>
    public IReadOnlyList<DesignItem> Items => _designItems.AsReadOnly();

    /// <summary>
    /// Gets the root control that hosts the designed content — a <see cref="DesignForm"/>
    /// or <see cref="DesignUserControl"/>. This is the container that top-level controls
    /// are parented to. Null until <see cref="BeginDesignForm"/> or
    /// <see cref="BeginDesignUserControl"/> is called.
    /// </summary>
    public ContainerControl? RootControl => _rootControl;

    /// <summary>
    /// Gets the design item wrapping <see cref="RootControl"/>, or null before a design is begun.
    /// </summary>
    public DesignItem? RootItem => _rootItem;

    /// <summary>
    /// Gets or sets the offset of the root control within the surface. Controls placed on the
    /// surface are positioned relative to this offset (and the root's render offset) so that a
    /// click lands under the cursor inside the root's client area.
    /// </summary>
    public Point RootOffset
    {
        get => _rootOffset;
        set { _rootOffset = value; Invalidate(); }
    }

    /// <summary>
    /// Gets or sets whether the alignment grid is visible.
    /// </summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set { _showGrid = value; Invalidate(); }
    }

    /// <summary>
    /// Gets or sets whether controls snap to the alignment grid when moved or resized.
    /// </summary>
    public bool SnapToGrid
    {
        get => _snapToGrid;
        set => _snapToGrid = value;
    }

    /// <summary>
    /// Gets whether the design surface is waiting for a drop (from toolbox).
    /// </summary>
    public bool IsPendingDrop => _pendingDropItem != null;

    /// <summary>
    /// Begins an external drop operation. The surface enters a mode where the next
    /// left-click will create a control of the given toolbox item at the click position.
    /// </summary>
    /// <param name="item">The toolbox item to place.</param>
    public void BeginExternalDrop(ToolboxItem item)
    {
        _pendingDropItem = item ?? throw new ArgumentNullException(nameof(item));
        var form = FindForm();
        if (form != null) form.Cursor = SystemCursorType.Crosshair;
    }

    /// <summary>
    /// Cancels a pending external drop operation.
    /// </summary>
    public void CancelExternalDrop()
    {
        _pendingDropItem = null;
        var form = FindForm();
        if (form != null) form.Cursor = null;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DesignSurface"/>.
    /// </summary>
    public DesignSurface()
    {
        _selectionService = new SelectionService();
        _dragService = new DragService(this, _selectionService, new SnapService());
        _toolboxService = new ToolboxService();
        _undoService = new UndoService();

        _selectionService.SelectionChanged += (s, e) =>
        {
            SelectionChanged?.Invoke(this, e);
            Invalidate();
        };

        _dragService.DragCompleted += (s, e) =>
        {
            ClearResizeCursor();
            if (RootControl is DesignReport completedRoot)
                completedRoot.SyncControlProxies();
            ContentModified?.Invoke(this, e);
        };

        _dragService.DragReparented += (_, _) =>
        {
            ContentModified?.Invoke(this, EventArgs.Empty);
            Invalidate();
        };

        BackColor = Color.White;

        _dragService.DragStarted += (_, _) => Invalidate();

        _vscroll.Orientation = ScrollBarEngine.ScrollBarOrientation.Vertical;
        _vscroll.ScrollBarSize = ScrollBarEngine.DefaultScrollBarSize;
        _vscroll.SmallChange = GridSize;
        _vscroll.LargeChange = GridSize * 5;
        _vscroll.Scroll += (_, _) => Invalidate();

        _hscroll.Orientation = ScrollBarEngine.ScrollBarOrientation.Horizontal;
        _hscroll.ScrollBarSize = ScrollBarEngine.DefaultScrollBarSize;
        _hscroll.SmallChange = GridSize;
        _hscroll.LargeChange = GridSize * 5;
        _hscroll.Scroll += (_, _) => Invalidate();

        // A surface is immediately usable: start with an empty design form as the root.
        BeginDesignForm();

        _adapterRegistry.Register<TabControl>(c => new TabControlDesignAdapter(c));
        _adapterRegistry.Register<ToolStrip>(c =>
        {
            var adapter = new ToolStripDesignAdapter(c, this);
            adapter.SelectedItemChanged += (_, _) => AdapterSelectionChanged?.Invoke(this, EventArgs.Empty);
            return adapter;
        });
        _adapterRegistry.Register<MenuStrip>(c =>
        {
            var adapter = new MenuStripDesignAdapter(c, this);
            adapter.SelectedItemChanged += (_, _) => AdapterSelectionChanged?.Invoke(this, EventArgs.Empty);
            return adapter;
        });
        _adapterRegistry.Register<StatusStrip>(c =>
        {
            var adapter = new StatusStripDesignAdapter(c, this);
            adapter.SelectedItemChanged += (_, _) => AdapterSelectionChanged?.Invoke(this, EventArgs.Empty);
            return adapter;
        });
        _adapterRegistry.Register<DesignReport>(c => new ReportRootAdapter(c, this));
        _adapterRegistry.Register<DesignBand>(c => new BandDesignAdapter(c, this));
    }

    /// <summary>
    /// Raised when a design adapter changes its internal selection (for example a selected
    /// tool strip item), so the property grid can rebind.
    /// </summary>
    public event EventHandler? AdapterSelectionChanged;

    /// <summary>
    /// Gets the adapter-internal selected value of the primary selection (for example a
    /// selected tool strip item), or <see langword="null"/> if none.
    /// </summary>
    public object? AdapterSelectedValue
    {
        get
        {
            var control = _selectionService.PrimarySelection?.Control;
            if (control == null) return null;
            if (control is DesignReportControl reportProxy)
                return reportProxy.Wrapped;
            return _adapterRegistry.GetAdapter(control)?.GetSelectedValue();
        }
    }

    /// <summary>
    /// Raises the <see cref="ContentModified"/> event. Used by design adapters after they
    /// mutate their control's state (for example adding or removing a tool strip item).
    /// </summary>
    internal void NotifyContentModified() => ContentModified?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Begins designing a form. The current content is cleared and a fresh
    /// <see cref="DesignForm"/> is installed as the surface root. Controls added
    /// afterwards are parented to this form and placed in its client area.
    /// </summary>
    public void BeginDesignForm()
    {
        SetRoot(new DesignForm { Location = _rootOffset });
    }

    /// <summary>
    /// Begins designing a user control. The current content is cleared and a fresh
    /// <see cref="DesignUserControl"/> is installed as the surface root. Controls added
    /// afterwards are parented to this control and placed across its full area.
    /// </summary>
    public void BeginDesignUserControl()
    {
        SetRoot(new DesignUserControl { Location = _rootOffset });
    }

    /// <summary>
    /// Begins designing a report. The current content is cleared and a
    /// <see cref="DesignReport"/> root wrapping the given report model is installed.
    /// The root renders the page sheet, margins, and band layout; its child space is
    /// the printable area where band content is placed.
    /// </summary>
    /// <param name="report">The report model to design. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="report"/> is null.</exception>
    public void BeginDesignReport(Report report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        var root = new DesignReport(report) { Location = _rootOffset };
        SetRoot(root);
        root.AttachSurface(this);
    }

    private void SetRoot(ContainerControl root)
    {
        ReportDesignContext.Current = root as DesignReport;

        // Remove every existing item and its underlying control.
        _selectionService.DeselectAll();
        foreach (var item in _designItems.ToArray())
        {
            item.Control.Parent?.Controls.Remove(item.Control);
            _designItems.Remove(item);
        }
        _undoService.Clear();

        Controls.Clear();

        _rootControl = root;
        Controls.Add(root);

        _rootItem = new DesignItem(root);
        _designItems.Insert(0, _rootItem);

        _vscroll.Value = 0;
        _hscroll.Value = 0;
        _scrollOffset = Point.Empty;

        ContentModified?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>
    /// Adds a control to the design surface at the specified location. The control is
    /// wrapped in a <see cref="DesignItem"/> and parented to the surface root
    /// (<see cref="RootControl"/>), placing it in the root's client area.
    /// </summary>
    /// <param name="control">The control to add.</param>
    /// <param name="location">The location on the design surface.</param>
    /// <returns>The newly created DesignItem.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no design has been begun.</exception>
    public DesignItem AddControl(Control control, Point location)
    {
        if (_rootControl == null)
            throw new InvalidOperationException(LangRes.GetString("Designer_NoDesignStarted"));
        return RegisterItem(control, parent: _rootControl, ToRootLocal(location));
    }

    /// <summary>
    /// Adds a control as a child of the specified container control.
    /// The control is wrapped in a <see cref="DesignItem"/> and tracked on the surface regardless
    /// of which container it belongs to, so selection, move and resize work uniformly.
    /// Works with any <see cref="ContainerControl"/> derived type, including future ones.
    /// </summary>
    /// <param name="control">The control to add.</param>
    /// <param name="parent">The container control to add the control to.</param>
    /// <param name="localPoint">The location in the container's local coordinate space.</param>
    /// <returns>The newly created DesignItem.</returns>
    /// <exception cref="ArgumentNullException">Thrown when control or parent is null.</exception>
    public DesignItem AddControl(Control control, ContainerControl parent, Point localPoint)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        return RegisterItem(control, parent, localPoint);
    }

    private DesignItem RegisterItem(Control control, ContainerControl? parent, Point localPoint)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));

        // Surface-level controls are parented to the surface so they render through
        // the normal control tree; controls given an explicit container are parented there.
        // Location is assigned before parenting so an auto-layout container (e.g.
        // FlowLayoutPanel) can override it during the PerformLayout triggered by the
        // parent assignment; assigning it afterwards would clobber the computed layout.
        control.Location = SnapToGrid ? SnapPoint(localPoint) : localPoint;
        control.Parent = parent ?? this;

        var item = new DesignItem(control);
        item.ParentItem = parent != null ? FindItem(parent) : null;
        _designItems.Add(item);

        if (control is DesignReportControl && RootControl is DesignReport reportRoot)
            reportRoot.SyncControlProxies();

        var stillRegistered = FindItem(control) != null;

        // Syncing proxies can orphan a freshly placed proxy (for example when the drop
        // landed outside any band, so the cleanup detaches it from the model and the
        // tree). Only select the item when it is still a registered design item;
        // selecting a deregistered phantom would leave the property grid bound to a
        // control that is no longer visible on the surface.
        if (stillRegistered)
            _selectionService.Select(item);
        _undoService.Clear();
        ContentModified?.Invoke(this, EventArgs.Empty);
        Invalidate();
        return item;
    }

    /// <summary>
    /// Converts a point expressed in surface coordinates into the root's client coordinate
    /// space, accounting for the root's surface offset and its render offset (e.g. below a
    /// form title bar). The returned point is suitable as a child control's <see cref="Control.Location"/>.
    /// </summary>
    /// <param name="surfacePoint">The point in this surface's coordinates.</param>
    /// <returns>The corresponding point in the root's client area.</returns>
    private Point ToRootLocal(Point surfacePoint)
    {
        // The root's actual position on the surface is its Location, which already
        // includes the scroll offset applied by LayoutRoot. Subtract it (plus the
        // root's own render offset, e.g. below a title bar) to land in the root's
        // client area where child controls are positioned.
        var offset = _rootControl!.GetChildRenderOffsetPublic();
        return new Point(
            surfacePoint.X - _rootControl!.X - offset.X,
            surfacePoint.Y - _rootControl!.Y - offset.Y);
    }

    /// <summary>
    /// Adds a control that was created by a toolbox item.
    /// </summary>
    /// <param name="toolboxItem">The toolbox item describing the control type.</param>
    /// <param name="location">The placement location.</param>
    /// <returns>The newly created DesignItem.</returns>
    public DesignItem AddControlFromToolbox(ToolboxItem toolboxItem, Point location)
    {
        var control = _toolboxService.CreateControl(toolboxItem);
        control.Location = SnapToGrid ? SnapPoint(location) : location;
        return AddControl(control, control.Location);
    }

    /// <summary>
    /// Registers a structural design item that is positioned by its container rather than by
    /// user interaction. Unlike <see cref="AddControl(Control, ContainerControl, Point)"/> this
    /// does not select the item nor clear the undo history, so layout synchronization (for
    /// example report band containers) does not disturb the user's selection or history.
    /// </summary>
    /// <param name="control">The control to track as a design item.</param>
    /// <param name="parent">The container control the control is parented to.</param>
    /// <returns>The newly created design item.</returns>
    internal DesignItem RegisterStructuralItem(Control control, ContainerControl parent)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        if (parent == null) throw new ArgumentNullException(nameof(parent));

        control.Parent = parent;
        var item = new DesignItem(control);
        item.ParentItem = FindItem(parent);
        _designItems.Add(item);
        return item;
    }

    /// <summary>
    /// Removes a structural design item previously added via <see cref="RegisterStructuralItem"/>.
    /// Deselects the item if it is currently selected and removes its control from the tree.
    /// </summary>
    /// <param name="control">The control whose design item should be removed.</param>
    internal void UnregisterStructuralItem(Control control)
    {
        var item = FindItem(control);
        if (item == null) return;
        _selectionService.Deselect(item);
        control.Parent?.Controls.Remove(control);
        _designItems.Remove(item);
    }

    /// <summary>
    /// Removes a design item and its underlying control from the surface.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    public void RemoveControl(DesignItem item)
    {
        if (item == null) return;

        if (item.Control is DesignReportControl reportProxy)
            reportProxy.DetachWrappedFromBands();

        _undoService.PushSnapshot(item.Control);
        _selectionService.Deselect(item);
        item.Control.Parent?.Controls.Remove(item.Control);
        _designItems.Remove(item);
        ContentModified?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>
    /// Removes all currently selected controls.
    /// </summary>
    public void DeleteSelected()
    {
        var items = _selectionService.SelectedItems.ToArray();
        if (items.Length == 0) return;

        _undoService.PushSnapshot(items.Select(i => i.Control).ToArray());
        _selectionService.DeselectAll();

        foreach (var item in items)
        {
            item.Control.Parent?.Controls.Remove(item.Control);
            _designItems.Remove(item);
        }

        ContentModified?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>
    /// Finds the DesignItem that wraps the given control.
    /// </summary>
    public DesignItem? FindItem(Control control)
    {
        return _designItems.FirstOrDefault(di => di.Control == control);
    }

    /// <summary>
    /// Finds the design adapter whose interaction region contains the given surface point.
    /// Returns the adapter and the point converted to the adapter control's local coordinates.
    /// </summary>
    private IControlDesignAdapter? FindDesignAdapter(Point surfacePoint, out Point localPoint)
    {
        localPoint = Point.Empty;

        if (_activeAdapter != null)
        {
            localPoint = _activeAdapter.Control.PointToClient(this.PointToScreen(surfacePoint));
            return _activeAdapter;
        }

        var formPoint = this.PointToScreen(surfacePoint);
        for (int i = _designItems.Count - 1; i >= 0; i--)
        {
            var item = _designItems[i];
            var adapter = _adapterRegistry.GetAdapter(item.Control);
            if (adapter == null) continue;

            var local = item.Control.PointToClient(formPoint);
            if (adapter.TryHitRegion(local))
            {
                localPoint = local;
                return adapter;
            }
        }

        return null;
    }

    /// <summary>
    /// Clears all controls from the design surface.
    /// </summary>
    public void ClearAll()
    {
        _selectionService.DeselectAll();
        _designItems.Clear();
        Controls.Clear();
        _undoService.Clear();
        ContentModified?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>
    /// Snaps a point to the nearest grid intersection.
    /// </summary>
    public Point SnapPoint(Point point)
    {
        int grid = GridSize;
        return new Point(
            (int)Math.Round(point.X / (double)grid) * grid,
            (int)Math.Round(point.Y / (double)grid) * grid);
    }

    /// <summary>
    /// Overrides child hit-testing to return null for all designed controls.
    /// This ensures all mouse events are routed to DesignSurface, allowing the
    /// designer to intercept selection, move, and resize operations. Controls
    /// still render normally; they just don't receive interactive events.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns>Always null — designed controls are not interactive in design mode.</returns>
    protected override Control? GetChildAtPoint(Point point)
    {
        return null;
    }

    /// <summary>
    /// Recursively hit-tests the design surface and returns the topmost selectable
    /// <see cref="DesignItem"/> at the given point (in surface coordinates).
    /// Descends into nested containers so that controls placed inside a container
    /// (Panel, GroupBox, TabControl, ...) are selectable by clicking them.
    /// </summary>
    /// <param name="point">The point in this surface's coordinates.</param>
    /// <returns>The topmost DesignItem at the point, or null if none.</returns>
    public DesignItem? FindItemAt(Point point)
    {
        // Convert once into form coordinates; PointToClient below then yields the point
        // in any child's parent coordinate space, which is required for controls nested
        // inside the root (or any container), not just controls directly on the surface.
        var formPoint = this.PointToScreen(point);

        for (int i = _designItems.Count - 1; i >= 0; i--)
        {
            var item = _designItems[i];
            var ctrl = item.Control;
            if (!ctrl.Visible) continue;

            if (ctrl is ContainerControl container)
            {
                var local = container.PointToClient(formPoint);
                if (!new Rectangle(0, 0, container.Width, container.Height).Contains(local)) continue;

                var deepest = container.GetDeepestChildAtPoint(local, out _);
                if (deepest != null)
                {
                    var nested = FindItem(deepest);
                    if (nested != null) return nested;
                }

                // Empty area of the container: the container itself is selectable.
                return item;
            }

            // `PointToClient` yields the point in the control's own local space, where the
            // control fills (0,0,Width,Height). Bounds is parent-relative, so comparing the
            // local point against it would only match at (0,0); test against the local bounds.
            var localPoint = ctrl.PointToClient(formPoint);
            if (new Rectangle(0, 0, ctrl.Width, ctrl.Height).Contains(localPoint))
                return item;
        }

        return null;
    }

    /// <summary>
    /// Finds the innermost container control that contains the given point (in surface
    /// coordinates). Recurses through nested containers using
    /// <see cref="ContainerControl.GetDeepestChildAtPoint"/> so that a point over a
    /// container's content is attributed to the deepest container that can hold it.
    /// </summary>
    /// <param name="point">The point in this surface's coordinates.</param>
    /// <param name="exclude">A control to skip (e.g. the control currently being dragged).</param>
    /// <returns>The innermost containing container, or null if none contains the point.</returns>
    public ContainerControl? FindDropContainerAt(Point point, Control? exclude = null)
    {
        for (int i = _designItems.Count - 1; i >= 0; i--)
        {
            var item = _designItems[i];
            var ctrl = item.Control;
            if (ctrl == exclude || !ctrl.Visible) continue;
            if (ctrl is not ContainerControl container) continue;

            // `point` is in surface-local coordinates; convert it to form coordinates
            // before PointToClient so the offset of this surface (and any ancestors) is
            // accounted for. Otherwise a container under the pointer would be missed
            // whenever the surface carries a non-zero position.
            var formPoint = this.PointToScreen(point);
            var local = container.PointToClient(formPoint);
            if (!new Rectangle(0, 0, container.Width, container.Height).Contains(local)) continue;

            var deepest = container.GetDeepestChildAtPoint(local, out _);
            if (deepest == null)
                return container;                 // empty area of this container
            if (deepest is ContainerControl deepestContainer)
                return deepestContainer;          // deepest child is itself a container
            return deepest.Parent as ContainerControl; // sibling of a leaf control
        }

        return null;
    }

    /// <summary>
    /// Creates a new control at the specified location by type name.
    /// </summary>
    /// <param name="typeName">The display name or type name of the control.</param>
    /// <param name="location">The placement location.</param>
    /// <returns>The DesignItem, or null if the type was not found.</returns>
    public DesignItem? AddControlByType(string typeName, Point location)
    {
        var control = _toolboxService.CreateControl(typeName);
        if (control == null) return null;
        return AddControl(control, location);
    }

    private static ModifierKeys GetCurrentModifiers()
    {
        try
        {
            return OldSchoolForms.Ui.Platform.Platform.GetCurrentModifiers();
        }
        catch
        {
            return ModifierKeys.None;
        }
    }

    protected override void OnMouseDown(EventArgs e)
    {
        if (e is not MouseEventArgs args) return;

        // Handle pending drop from toolbox
        if (_pendingDropItem != null)
        {
            if (args.Button == MouseButtons.Left)
            {
                var dropPoint = new Point(args.X, args.Y);
                PerformToolboxDrop(dropPoint);
            }
            else
            {
                CancelExternalDrop();
            }
            return;
        }

        var downPoint = new Point(args.X, args.Y);
        if (HandleScrollbarMouseDown(downPoint))
            return;

        // Design-mode adapter routing: forward clicks on interaction regions
        // (e.g. tab headers) to the matching adapter so placed controls can be
        // interacted with while designing.
        if (FindDesignAdapter(downPoint, out var localDown) is { } downAdapter)
        {
            if (_activeAdapter != downAdapter)
            {
                var item = FindItem(downAdapter.Control);
                if (item != null && !GetCurrentModifiers().HasFlag(ModifierKeys.Control))
                    _selectionService.Select(item);
            }
            _activeAdapter = downAdapter;
            downAdapter.OnMouseDown(localDown, new MouseEventArgs(args.Button, args.Clicks, localDown.X, localDown.Y, args.Delta));
            if (!downAdapter.IsInteracting) _activeAdapter = null;
            return;
        }

        _lastMouseDown = new Point(args.X, args.Y);
        _hasMouseMovedSinceDown = false;

        // Track mouse button state - Platform layer always passes MouseButtons.None
        // in OnMouseMove, so we must track it ourselves.
        if (args.Button == MouseButtons.Left) _leftButtonPressed = true;

        // Fix 1: Detect if clicking on a resize handle upfront to prevent
        // OnMouseUp from overriding the selection before drag starts.
        var primarySel = _selectionService.PrimarySelection;
        UpdateResizeConstraints(primarySel);
        _pendingResizeHandle = primarySel != null
            ? _dragService.HitTestHandles(primarySel, _lastMouseDown)
            : ResizeHandle.None;

        // Do NOT call base.OnMouseDown — we intercept all mouse events
        // to prevent routing interaction events to designed controls.
    }

    private void PerformToolboxDrop(Point dropPoint)
    {
        if (_pendingDropItem == null) return;

        // Drop into the innermost container at the point, if any.
        var container = FindDropContainerAt(dropPoint);
        if (container != null)
        {
            var control = _toolboxService.CreateControl(_pendingDropItem);
            var client = container.PointToClient(PointToScreen(dropPoint));
            var offset = container.GetChildRenderOffsetPublic();
            var localPoint = new Point(client.X - offset.X, client.Y - offset.Y);
            AddControl(control, container, localPoint);
        }
        else
        {
            AddControlFromToolbox(_pendingDropItem, dropPoint);
        }

        _pendingDropItem = null;
        var form = FindForm();
        if (form != null) form.Cursor = null;
        Invalidate();
    }

    protected override void OnMouseMove(EventArgs e)
    {
        if (e is not MouseEventArgs args) return;

        var point = new Point(args.X, args.Y);

        if (HandleScrollbarMouseMove(point))
        {
            Invalidate();
            return;
        }

        // Design-mode adapter routing: forward moves to the active adapter
        // (e.g. overflow dropdown hover tracking).
        if (FindDesignAdapter(point, out var localAdapter) is { } adapter)
        {
            adapter.OnMouseMove(localAdapter, new MouseEventArgs(args.Button, args.Clicks, localAdapter.X, localAdapter.Y, args.Delta));
            if (!adapter.IsInteracting) _activeAdapter = null;
            return;
        }

        if (_dragService.IsDragging)
        {
            _dragService.ContinueDrag(point);
            Invalidate();
            return;
        }

        // Silk.NET always passes MouseButtons.None in MouseMove, so we use our own
        // _leftButtonPressed flag to know whether the button is held down.
        if (!_leftButtonPressed)
        {
            // Update resize cursor when hovering over handles
            var primary = _selectionService.PrimarySelection;
            UpdateResizeConstraints(primary);
            if (primary != null)
            {
                var handle = _dragService.HitTestHandles(primary, point);
                if (handle != ResizeHandle.None)
                    SetResizeCursor(handle);
                else
                    ClearResizeCursor();
            }
            else
            {
                ClearResizeCursor();
            }
        }

        if (_leftButtonPressed && !_hasMouseMovedSinceDown)
        {
            int moveThreshold = 4;
            if (Math.Abs(point.X - _lastMouseDown.X) > moveThreshold ||
                Math.Abs(point.Y - _lastMouseDown.Y) > moveThreshold)
            {
                _hasMouseMovedSinceDown = true;

                var primarySel = _selectionService.PrimarySelection;
                if (primarySel != null)
                {
                    if (_pendingResizeHandle != ResizeHandle.None)
                    {
                        _dragService.BeginResize(_lastMouseDown, _pendingResizeHandle);
                        _undoService.PushSnapshot(primarySel.Control);
                    }
                    else
                    {
                        _dragService.BeginMove(_lastMouseDown);
                        var controls = _selectionService.SelectedItems.Select(i => i.Control).ToArray();
                        _undoService.PushSnapshot(controls);
                    }
                }
            }
        }
    }

    protected override void OnMouseUp(EventArgs e)
    {
        if (e is not MouseEventArgs args) return;

        if (_vscroll.IsDragging || _hscroll.IsDragging ||
            _vscroll.IsUpButtonPressed || _vscroll.IsDownButtonPressed ||
            _hscroll.IsUpButtonPressed || _hscroll.IsDownButtonPressed)
        {
            if (_vscroll.NeedsScrollbar) _vscroll.HandleMouseUp(ScrollbarContext);
            if (_hscroll.NeedsScrollbar) _hscroll.HandleMouseUp(ScrollbarContext);
            return;
        }

        var point = new Point(args.X, args.Y);

        // Design-mode adapter routing: forward the up event to the active adapter
        // (e.g. closing the overflow dropdown).
        if (FindDesignAdapter(point, out var localAdapter) is { } adapter)
        {
            adapter.OnMouseUp(localAdapter, new MouseEventArgs(args.Button, args.Clicks, localAdapter.X, localAdapter.Y, args.Delta));
            if (!adapter.IsInteracting) _activeAdapter = null;
            _leftButtonPressed = false;
            return;
        }

        if (_dragService.IsDragging)
        {
            _dragService.EndDrag();
            // The mouse button is physically released here, so clear the pressed flag.
            // Leaving it set would leak into the next click (e.g. a toolbox drop) and make
            // the click-selection block below run with a stale state.
            _leftButtonPressed = false;
            ClearResizeCursor();
            return;
        }

        // Use _leftButtonPressed instead of args.Button - Silk.NET may not pass correct
        // button state in MouseUp events either.
        if (!_hasMouseMovedSinceDown && _leftButtonPressed)
        {
            // Fix 1: Skip selection if we clicked on a resize handle - the drag will start
            // in OnMouseMove, and selecting here would override it.
            if (_pendingResizeHandle == ResizeHandle.None)
            {
                // It was a click (not a drag). FindItemAt descends into containers so that
                // controls placed inside a Panel, TabControl, etc. are selectable.
                var hitItem = FindItemAt(point);
                if (hitItem != null)
                {
                    bool ctrlPressed = GetCurrentModifiers().HasFlag(ModifierKeys.Control);

                    if (ctrlPressed)
                        _selectionService.ToggleSelection(hitItem);
                    else
                        _selectionService.Select(hitItem);
                }
                else
                {
                    _selectionService.DeselectAll();
                }
            }
        }

        _leftButtonPressed = false;
        ClearResizeCursor();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Cancel pending drop on Escape
        if (_pendingDropItem != null && e.KeyCode == Keys.Escape)
        {
            CancelExternalDrop();
            Invalidate();
            e.Handled = true;
            return;
        }

        // Design-mode adapter routing: forward keys to the active adapter
        // (e.g. overflow dropdown keyboard navigation).
        if (_activeAdapter != null)
        {
            _activeAdapter.OnKeyDown(e);
            if (!_activeAdapter.IsInteracting) _activeAdapter = null;
            return;
        }

        // Allow F4 to open the overflow dropdown when a TabControl is the primary selection.
        if (e.KeyCode == Keys.F4)
        {
            var primaryControl = _selectionService.PrimarySelection?.Control;
            if (primaryControl != null)
            {
                var adapter = _adapterRegistry.GetAdapter(primaryControl);
                if (adapter != null && adapter.OnKeyDown(e))
                {
                    _activeAdapter = adapter;
                    return;
                }
            }
        }

        if (e.KeyCode == Keys.Escape)
        {
            var primaryControl = _selectionService.PrimarySelection?.Control;
            var escAdapter = primaryControl != null ? _adapterRegistry.GetAdapter(primaryControl) : null;
            if (escAdapter != null && escAdapter.TryClearSelection())
            {
                Invalidate();
                e.Handled = true;
                return;
            }
        }

        if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
        {
            var primaryControl = _selectionService.PrimarySelection?.Control;
            var delAdapter = primaryControl != null ? _adapterRegistry.GetAdapter(primaryControl) : null;
            if (delAdapter != null && delAdapter.TryDeleteSelection())
            {
                e.Handled = true;
                return;
            }
            DeleteSelected();
            e.Handled = true;
            return;
        }

        if (e.Modifiers.HasFlag(ModifierKeys.Control))
        {
            switch (e.KeyCode)
            {
                case Keys.Z:
                    if (_undoService.CanUndo) _undoService.Undo();
                    e.Handled = true;
                    return;
                case Keys.Y:
                    if (_undoService.CanRedo) _undoService.Redo();
                    e.Handled = true;
                    return;
                case Keys.A:
                    _selectionService.SelectAll(_designItems);
                    Invalidate();
                    e.Handled = true;
                    return;
            }
        }

        // Nudge selected controls with arrow keys
        if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
        {
            int dx = 0, dy = 0;
            int step = _snapToGrid ? GridSize : 1;
            switch (e.KeyCode)
            {
                case Keys.Left: dx = -step; break;
                case Keys.Right: dx = step; break;
                case Keys.Up: dy = -step; break;
                case Keys.Down: dy = step; break;
            }

            foreach (var item in _selectionService.SelectedItems)
            {
                var ctrl = item.Control;
                ctrl.Location = new Point(ctrl.X + dx, ctrl.Y + dy);
            }
            e.Handled = true;
            Invalidate();
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
    }

    protected override void OnMouseWheel(EventArgs e)
    {
        if (e is MouseEventArgs args)
        {
            // Design-mode adapter routing: forward wheel events to the active adapter
            // (e.g. scrolling the overflow dropdown).
            var wheelPoint = new Point(args.X, args.Y);
            if (FindDesignAdapter(wheelPoint, out var localAdapter) is { } adapter)
            {
                adapter.OnMouseWheel(localAdapter, new MouseEventArgs(args.Button, args.Clicks, localAdapter.X, localAdapter.Y, args.Delta));
                if (!adapter.IsInteracting) _activeAdapter = null;
                return;
            }

            // Keep the scrollbar ranges fresh (content size may have changed), then
            // hand the wheel delta to whichever scrollbar is currently overflowed.
            LayoutRoot();
            if (_vscroll.NeedsScrollbar)
                _vscroll.HandleMouseWheel(args.Delta, ScrollbarContext);
            if (_hscroll.NeedsScrollbar)
                _hscroll.HandleMouseWheel(args.Delta, ScrollbarContext);
        }

        base.OnMouseWheel(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _vscroll.HandleMouseLeave(ScrollbarContext);
        _hscroll.HandleMouseLeave(ScrollbarContext);
        base.OnMouseLeave(e);
    }

    public override void Render(Graphics g)
    {
        // Fix: Fill background first to clear previous frame content.
        // Without this, the SKSurface retains old content at the old control
        // positions, causing ghost lines and flickering during drag operations.
        g.FillRectangle(BackColor, 0, 0, Width, Height);

        // Keep the root position and scrollbar ranges in sync with the current
        // content size before anything is drawn.
        LayoutRoot();

        // Draw grid background
        DrawGrid(g);

        // Render all child controls (the designed controls)
        base.Render(g);

        // Draw selection chrome (handles + border) on selected controls
        DrawSelectionChrome(g);

        // Draw smart guide lines during drag
        DrawGuideLines(g);

        // Draw scrollbars when the root content overflows the surface.
        DrawScrollbars(g);

        // Draw the in-surface add-item type-picker popup of the primary adapter on top.
        DrawAdapterPopups(g);

        // Draw subtle top banner when in toolbox drop mode
        if (_pendingDropItem != null)
        {
            var bannerHeight = 40;
            var bannerColor = Color.FromArgb(120, 230, 230, 230);
            g.FillRectangle(bannerColor, 0, 0, Width, bannerHeight);
            var borderColor = Color.FromArgb(160, 200, 200, 200);
            g.FillRectangle(borderColor, 0, bannerHeight - 1, Width, 1);
            var msg = string.Format(LangRes.GetString("Designer_DropHint"), _pendingDropItem.DisplayName);
            g.DrawString(msg, ThemeManager.CurrentTheme.DefaultFont,
                Color.FromArgb(0, 80, 160), 10, 10);
        }
    }

    private void LayoutRoot()
    {
        if (_rootControl == null) return;

        var root = _rootControl;

        // Vertical range: content height vs. the visible height.
        _vscroll.ViewSize = Height;
        _vscroll.ContentSize = root.Height;
        if (_vscroll.Value > _vscroll.MaxScroll)
            _vscroll.Value = _vscroll.MaxScroll;

        // Horizontal range: content width vs. the visible width.
        _hscroll.ViewSize = Width;
        _hscroll.ContentSize = root.Width;
        if (_hscroll.Value > _hscroll.MaxScroll)
            _hscroll.Value = _hscroll.MaxScroll;

        var newOffset = new Point(_hscroll.Value, _vscroll.Value);
        if (newOffset.X == _scrollOffset.X && newOffset.Y == _scrollOffset.Y) return;

        _scrollOffset = newOffset;

        // Move the root by the negative scroll offset so its content pans into view.
        // The position is only rewritten while scrolling actually changes, which keeps
        // a location set directly on the root (e.g. by callers or tests) intact whenever
        // the content fits the surface.
        root.Location = new Point(_rootOffset.X - newOffset.X, _rootOffset.Y - newOffset.Y);
        Invalidate();
    }

    private void DrawScrollbars(Graphics g)
    {
        var theme = ThemeManager.CurrentTheme;
        int vsw = _vscroll.ScrollBarSize;
        int hsh = _hscroll.ScrollBarSize;

        if (_vscroll.NeedsScrollbar)
            _vscroll.Render(g, new Rectangle(Width - vsw, 0, vsw, Height), theme);

        if (_hscroll.NeedsScrollbar)
            _hscroll.Render(g, new Rectangle(0, Height - hsh, Width, hsh), theme);
    }

    private bool HandleScrollbarMouseDown(Point point)
    {
        int vsw = _vscroll.ScrollBarSize;
        int hsh = _hscroll.ScrollBarSize;

        if (_vscroll.NeedsScrollbar)
        {
            var vRect = new Rectangle(Width - vsw, 0, vsw, Height);
            if (vRect.Contains(point))
            {
                _vscroll.HandleMouseDown(point, vRect, ScrollbarContext);
                return true;
            }
        }

        if (_hscroll.NeedsScrollbar)
        {
            var hRect = new Rectangle(0, Height - hsh, Width, hsh);
            if (hRect.Contains(point))
            {
                _hscroll.HandleMouseDown(point, hRect, ScrollbarContext);
                return true;
            }
        }

        return false;
    }

    private bool HandleScrollbarMouseMove(Point point)
    {
        if (!_vscroll.IsDragging && !_hscroll.IsDragging) return false;

        int vsw = _vscroll.ScrollBarSize;
        int hsh = _hscroll.ScrollBarSize;

        if (_vscroll.IsDragging && _vscroll.NeedsScrollbar)
        {
            var vRect = new Rectangle(Width - vsw, 0, vsw, Height);
            _vscroll.HandleMouseMove(point, vRect, ScrollbarContext);
        }

        if (_hscroll.IsDragging && _hscroll.NeedsScrollbar)
        {
            var hRect = new Rectangle(0, Height - hsh, Width, hsh);
            _hscroll.HandleMouseMove(point, hRect, ScrollbarContext);
        }

        return true;
    }

    private ScrollBarContext ScrollbarContext => _scrollBarContext ??= new ScrollBarContext(this);

    /// <summary>
    /// Adapts a surface to the <see cref="IScrollBarContext"/> contract so that a
    /// <see cref="ScrollBarEngine"/> can query the current zoom, request repaints, and
    /// capture the mouse while a thumb or button is being dragged.
    /// </summary>
    private sealed class ScrollBarContext : IScrollBarContext
    {
        private readonly DesignSurface _owner;

        public ScrollBarContext(DesignSurface owner) => _owner = owner;

        public float Zoom => _owner.EffectiveZoom;

        public void Invalidate() => _owner.Invalidate();

        public void CaptureMouse(bool capture) => _owner.CapturingMouse = capture;
    }

    private void DrawGrid(Graphics g)
    {
        if (!_showGrid) return;

        // Draw a subtle dot pattern: only draw in the lower-right quadrant of each cell.
        // 9 draw calls per cell (3×3 grid dots) instead of O(n×m) fill rectangles.
        var gridColor = Color.FromArgb(230, 230, 230);
        int spacing = GridSize;

        // Fix 3: Reduce grid draw calls. Instead of every cell, draw every second cell
        // and only 1 dot per cell.
        int stride = spacing * 2;  // skip every second cell
        int cols = (Width + stride - 1) / stride;
        int rows = (Height + stride - 1) / stride;

        for (int cx = 0; cx < cols; cx++)
        {
            int baseX = cx * stride;
            if (baseX >= Width) break;

            for (int cy = 0; cy < rows; cy++)
            {
                int baseY = cy * stride;
                if (baseY >= Height) break;

                // Draw 1 dot per cell at center
                g.FillRectangle(gridColor, baseX + spacing / 2, baseY + spacing / 2, 1, 1);
            }
        }
    }

    private void DrawAdapterPopups(Graphics g)
    {
        var primary = _selectionService.PrimarySelection;
        if (primary == null) return;

        var adapter = _adapterRegistry.GetAdapter(primary.Control);
        if (adapter == null) return;
        if (!adapter.TryGetAddPopup(out var localBounds, out var labels, out var hovered)) return;
        if (labels.Count == 0) return;

        var origin = this.PointToClient(primary.Control.PointToScreen(Point.Empty));
        var box = new Rectangle(origin.X + localBounds.X, origin.Y + localBounds.Y,
            localBounds.Width, localBounds.Height);
        var theme = ThemeManager.CurrentTheme;

        g.FillRectangle(theme.ControlLight, box.X, box.Y, box.Width, box.Height);
        g.DrawRectangle(theme.ControlDark, box.X, box.Y, box.Width, box.Height, 1);

        int rowHeight = box.Height / labels.Count;
        for (int i = 0; i < labels.Count; i++)
        {
            var rowRect = new Rectangle(box.X, box.Y + i * rowHeight, box.Width, rowHeight);
            if (i == hovered)
                g.FillRectangle(theme.HoverHighlight, rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height);

            var textColor = i == hovered ? theme.HighlightText : theme.ControlText;
            g.DrawString(labels[i], theme.DefaultFont, textColor, rowRect.X + 8, rowRect.Y + 4);
        }
    }

    private void DrawSelectionChrome(Graphics g)
    {
        var selectionColor = Color.FromArgb(0, 120, 215);
        int hs = SelectionService.GetHandleSize();

        foreach (var item in _designItems)
        {
            if (!item.Selected) continue;

            // Bounds are expressed in the control's parent coordinate space, but the
            // chrome is drawn in this surface's space, so convert the top-left to
            // surface coordinates. This keeps the selection box and handles correct
            // for controls nested inside a container (e.g. a button inside a panel).
            var origin = this.PointToClient(item.Control.PointToScreen(Point.Empty));
            var bounds = new Rectangle(origin.X, origin.Y, item.Control.Bounds.Width, item.Control.Bounds.Height);
            bool isPrimary = item == _selectionService.PrimarySelection;

            if (isPrimary)
            {
                g.DrawRectangle(selectionColor, bounds.X - 1, bounds.Y - 1,
                    bounds.Width + 2, bounds.Height + 2, 2);

                var adapter = _adapterRegistry.GetAdapter(item.Control);
                if (adapter != null)
                {
                    if (adapter.TryGetSelectionHighlight(out var itemBounds))
                    {
                        var hl = new Rectangle(origin.X + itemBounds.X, origin.Y + itemBounds.Y,
                            itemBounds.Width, itemBounds.Height);
                        g.DrawRectangle(selectionColor, hl.X, hl.Y, hl.Width, hl.Height, 1);
                    }

                    if (adapter.TryGetAddButtonHighlight(out var addBounds))
                    {
                        var ab = new Rectangle(origin.X + addBounds.X, origin.Y + addBounds.Y,
                            addBounds.Width, addBounds.Height);
                        g.DrawRectangle(selectionColor, ab.X, ab.Y, ab.Width, ab.Height, 1);
                        int cx = ab.X + ab.Width / 2;
                        int cy = ab.Y + ab.Height / 2;
                        g.DrawLine(selectionColor, cx - 3, cy, cx + 3, cy, 1);
                        g.DrawLine(selectionColor, cx, cy - 3, cx, cy + 3, 1);
                    }
                }
            }
            else
            {
                g.DrawRectangle(selectionColor, bounds.X, bounds.Y,
                    bounds.Width, bounds.Height, 1);
            }

            if (!isPrimary) continue;

            int half = hs / 2;

            DrawHandle(g, bounds.X - half, bounds.Y - half, hs, hs, selectionColor, true);
            DrawHandle(g, bounds.Right - half, bounds.Y - half, hs, hs, selectionColor, true);
            DrawHandle(g, bounds.X - half, bounds.Bottom - half, hs, hs, selectionColor, true);
            DrawHandle(g, bounds.Right - half, bounds.Bottom - half, hs, hs, selectionColor, true);

            if (bounds.Width > hs * 3)
            {
                int cx = bounds.X + bounds.Width / 2 - half;
                DrawHandle(g, cx, bounds.Y - half, hs, hs, selectionColor, false);
                DrawHandle(g, cx, bounds.Bottom - half, hs, hs, selectionColor, false);
            }

            if (bounds.Height > hs * 3)
            {
                int cy = bounds.Y + bounds.Height / 2 - half;
                DrawHandle(g, bounds.X - half, cy, hs, hs, selectionColor, false);
                DrawHandle(g, bounds.Right - half, cy, hs, hs, selectionColor, false);
            }
        }
    }

    private void DrawGuideLines(Graphics g)
    {
        if (!_dragService.IsDragging) return;

        var snap = _dragService.CurrentSnap;
        if (snap.GuideLines.Count == 0) return;

        var guideColor = Color.FromArgb(255, 0, 120, 215);
        var centerColor = Color.FromArgb(0, 200, 0);
        var containerColor = Color.FromArgb(255, 255, 120, 0);

        // WinForms-style dash pattern for container/form snap guides.
        var containerDash = new[] { 5f, 4f };

        foreach (var line in snap.GuideLines)
        {
            bool isCenter = line.Kind == GuideKind.Center;
            bool isContainer = line.Kind == GuideKind.Container;

            // Draw glow (thicker, more transparent)
            var glowColor = Color.FromArgb(
                isContainer ? 50 : (isCenter ? 60 : 40),
                isContainer ? 255 : (isCenter ? 0 : 0),
                isContainer ? 120 : (isCenter ? 200 : 120),
                isContainer ? 0 : 215);
            g.DrawLine(glowColor, line.Start.X, line.Start.Y, line.End.X, line.End.Y, 3);

            // Draw main line; container/form guides use a dashed stroke so they read as alignment aids.
            var mainColor = isContainer ? containerColor : (isCenter ? centerColor : guideColor);
            g.DrawLine(mainColor, line.Start.X, line.Start.Y, line.End.X, line.End.Y, 1, isContainer ? containerDash : null);
        }
    }

    private static void DrawHandle(Graphics g, int x, int y, int w, int h, Color borderColor, bool filled)
    {
        if (filled)
        {
            g.FillRectangle(Color.White, x + 1, y + 1, w - 2, h - 2);
        }
        g.DrawRectangle(borderColor, x, y, w, h, 1);
    }

    private void SetResizeCursor(ResizeHandle handle)
    {
        var form = FindForm();
        if (form == null) return;

        form.Cursor = handle switch
        {
            ResizeHandle.TopLeft or ResizeHandle.BottomRight => SystemCursorType.SizeAll,
            ResizeHandle.TopRight or ResizeHandle.BottomLeft => SystemCursorType.SizeAll,
            ResizeHandle.TopCenter or ResizeHandle.BottomCenter => SystemCursorType.SizeNS,
            ResizeHandle.MiddleLeft or ResizeHandle.MiddleRight => SystemCursorType.SizeWE,
            _ => null
        };
    }

    private void ClearResizeCursor()
    {
        var form = FindForm();
        if (form != null)
            form.Cursor = null;
    }

    /// <summary>
    /// Applies the appropriate minimum size to the drag service for the next resize.
    /// The root (design form or user control) must remain large enough to host a usable
    /// title bar/client area, so it gets a larger floor than ordinary controls.
    /// </summary>
    /// <param name="primary">The primary selection being resized, or null.</param>
    private void UpdateResizeConstraints(DesignItem? primary)
    {
        if (primary?.Control is DesignForm)
        {
            _dragService.MinResizeSize = new Size(300, 160);
        }
        else if (primary?.Control is DesignUserControl)
        {
            _dragService.MinResizeSize = new Size(80, 60);
        }
        else if (primary?.Control is DesignReport)
        {
            // The page size is driven by the report's page setup; the root snaps
            // back to it after any resize attempt, so forbid shrinking entirely.
            _dragService.MinResizeSize = primary.Control.Size;
        }
        else
        {
            _dragService.MinResizeSize = new Size(10, 10);
        }
    }
}
