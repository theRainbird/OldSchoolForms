using System;
using System.Collections.Generic;
using System.Reflection;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Designer.Services;

/// <summary>
/// Represents a snapshot of control bounds or a single property change for undo/redo operations.
/// A snapshot without <see cref="PropertyName"/> restores bounds and text; a snapshot with a
/// property name restores that property to <see cref="RestoreValue"/>.
/// </summary>
public class UndoSnapshot
{
    /// <summary>
    /// Gets the control whose state was captured.
    /// </summary>
    public Control Control { get; }

    /// <summary>
    /// Gets the captured bounds.
    /// </summary>
    public Rectangle Bounds { get; }

    /// <summary>
    /// Gets the captured text value.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// Gets the name of the property captured by this snapshot, or null for bounds/text snapshots.
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    /// Gets the value to restore. For property snapshots this is the property value; for
    /// bounds/text snapshots it is null.
    /// </summary>
    public object? RestoreValue { get; }

    /// <summary>
    /// Gets the object instance a property snapshot writes back to. For control property
    /// snapshots this is the control; for report model snapshots (bands, controls, page
    /// setup) it is the model object. Null for bounds/text and action snapshots.
    /// </summary>
    public object? PropertyTarget { get; }

    /// <summary>
    /// Initializes a new snapshot for the given control.
    /// </summary>
    public UndoSnapshot(Control control)
    {
        Control = control;
        Bounds = control.Bounds;
        Text = control.Text;
    }

    internal UndoSnapshot(Control control, string propertyName, object? restoreValue)
    {
        Control = control;
        Bounds = control.Bounds;
        Text = control.Text;
        PropertyName = propertyName;
        RestoreValue = restoreValue;
        PropertyTarget = control;
    }

    internal UndoSnapshot(object propertyTarget, string propertyName, object? restoreValue)
    {
        Control = null!;
        PropertyName = propertyName;
        RestoreValue = restoreValue;
        PropertyTarget = propertyTarget;
    }

    /// <summary>
    /// Initializes a new action-based snapshot that runs delegates instead of restoring
    /// control state. Used for operations that cannot be expressed as bounds/text/property
    /// changes, such as adding or removing items from a collection.
    /// </summary>
    /// <param name="undoAction">The delegate that reverts the operation.</param>
    /// <param name="redoAction">The delegate that re-applies the operation.</param>
    internal UndoSnapshot(Action undoAction, Action redoAction)
    {
        Control = null!;
        UndoAction = undoAction;
        RedoAction = redoAction;
    }

    /// <summary>
    /// Gets the delegate that reverts the operation for action-based snapshots,
    /// or <see langword="null"/> for state-based snapshots.
    /// </summary>
    public Action? UndoAction { get; }

    /// <summary>
    /// Gets the delegate that re-applies the operation for action-based snapshots,
    /// or <see langword="null"/> for state-based snapshots.
    /// </summary>
    public Action? RedoAction { get; }

    /// <summary>
    /// Restores the control to the captured state, or invokes the undo delegate for
    /// action-based snapshots.
    /// </summary>
    public void Restore()
    {
        if (UndoAction != null)
        {
            UndoAction();
            return;
        }
        if (PropertyName != null)
        {
            if (PropertyTarget != null)
                RestoreProperty(PropertyTarget, PropertyName, RestoreValue);
            return;
        }
        Control.Bounds = Bounds;
        if (Text != null)
            Control.Text = Text;
    }

    private static void RestoreProperty(object target, string propertyName, object? value)
    {
        try
        {
            var prop = target.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
                prop.SetValue(target, value);
        }
        catch { }
    }
}

/// <summary>
/// Provides undo/redo support for designer operations.
/// Stores snapshots of control state before each mutation.
/// </summary>
public class UndoService
{
    private readonly Stack<UndoSnapshot[]> _undoStack = new();
    private readonly Stack<UndoSnapshot[]> _redoStack = new();
    private const int MaxUndoDepth = 100;

    /// <summary>
    /// Raised when the undo/redo state changes (stacks modified).
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Gets whether undo operations are available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether redo operations are available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Captures a snapshot of the given controls and pushes it onto the undo stack.
    /// Clears the redo stack, since a new action invalidates redo history.
    /// </summary>
    /// <param name="controls">The controls to capture.</param>
    public void PushSnapshot(params Control[] controls)
    {
        if (controls == null || controls.Length == 0) return;

        var snapshots = new UndoSnapshot[controls.Length];
        for (int i = 0; i < controls.Length; i++)
            snapshots[i] = new UndoSnapshot(controls[i]);

        _undoStack.Push(snapshots);
        _redoStack.Clear();

        if (_undoStack.Count > MaxUndoDepth)
        {
            var temp = new Stack<UndoSnapshot[]>(_undoStack.ToArray()[..MaxUndoDepth]);
            _undoStack.Clear();
            foreach (var item in temp)
                _undoStack.Push(item);
        }

        OnStateChanged();
    }

    /// <summary>
    /// Captures a single property change and pushes it onto the undo stack, clearing the redo
    /// stack since a new action invalidates redo history. Used by the PropertyGrid to make
    /// inline property edits (color, enum, text, numeric, bool, point, size) undoable.
    /// </summary>
    /// <param name="control">The control whose property changed.</param>
    /// <param name="propertyName">The name of the modified property.</param>
    /// <param name="oldValue">The previous property value, restored on undo.</param>
    public void PushPropertyChange(Control? control, string propertyName, object? oldValue)
    {
        if (control == null || string.IsNullOrEmpty(propertyName)) return;

        var snapshot = new UndoSnapshot(control, propertyName, oldValue);
        _undoStack.Push(new[] { snapshot });
        _redoStack.Clear();

        if (_undoStack.Count > MaxUndoDepth)
        {
            var temp = new Stack<UndoSnapshot[]>(_undoStack.ToArray()[..MaxUndoDepth]);
            _undoStack.Clear();
            foreach (var item in temp)
                _undoStack.Push(item);
        }

        OnStateChanged();
    }

    /// <summary>
    /// Captures a single property change on an arbitrary object (such as a report band or
    /// report control) and pushes it onto the undo stack, clearing the redo stack since a new
    /// action invalidates redo history. Used for model objects that are not controls.
    /// </summary>
    /// <param name="target">The object whose property changed.</param>
    /// <param name="propertyName">The name of the modified property.</param>
    /// <param name="oldValue">The previous property value, restored on undo.</param>
    public void PushPropertyChangeOn(object? target, string propertyName, object? oldValue)
    {
        if (target == null || string.IsNullOrEmpty(propertyName)) return;

        var snapshot = new UndoSnapshot(target, propertyName, oldValue);
        _undoStack.Push(new[] { snapshot });
        _redoStack.Clear();

        if (_undoStack.Count > MaxUndoDepth)
        {
            var temp = new Stack<UndoSnapshot[]>(_undoStack.ToArray()[..MaxUndoDepth]);
            _undoStack.Clear();
            foreach (var item in temp)
                _undoStack.Push(item);
        }

        OnStateChanged();
    }

    /// <summary>
    /// Pushes an action-based undo entry that stores explicit undo/redo delegates instead of
    /// control state. Used for collection mutations such as adding or removing tool strip items.
    /// Clears the redo stack since a new action invalidates redo history.
    /// </summary>
    /// <param name="undoAction">The delegate that reverts the operation.</param>
    /// <param name="redoAction">The delegate that re-applies the operation.</param>
    public void PushAction(Action undoAction, Action redoAction)
    {
        if (undoAction == null || redoAction == null) return;

        var snapshot = new UndoSnapshot(undoAction, redoAction);
        _undoStack.Push(new[] { snapshot });
        _redoStack.Clear();

        if (_undoStack.Count > MaxUndoDepth)
        {
            var temp = new Stack<UndoSnapshot[]>(_undoStack.ToArray()[..MaxUndoDepth]);
            _undoStack.Clear();
            foreach (var item in temp)
                _undoStack.Push(item);
        }

        OnStateChanged();
    }

    /// <summary>
    /// Performs an undo, restoring the most recent snapshot.
    /// The current state is pushed onto the redo stack.
    /// </summary>
    public void Undo()
    {
        if (_undoStack.Count == 0) return;

        var snapshots = _undoStack.Pop();

        // Save current state for redo, preserving property-change and action information.
        var redoSnapshots = new UndoSnapshot[snapshots.Length];
        for (int i = 0; i < snapshots.Length; i++)
            redoSnapshots[i] = snapshots[i].UndoAction != null
                ? new UndoSnapshot(snapshots[i].RedoAction!, snapshots[i].UndoAction!)
                : snapshots[i].PropertyName != null
                    ? new UndoSnapshot(snapshots[i].PropertyTarget ?? snapshots[i].Control,
                        snapshots[i].PropertyName!,
                        CurrentValueOf(snapshots[i].PropertyTarget ?? snapshots[i].Control, snapshots[i].PropertyName))
                    : new UndoSnapshot(snapshots[i].Control);
        _redoStack.Push(redoSnapshots);

        foreach (var snapshot in snapshots)
            snapshot.Restore();

        OnStateChanged();
    }

    /// <summary>
    /// Performs a redo, restoring the state before the last undo.
    /// </summary>
    public void Redo()
    {
        if (_redoStack.Count == 0) return;

        var snapshots = _redoStack.Pop();

        // Save current state for undo, preserving property-change and action information.
        var undoSnapshots = new UndoSnapshot[snapshots.Length];
        for (int i = 0; i < snapshots.Length; i++)
            undoSnapshots[i] = snapshots[i].UndoAction != null
                ? new UndoSnapshot(snapshots[i].RedoAction!, snapshots[i].UndoAction!)
                : snapshots[i].PropertyName != null
                    ? new UndoSnapshot(snapshots[i].PropertyTarget ?? snapshots[i].Control,
                        snapshots[i].PropertyName!,
                        CurrentValueOf(snapshots[i].PropertyTarget ?? snapshots[i].Control, snapshots[i].PropertyName))
                    : new UndoSnapshot(snapshots[i].Control);
        _undoStack.Push(undoSnapshots);

        foreach (var snapshot in snapshots)
            snapshot.Restore();

        OnStateChanged();
    }

    private static object? CurrentValueOf(object target, string propertyName)
    {
        try
        {
            var prop = target.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
                return prop.GetValue(target);
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Clears all undo/redo history.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnStateChanged();
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
