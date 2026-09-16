using System;
using System.Linq;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Exposes the protected mouse-event handlers so the designer's adapter routing
/// can be exercised headlessly without a real input stack.
/// </summary>
public class ControlDesignAdapterTests
{
    private static MouseEventArgs LeftMouseDown(int x, int y)
        => new(MouseButtons.Left, 1, x, y, 0f);

    private static MouseEventArgs LeftMouseUp(int x, int y)
        => new(MouseButtons.Left, 1, x, y, 0f);

    private sealed class TestableDesignSurface : DesignSurface
    {
        public void RaiseMouseDown(MouseEventArgs e) => OnMouseDown(e);
        public void RaiseMouseMove(MouseEventArgs e) => OnMouseMove(e);
        public void RaiseMouseUp(MouseEventArgs e) => OnMouseUp(e);
    }

    private static TabControl CreateTabControl(int width = 400, int tabCount = 2)
    {
        var tc = new TabControl { Size = new Size(width, 200) };
        for (int i = 0; i < tabCount; i++)
            tc.AddTabPage(new TabPage { Text = $"Tab {i}" });
        return tc;
    }

    [Fact]
    public void Registry_RegisterAndGetAdapter_ReturnsCorrectAdapter()
    {
        var registry = new ControlDesignAdapterRegistry();
        registry.Register<TabControl>(c => new TabControlDesignAdapter(c));

        var tc = new TabControl();
        var adapter = registry.GetAdapter(tc);
        Assert.IsType<TabControlDesignAdapter>(adapter);

        var button = new Button();
        Assert.Null(registry.GetAdapter(button));
    }

    [Fact]
    public void Adapter_TryHitRegion_ReturnsTrueForHeader()
    {
        var tc = CreateTabControl();
        var adapter = new TabControlDesignAdapter(tc);

        Assert.True(adapter.TryHitRegion(new Point(10, 5)));
        Assert.False(adapter.TryHitRegion(new Point(10, 50)));
    }

    [Fact]
    public void Adapter_OnMouseDown_SwitchesTab()
    {
        var tc = CreateTabControl(tabCount: 2);
        var adapter = new TabControlDesignAdapter(tc);

        adapter.OnMouseDown(new Point(10, 5), LeftMouseDown(10, 5));
        Assert.Equal(0, tc.SelectedIndex);

        adapter.OnMouseDown(new Point(60, 5), LeftMouseDown(60, 5));
        Assert.Equal(1, tc.SelectedIndex);
    }

    [Fact]
    public void Adapter_OnKeyDown_F4OpensOverflow()
    {
        var tc = CreateTabControl(width: 120, tabCount: 5);
        var adapter = new TabControlDesignAdapter(tc);

        tc.DesignerMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 5, 0));

        var keyF4 = new KeyEventArgs { KeyCode = Keys.F4 };
        Assert.True(adapter.OnKeyDown(keyF4));
        Assert.True(tc.OverflowDroppedDown);
    }

    [Fact]
    public void DesignSurface_TabClick_SwitchesTab()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var tc = CreateTabControl();
        surface.AddControl(tc, new Point(30, 30));

        surface.RaiseMouseDown(LeftMouseDown(30 + 60, 30 + 5));
        surface.RaiseMouseUp(LeftMouseUp(30 + 60, 30 + 5));

        Assert.Equal(1, tc.SelectedIndex);
    }

    [Fact]
    public void DesignSurface_TabClick_SelectsTabControl()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var tc = CreateTabControl();
        surface.AddControl(tc, new Point(30, 30));

        surface.RaiseMouseDown(LeftMouseDown(30 + 60, 30 + 5));
        surface.RaiseMouseUp(LeftMouseUp(30 + 60, 30 + 5));

        var selected = surface.Selection.SelectedItems;
        Assert.Single(selected);
        Assert.Same(tc, selected[0].Control);
    }

    [Fact]
    public void DesignSurface_OverflowButtonClick_OpensDropdown()
    {
        var surface = new TestableDesignSurface { SnapToGrid = false };
        var tc = CreateTabControl(width: 120, tabCount: 5);
        surface.AddControl(tc, new Point(30, 30));

        // Der Overflow-Button liegt rechts im Header (Surface: 30 + Width - 8)
        var btnX = 30 + tc.Width - 8;
        surface.RaiseMouseDown(LeftMouseDown(btnX, 30 + 5));
        Assert.True(tc.OverflowDroppedDown);
    }
}
