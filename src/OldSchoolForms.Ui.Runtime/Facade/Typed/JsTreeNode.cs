using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS-friendly wrapper around a <see cref="TreeNode"/>. Tree nodes are not
/// controls, so this facade stands on its own next to <see cref="JsControl"/>.
/// Mutating operations trigger a repaint of the owning tree view.
/// </summary>
public class JsTreeNode
{
    /// <summary>
    /// Creates the facade around a tree node.
    /// </summary>
    /// <param name="host">The JS engine for value conversion.</param>
    /// <param name="node">The wrapped tree node.</param>
    public JsTreeNode(JsEngineHost host, TreeNode node)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(node);
        Host = host;
        Node = node;
    }

    /// <summary>The JS engine used for value conversion.</summary>
    protected JsEngineHost Host { get; }

    /// <summary>The wrapped tree node.</summary>
    public TreeNode Node { get; }

    /// <summary>The text displayed for the node.</summary>
    public string Text
    {
        get => Node.Text;
        set => Node.Text = value ?? string.Empty;
    }

    /// <summary>
    /// The expanded state. Setting a value different from the current state
    /// toggles the node (the CLR setter is internal).
    /// </summary>
    public bool IsExpanded
    {
        get => Node.IsExpanded;
        set
        {
            if (value != Node.IsExpanded)
                Node.Toggle();
        }
    }

    /// <summary>
    /// The optional icon index into the tree view's image list,
    /// or <c>null</c> when unset.
    /// </summary>
    public JsValue ImageIndex
    {
        get => JsValue.FromObject(Host.Engine, Node.ImageIndex);
        set
        {
            if (value is null || value.IsUndefined() || value.IsNull())
                Node.ImageIndex = null;
            else
                Node.ImageIndex = (int)value.AsNumber();
        }
    }

    /// <summary>User-defined data attached to the node.</summary>
    public JsValue Tag
    {
        get => JsValue.FromObject(Host.Engine, Node.Tag);
        set => Node.Tag = value?.ToObject();
    }

    /// <summary>The child node facades.</summary>
    public JsTreeNode[] Children
    {
        get
        {
            var list = new List<JsTreeNode>();
            foreach (var child in Node.Children)
                list.Add(new JsTreeNode(Host, child));
            return list.ToArray();
        }
    }

    /// <summary>
    /// Adds a child node with the given text.
    /// </summary>
    /// <param name="text">The child caption.</param>
    /// <returns>The facade of the new child node.</returns>
    public JsTreeNode Add(string text)
    {
        var child = new TreeNode(text ?? string.Empty);
        Node.Add(child);
        return new JsTreeNode(Host, child);
    }

    /// <summary>Toggles the expanded state of the node.</summary>
    public void Toggle() => Node.Toggle();
}
