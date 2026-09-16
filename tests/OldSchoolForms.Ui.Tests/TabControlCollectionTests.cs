using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using Xunit;

namespace OldSchoolForms.Ui.Tests;

/// <summary>
/// Tests that the TabControl page-collection operations keep the tab pages and the
/// internal control collection in sync and clamp the selected index.
/// </summary>
public class TabControlCollectionTests
{
    [Fact]
    public void AddTabPage_SyncsTabPagesAndControls_AndDisablesTabStop()
    {
        var tab = new TabControl();
        var page = new TabPage { Name = "p1", Text = "One" };

        tab.AddTabPage(page);

        Assert.Single(tab.TabPages);
        Assert.Single(tab.Controls);
        Assert.Same(page, tab.TabPages[0]);
        Assert.Same(page, tab.Controls[0]);
        Assert.False(page.TabStop);
    }

    [Fact]
    public void AddTabPage_ThrowsOnNullAndIgnoresDuplicates()
    {
        var tab = new TabControl();

        TabPage page = null!;
        Assert.Throws<ArgumentNullException>(() => tab.AddTabPage(page));

        var p1 = new TabPage { Text = "P1" };
        tab.AddTabPage(p1);
        tab.AddTabPage(p1);

        Assert.Single(tab.TabPages);
    }

    [Fact]
    public void SelectedIndex_ClampsOutOfRange()
    {
        var tab = new TabControl();
        tab.AddTabPage(new TabPage { Text = "A" });

        tab.SelectedIndex = 5;
        Assert.Equal(0, tab.SelectedIndex);

        tab.SelectedIndex = -1;
        Assert.Equal(0, tab.SelectedIndex);
    }

    [Fact]
    public void RemoveTabPage_KeepsControlsInSync()
    {
        var tab = new TabControl();
        var a = new TabPage { Text = "A" };
        var b = new TabPage { Text = "B" };
        tab.AddTabPage(a);
        tab.AddTabPage(b);
        tab.SelectedIndex = 1;

        tab.RemoveTabPage(a);

        Assert.Single(tab.TabPages);
        Assert.Single(tab.Controls);
        Assert.Same(b, tab.TabPages[0]);
        Assert.Equal(0, tab.SelectedIndex);
    }

    [Fact]
    public void RemoveTabPage_LastPage_ResetsSelection()
    {
        var tab = new TabControl();
        var a = new TabPage { Text = "A" };
        tab.AddTabPage(a);

        tab.RemoveTabPage(a);

        Assert.Empty(tab.TabPages);
        Assert.Empty(tab.Controls);
        Assert.Equal(0, tab.SelectedIndex);
    }

    [Fact]
    public void Insert_SyncsOrder_AndControls_AndBumpsSelection()
    {
        var tab = new TabControl();
        var a = new TabPage();
        var mid = new TabPage();
        var b = new TabPage();
        tab.AddTabPage(a);
        tab.AddTabPage(b);
        tab.SelectedIndex = 1;

        tab.Insert(1, mid);

        Assert.Equal(3, tab.TabPages.Count);
        Assert.Same(mid, tab[1]);
        Assert.Same(mid, tab.Controls[1]);
        Assert.Equal(2, tab.SelectedIndex);
    }

    [Fact]
    public void TryHitTabHeaderArea_ReturnsTabIndexWhenOverTab()
    {
        var tab = new TabControl { Size = new Size(400, 200) };
        tab.AddTabPage(new TabPage { Text = "A" });
        tab.AddTabPage(new TabPage { Text = "B" });

        Assert.True(tab.TryHitTabHeaderAreaPublic(new Point(10, 5), out int idx0));
        Assert.Equal(0, idx0);

        Assert.True(tab.TryHitTabHeaderAreaPublic(new Point(60, 5), out int idx1));
        Assert.Equal(1, idx1);
    }

    [Fact]
    public void TryHitTabHeaderArea_ReturnsFalseWhenOutsideHeader()
    {
        var tab = new TabControl { Size = new Size(400, 200) };
        tab.AddTabPage(new TabPage { Text = "A" });

        Assert.False(tab.TryHitTabHeaderAreaPublic(new Point(10, 50), out _));
        Assert.False(tab.TryHitTabHeaderAreaPublic(new Point(-1, 5), out _));
        Assert.False(tab.TryHitTabHeaderAreaPublic(new Point(400, 5), out _));
    }

    [Fact]
    public void DesignerMouseDown_SwitchesTabViaBridge()
    {
        var tab = new TabControl { Size = new Size(400, 200) };
        tab.AddTabPage(new TabPage { Text = "A" });
        tab.AddTabPage(new TabPage { Text = "B" });

        var args = new MouseEventArgs(MouseButtons.Left, 1, 55, 5, 0);
        tab.DesignerMouseDown(args);

        Assert.Equal(1, tab.SelectedIndex);
    }
}
