using System;
using System.Linq;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Designer.Services;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

public class ToolboxSearchIconTests
{
    private sealed class NoIconTestControl : Control
    {
    }

    [Fact]
    public void ToolboxItem_PicksUpIconFromToolboxIconAttribute()
    {
        var item = new ToolboxItem("Button", "Basic", typeof(Button), new Size(120, 40));

        Assert.Equal("OldSchoolForms.Ui.Resources.Icons.Controls.ctrl_button.svg",
            item.IconResourceName);
    }

    [Fact]
    public void ToolboxItem_WithoutAttribute_HasNoIconResource()
    {
        var item = new ToolboxItem("NoIcon", "Basic", typeof(NoIconTestControl), new Size(100, 30));

        Assert.Null(item.IconResourceName);
        Assert.Null(item.GetIcon());
    }

    [Fact]
    public void GetIcon_LoadsSvgImage_AndCachesInstance()
    {
        var item = new ToolboxItem("Button", "Basic", typeof(Button), new Size(120, 40));

        var first = item.GetIcon();
        var second = item.GetIcon();

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void AllStandardControls_HaveIconResource()
    {
        var service = new ToolboxService();

        var withoutIcon = service.Items.Where(i => string.IsNullOrEmpty(i.IconResourceName)).ToList();

        Assert.Empty(withoutIcon);
        Assert.All(service.Items, i => Assert.NotNull(i.GetIcon()));
    }

    [Fact]
    public void SearchText_FiltersByDisplayName_CaseInsensitive()
    {
        var control = new ToolboxControl(new ToolboxService());

        control.SearchText = "but";
        Assert.Contains(control.FilteredItems, i => i.DisplayName == "Button");
        Assert.All(control.FilteredItems,
            i => Assert.Contains("but", i.DisplayName, StringComparison.OrdinalIgnoreCase));

        control.SearchText = "BUTTON";
        Assert.Contains(control.FilteredItems, i => i.DisplayName == "Button");
        Assert.All(control.FilteredItems,
            i => Assert.Contains("button", i.DisplayName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EmptySearchText_ShowsAllItems()
    {
        var service = new ToolboxService();
        var control = new ToolboxControl(service);

        control.SearchText = "box";
        Assert.NotEmpty(control.FilteredItems);
        Assert.True(control.FilteredItems.Count < service.Items.Count);

        control.SearchText = string.Empty;
        Assert.Equal(service.Items.Count, control.FilteredItems.Count);
    }

    [Fact]
    public void SearchText_NoMatch_YieldsEmptyList()
    {
        var control = new ToolboxControl(new ToolboxService());

        control.SearchText = "zzz_no_such_control_zzz";

        Assert.Empty(control.FilteredItems);
    }

    [Fact]
    public void CategoryAndSearch_CombineFilters()
    {
        var control = new ToolboxControl(new ToolboxService());

        control.FilterByCategory("Basic");
        control.SearchText = "box";

        Assert.NotEmpty(control.FilteredItems);
        Assert.All(control.FilteredItems, i => Assert.Equal("Basic", i.Category));
        Assert.All(control.FilteredItems,
            i => Assert.Contains("box", i.DisplayName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SearchText_SetterNull_NormalizesToEmpty()
    {
        var service = new ToolboxService();
        var control = new ToolboxControl(service);

        control.SearchText = null!;

        Assert.Equal(string.Empty, control.SearchText);
        Assert.Equal(service.Items.Count, control.FilteredItems.Count);
    }
}
