using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// Typed JS facade for a <see cref="TabControl"/>. Exposes the tab pages,
/// the selected index and the <c>selectedindexchanged</c> / <c>tabcloserequested</c>
/// events. On a close request the event target is the facade of the closed page.
/// </summary>
public class JsTabControl : JsControl
{
    /// <summary>
    /// Creates the facade around a tab control.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="tabControl">The wrapped tab control.</param>
    public JsTabControl(JsEngineHost host, TabControl tabControl)
        : base(host, tabControl)
    {
        TabControl = tabControl;
    }

    /// <summary>The wrapped tab control.</summary>
    public TabControl TabControl { get; }

    /// <summary>The tab page facades, in tab order.</summary>
    public JsControl[] TabPages
    {
        get
        {
            var list = new List<JsControl>();
            foreach (var page in TabControl.TabPages)
                list.Add(JsFacadeRegistry.WrapControl(Host, page));
            return list.ToArray();
        }
    }

    /// <summary>The index of the selected tab, or <c>-1</c> when none is selected.</summary>
    public int SelectedIndex
    {
        get => TabControl.SelectedIndex;
        set => TabControl.SelectedIndex = value;
    }

    /// <summary>The selected tab page facade, or <c>null</c>.</summary>
    public JsControl? SelectedTab
    {
        get
        {
            var page = TabControl.SelectedTab;
            return page == null ? null : JsFacadeRegistry.WrapControl(Host, page);
        }
    }

    /// <summary>Height of the tab header row in pixels.</summary>
    public int TabHeight
    {
        get => TabControl.TabHeight;
        set => TabControl.TabHeight = value;
    }

    /// <summary>
    /// Creates a new tab page with the given caption and appends it.
    /// </summary>
    /// <param name="title">The tab caption.</param>
    /// <returns>The facade of the new tab page.</returns>
    public JsControl AddTabPage(string title)
    {
        var page = new TabPage { Text = title ?? string.Empty };
        TabControl.AddTabPage(page);
        return JsFacadeRegistry.WrapControl(Host, page);
    }

    /// <summary>
    /// Creates a new tab page with the given caption and inserts it at the index.
    /// </summary>
    /// <param name="index">The zero-based insertion index.</param>
    /// <param name="title">The tab caption.</param>
    /// <returns>The facade of the new tab page.</returns>
    public JsControl InsertTabPage(int index, string title)
    {
        var page = new TabPage { Text = title ?? string.Empty };
        TabControl.Insert(index, page);
        return JsFacadeRegistry.WrapControl(Host, page);
    }

    /// <summary>
    /// Removes the tab page at the given index.
    /// </summary>
    /// <param name="index">The zero-based index of the page to remove.</param>
    /// <returns><see langword="true"/> when a page was removed.</returns>
    public bool RemoveTabAt(int index)
    {
        if (index < 0 || index >= TabControl.TabPages.Count)
            return false;
        TabControl.RemoveAt(index);
        return true;
    }

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        switch (normalized)
        {
            case "selectedindexchanged":
            {
                EventHandler handler = (s, e) => Raise("selectedindexchanged", callback, e);
                TabControl.SelectedIndexChanged += handler;
                return handler;
            }
            case "tabcloserequested":
            {
                EventHandler<TabPage> handler = (s, page) =>
                    RaiseEvent("tabcloserequested", callback,
                        new JsEvent("tabcloserequested", page == null ? this : JsFacadeRegistry.WrapControl(Host, page)));
                TabControl.TabCloseRequested += handler;
                return handler;
            }
            default:
                return null;
        }
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        switch (normalized)
        {
            case "selectedindexchanged":
                TabControl.SelectedIndexChanged -= (EventHandler)handler;
                return true;
            case "tabcloserequested":
                TabControl.TabCloseRequested -= (EventHandler<TabPage>)handler;
                return true;
            default:
                return false;
        }
    }
}
