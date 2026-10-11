using Jint.Native;
using OldSchoolForms.Ui.Controls;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="TreeView"/>. Exposes the node tree,
/// the selection and the <c>afterselect</c> event.
/// </summary>
public class JsTreeView : JsControl
{
    /// <summary>
    /// Creates the facade around a tree view.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="treeView">The wrapped tree view.</param>
    public JsTreeView(JsEngineHost host, TreeView treeView)
        : base(host, treeView)
    {
        TreeView = treeView;
    }

    /// <summary>The wrapped tree view.</summary>
    public TreeView TreeView { get; }

    /// <summary>The root node facades.</summary>
    public JsTreeNode[] Nodes
    {
        get
        {
            var list = new List<JsTreeNode>();
            foreach (var node in TreeView.Nodes)
                list.Add(new JsTreeNode(Host, node));
            return list.ToArray();
        }
    }

    /// <summary>
    /// Adds a root node with the given text.
    /// </summary>
    /// <param name="text">The node caption.</param>
    /// <returns>The facade of the new node.</returns>
    public JsTreeNode AddNode(string text)
    {
        var node = new TreeNode(text ?? string.Empty);
        TreeView.Nodes.Add(node);
        return new JsTreeNode(Host, node);
    }

    /// <summary>The selected node facade, or <c>null</c>.</summary>
    public JsTreeNode? SelectedNode
    {
        get
        {
            var node = TreeView.SelectedNode;
            return node == null ? null : new JsTreeNode(Host, node);
        }
    }

    /// <summary>Indent per tree level in pixels.</summary>
    public int Indent
    {
        get => TreeView.Indent;
        set => TreeView.Indent = value;
    }

    /// <summary>Whether root-level connecting lines are drawn.</summary>
    public bool ShowRootLines
    {
        get => TreeView.ShowRootLines;
        set => TreeView.ShowRootLines = value;
    }

    /// <summary>Expands all nodes.</summary>
    public void ExpandAll() => TreeView.ExpandAll();

    /// <summary>Collapses all nodes.</summary>
    public void CollapseAll() => TreeView.CollapseAll();

    /// <summary>
    /// Scrolls the tree so the given node is visible.
    /// </summary>
    /// <param name="node">The node facade to scroll into view.</param>
    public void EnsureVisible(JsTreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        TreeView.EnsureVisible(node.Node);
    }

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        if (normalized != "afterselect")
            return null;

        EventHandler handler = (s, e) => Raise("afterselect", callback, e);
        TreeView.AfterSelect += handler;
        return handler;
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        if (normalized != "afterselect")
            return false;

        TreeView.AfterSelect -= (EventHandler)handler;
        return true;
    }
}
