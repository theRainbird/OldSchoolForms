using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Controls.Basic;
using OldSchoolForms.Ui.Controls.Containers;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer;
using OldSchoolForms.Ui.Designer.Dialogs;
using OldSchoolForms.Ui.Designer.PropertyGrid;
using OldSchoolForms.Ui.Designer.Roslyn;
using OldSchoolForms.Ui.Designer.Serialization;
using OldSchoolForms.Ui.Layout;
using Xunit;

namespace OldSchoolForms.Ui.Designer.Tests;

/// <summary>
/// Verifies the TableLayoutPanel row/column support that backs the PropertyGrid
/// "Edit rows and columns" editor: the shared <see cref="TableLayoutCalculator"/>
/// sizing model, the <see cref="TableLayoutRowsColumnsEditor"/> property matching and
/// registration, and that row/column styles survive both the JSON layout round-trip and
/// the generated-C# round-trip.
/// </summary>
public class TableLayoutPanelEditorTests
{
    // ---- Calculator: the sizing model shared by the panel and the preview ----

    [Fact]
    public void Calculator_NoStyles_EqualDistribution()
    {
        var sizes = TableLayoutCalculator.ComputeSizes(null, 4, 200f, null);

        Assert.Equal(new[] { 50f, 50f, 50f, 50f }, sizes);
    }

    [Fact]
    public void Calculator_AbsolutePlusUnstyled_UnstyledShareRemainder()
    {
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.Absolute, 30f)
        };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 3, 100f, null);

        Assert.Equal(30f, sizes[0]);
        Assert.Equal(35f, sizes[1]);
        Assert.Equal(35f, sizes[2]);
    }

    [Fact]
    public void Calculator_PercentWeights_SplitByWeight()
    {
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.Percent, 25f),
            new TableLayoutStyle(SizeType.Percent, 75f)
        };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 2, 200f, null);

        Assert.Equal(50f, sizes[0]);
        Assert.Equal(150f, sizes[1]);
    }

    [Fact]
    public void Calculator_AutoSize_UsesPreferredSize()
    {
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.AutoSize),
            new TableLayoutStyle(SizeType.Percent, 50f)
        };
        var autoSizes = new[] { 40f, 0f };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 2, 100f, autoSizes);

        // AutoSize takes its content (40); the 50% row takes 50% of the remaining
        // 60 (= 30). The other 30 stays unallocated (no fill row to claim it).
        Assert.Equal(40f, sizes[0]);
        Assert.Equal(30f, sizes[1]);
    }

    [Fact]
    public void Calculator_MixedAbsolutePercent_AbsoluteFirstThenPercent()
    {
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.Absolute, 20f),
            new TableLayoutStyle(SizeType.Percent, 50f),
            new TableLayoutStyle(SizeType.Percent, 50f)
        };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 3, 100f, null);

        Assert.Equal(20f, sizes[0]);
        Assert.Equal(40f, sizes[1]);
        Assert.Equal(40f, sizes[2]);
    }

    [Fact]
    public void Calculator_UnstyledSharesLeftoverAfterPercent()
    {
        // One styled percent (2% of the space) plus two unstyled indices that share
        // the remaining 98% equally.
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.Percent, 2f)
        };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 3, 100f, null);

        Assert.Equal(2f, sizes[0]);
        Assert.Equal(49f, sizes[1]);
        Assert.Equal(49f, sizes[2]);
    }

    [Fact]
    public void Calculator_FillTakesRemainderAfterPercent()
    {
        // 25% + 25% + Fill => the fill row gets the remaining 50%.
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.Percent, 25f),
            new TableLayoutStyle(SizeType.Percent, 25f),
            new TableLayoutStyle(SizeType.Fill)
        };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 3, 200f, null);

        Assert.Equal(50f, sizes[0]);
        Assert.Equal(50f, sizes[1]);
        Assert.Equal(100f, sizes[2]);
    }

    [Fact]
    public void Calculator_AbsoluteOverflow_ClampsRemainderToZero()
    {
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.Absolute, 150f)
        };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 2, 100f, null);

        Assert.Equal(150f, sizes[0]);
        Assert.Equal(0f, sizes[1]);
    }

    [Fact]
    public void Calculator_PercentWithNoRemaining_IsZero()
    {
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.Absolute, 100f),
            new TableLayoutStyle(SizeType.Percent, 50f)
        };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 2, 100f, null);

        Assert.Equal(100f, sizes[0]);
        Assert.Equal(0f, sizes[1]);
    }

    [Fact]
    public void Calculator_SingleFill_TakesAllRemaining()
    {
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.Absolute, 40f),
            new TableLayoutStyle(SizeType.Fill)
        };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 2, 200f, null);

        Assert.Equal(40f, sizes[0]);
        Assert.Equal(160f, sizes[1]);
    }

    [Fact]
    public void Calculator_MultipleFills_ShareRemainingEqually()
    {
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.Fill),
            new TableLayoutStyle(SizeType.Fill),
            new TableLayoutStyle(SizeType.Fill)
        };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 3, 300f, null);

        Assert.Equal(100f, sizes[0]);
        Assert.Equal(100f, sizes[1]);
        Assert.Equal(100f, sizes[2]);
    }

    [Fact]
    public void Calculator_FillWithAutoSize_AutoFirstFillTakesRest()
    {
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.AutoSize),
            new TableLayoutStyle(SizeType.Fill)
        };
        var autoSizes = new[] { 60f, 0f };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 2, 250f, autoSizes);

        Assert.Equal(60f, sizes[0]);
        Assert.Equal(190f, sizes[1]);
    }

    [Fact]
    public void Calculator_FillIgnoresValue_WeightIsOne()
    {
        // A Fill entry uses weight 1 regardless of its stored Value.
        var styles = new List<TableLayoutStyle>
        {
            new TableLayoutStyle(SizeType.Fill, 999f),
            new TableLayoutStyle(SizeType.Fill)
        };

        var sizes = TableLayoutCalculator.ComputeSizes(styles, 2, 100f, null);

        Assert.Equal(50f, sizes[0]);
        Assert.Equal(50f, sizes[1]);
    }

    // ---- Effective counts ----

    [Fact]
    public void EffectiveCount_IsMaxOfCountAndStyles()
    {
        var table = new TableLayoutPanel { RowCount = 2, ColumnCount = 2 };
        Assert.Equal(2, table.EffectiveRowCount);
        Assert.Equal(2, table.EffectiveColumnCount);

        table.RowStyles.Add(new TableLayoutStyle(SizeType.Percent, 50f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Percent, 50f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Percent, 50f));
        Assert.Equal(3, table.EffectiveRowCount);

        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        Assert.Equal(3, table.EffectiveColumnCount);
    }

    // ---- Editor matching ----

    [Fact]
    public void Editor_HandlesRowAndColumnStyles_Only()
    {
        var editor = new TableLayoutRowsColumnsEditor();
        Assert.Equal("…", editor.ButtonText);

        var rowStyles = typeof(TableLayoutPanel).GetProperty(nameof(TableLayoutPanel.RowStyles))!;
        var columnStyles = typeof(TableLayoutPanel).GetProperty(nameof(TableLayoutPanel.ColumnStyles))!;
        var rowCount = typeof(TableLayoutPanel).GetProperty(nameof(TableLayoutPanel.RowCount))!;
        var tabPages = typeof(TabControl).GetProperty(nameof(TabControl.TabPages))!;

        Assert.True(editor.Handles(rowStyles));
        Assert.True(editor.Handles(columnStyles));
        Assert.False(editor.Handles(rowCount));
        Assert.False(editor.Handles(tabPages));
    }

    [Fact]
    public void PropertyService_ExposesRowStylesWithEditorAndLayoutCategory()
    {
        var service = new PropertyService();
        var table = new TableLayoutPanel();

        var descriptor = service.GetProperties(table).FirstOrDefault(p => p.Name == nameof(TableLayoutPanel.RowStyles));

        Assert.NotNull(descriptor);
        Assert.NotNull(descriptor!.Editor);
        Assert.Equal("Layout", descriptor.Category);
    }

    // ---- JSON round-trip ----

    [Fact]
    public void JsonRoundTrip_PreservesRowAndColumnStyles()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();

        var table = new TableLayoutPanel { Name = "table1", Size = new Size(300, 200) };
        surface.AddControl(table, new Point(20, 20));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 40f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Percent, 60f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Fill));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Percent, 100f));

        var service = new JsonDocumentService("My.App", "MainForm");
        string json = service.SaveToJson(surface);

        var loaded = new DesignSurface { SnapToGrid = false };
        service.LoadFromJson(loaded, json);

        var reloaded = loaded.Items.Select(i => i.Control).OfType<TableLayoutPanel>().Single();

        Assert.Equal(3, reloaded.RowStyles.Count);
        Assert.Equal(SizeType.Absolute, reloaded.RowStyles[0].SizeType);
        Assert.Equal(40f, reloaded.RowStyles[0].Value);
        Assert.Equal(SizeType.Percent, reloaded.RowStyles[1].SizeType);
        Assert.Equal(60f, reloaded.RowStyles[1].Value);
        Assert.Equal(SizeType.Fill, reloaded.RowStyles[2].SizeType);

        Assert.Equal(2, reloaded.ColumnStyles.Count);
        Assert.Equal(SizeType.AutoSize, reloaded.ColumnStyles[0].SizeType);
        Assert.Equal(SizeType.Percent, reloaded.ColumnStyles[1].SizeType);
        Assert.Equal(100f, reloaded.ColumnStyles[1].Value);
    }

    // ---- C# round-trip ----

    [Fact]
    public void CodeRoundTrip_PreservesRowAndColumnStyles()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();

        var table = new TableLayoutPanel { Name = "table1", Size = new Size(300, 200) };
        surface.AddControl(table, new Point(20, 20));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 40f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Percent, 60f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Fill));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Percent, 100f));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(code, loaded);

        var reloaded = loaded.Items.Select(i => i.Control).OfType<TableLayoutPanel>().Single();

        Assert.Equal(3, reloaded.RowStyles.Count);
        Assert.Equal(SizeType.Absolute, reloaded.RowStyles[0].SizeType);
        Assert.Equal(40f, reloaded.RowStyles[0].Value);
        Assert.Equal(SizeType.Percent, reloaded.RowStyles[1].SizeType);
        Assert.Equal(60f, reloaded.RowStyles[1].Value);
        Assert.Equal(SizeType.Fill, reloaded.RowStyles[2].SizeType);

        Assert.Equal(2, reloaded.ColumnStyles.Count);
        Assert.Equal(SizeType.AutoSize, reloaded.ColumnStyles[0].SizeType);
        Assert.Equal(SizeType.Percent, reloaded.ColumnStyles[1].SizeType);
        Assert.Equal(100f, reloaded.ColumnStyles[1].Value);
    }

    [Fact]
    public void CodeGenerator_EmitsTableLayoutStyleAddCalls()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();

        var table = new TableLayoutPanel { Name = "table1" };
        surface.AddControl(table, new Point(20, 20));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 40f));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Percent, 50f));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.Contains("table1.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 40f));", code);
        Assert.Contains("table1.ColumnStyles.Add(new TableLayoutStyle(SizeType.Percent, 50f));", code);
    }

    // ---- Cell positions ----

    [Fact]
    public void SetCellPosition_RoundTripsThroughGetCellPosition()
    {
        var table = new TableLayoutPanel { RowCount = 3, ColumnCount = 3 };
        var child = new Button { Name = "btn1" };
        table.Controls.Add(child);

        table.SetCellPosition(child, 2, 1);

        var cell = table.GetCellPosition(child);
        Assert.NotNull(cell);
        Assert.Equal(2, cell!.Value.Row);
        Assert.Equal(1, cell.Value.Col);
    }

    [Fact]
    public void GetCellPosition_ReturnsNull_WhenNotAssigned()
    {
        var table = new TableLayoutPanel();
        var child = new Button { Name = "btn1" };
        table.Controls.Add(child);

        Assert.Null(table.GetCellPosition(child));
    }

    [Fact]
    public void EffectiveCount_GrowsWithCellAssignment()
    {
        var table = new TableLayoutPanel { RowCount = 2, ColumnCount = 2 };
        var child = new Button { Name = "btn1" };
        table.Controls.Add(child);

        table.SetCellPosition(child, 4, 3);

        Assert.Equal(5, table.EffectiveRowCount);
        Assert.Equal(4, table.EffectiveColumnCount);
    }

    [Fact]
    public void AssignCells_ExplicitPositionWins_FillOrderFillsGaps()
    {
        var table = new TableLayoutPanel { RowCount = 2, ColumnCount = 2 };
        var a = new Button { Name = "a" };
        var b = new Button { Name = "b" };
        table.Controls.Add(a);
        table.Controls.Add(b);

        // b explicitly in cell (0,0); a has no assignment so it fills the first free
        // cell in row-major order, which is (0,1).
        table.SetCellPosition(b, 0, 0);

        var cells = table.AssignCells(2, 2);
        Assert.Equal((0, 0), cells[1]!.Value); // b
        Assert.Equal((0, 1), cells[0]!.Value); // a
    }

    [Fact]
    public void HitTestCell_ReturnsCellUnderPoint()
    {
        var table = new TableLayoutPanel { RowCount = 2, ColumnCount = 2, Size = new Size(204, 204) };
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));

        // Grid starts at Padding.Left+2 = 2. Point (50,50) is in cell (0,0);
        // point (150,150) is in cell (1,1).
        Assert.Equal((0, 0), table.HitTestCell(new Point(50, 50))!.Value);
        Assert.Equal((1, 1), table.HitTestCell(new Point(150, 150))!.Value);
    }

    [Fact]
    public void HitTestCell_ReturnsNull_OutsideGrid()
    {
        var table = new TableLayoutPanel { RowCount = 2, ColumnCount = 2, Size = new Size(204, 204) };
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));

        // Point in the padding area (before the grid) is outside.
        Assert.Null(table.HitTestCell(new Point(0, 0)));
    }

    // ---- AutoEmptyMinSize (empty Auto rows/columns stay clickable) ----

    [Fact]
    public void AutoEmptyMinSize_EmptyAutoRow_KeepsMinimumHeight()
    {
        var table = new TableLayoutPanel { RowCount = 1, ColumnCount = 1, Size = new Size(200, 200) };
        table.RowStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.AutoEmptyMinSize = 30f;

        var (autoColumns, autoRows) = table.ComputeAutoSizes(1, 1);

        Assert.Equal(30f, autoRows[0]);
    }

    [Fact]
    public void AutoColumn_OccupiedByResizedChild_UsesActualSize()
    {
        var table = new TableLayoutPanel { RowCount = 1, ColumnCount = 1, Size = new Size(400, 400) };
        table.RowStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.AutoSize));

        // The child's actual Size (as resized in the designer) drives the auto cell,
        // not the (stale) PreferredSize. With the default CellSpacing of zero the auto
        // cell is exactly the child's size, so the child fills the cell edge to edge.
        var child = new Button { Name = "btn1", Size = new Size(150, 100), PreferredSize = new Size(80, 50) };
        table.Controls.Add(child);
        table.SetCellPosition(child, 0, 0);

        var (autoColumns, autoRows) = table.ComputeAutoSizes(1, 1);

        Assert.Equal(150f, autoColumns[0]);
        Assert.Equal(100f, autoRows[0]);
    }

    [Fact]
    public void ChildResize_TriggersParentAutoLayout()
    {
        var table = new TableLayoutPanel { RowCount = 1, ColumnCount = 1, Size = new Size(400, 400) };
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        var child = new Button { Name = "btn1", Size = new Size(100, 50) };
        table.Controls.Add(child);
        table.SetCellPosition(child, 0, 0);
        table.PerformLayout();
        Assert.Equal(100, child.Width); // cell 100, child fills it exactly (no gap)

        // User resizes the button in the designer; the parent must re-layout so the
        // auto column grows with the content (the OnBoundsChanged parent propagation).
        child.Size = new Size(150, 100);

        Assert.Equal(150f, table.ComputeGridSizes().Widths[0]);
        Assert.Equal(150, child.Width);
    }

    [Fact]
    public void AutoLayout_StableAcrossMultiplePasses()
    {
        var table = new TableLayoutPanel { RowCount = 1, ColumnCount = 1, Size = new Size(400, 400) };
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        var child = new Button { Name = "btn1", Size = new Size(150, 100) };
        table.Controls.Add(child);
        table.SetCellPosition(child, 0, 0);

        table.PerformLayout();
        var w1 = child.Width;
        table.PerformLayout();
        var w2 = child.Width;
        table.PerformLayout();
        var w3 = child.Width;

        Assert.Equal(150, w1);
        Assert.Equal(w1, w2);
        Assert.Equal(w2, w3); // no per-pass shrink
    }

    [Fact]
    public void AutoEmptyMinSize_SetterClampsNegative()
    {
        var table = new TableLayoutPanel { AutoEmptyMinSize = 30f };
        table.AutoEmptyMinSize = -5f;
        Assert.Equal(0f, table.AutoEmptyMinSize);
    }

    // ---- CellSpacing (content-to-cell-edge distance) ----

    [Fact]
    public void CellSpacing_Default_IsEmpty()
    {
        var table = new TableLayoutPanel();

        Assert.Equal(0, table.CellSpacing.Left);
        Assert.Equal(0, table.CellSpacing.Top);
        Assert.Equal(0, table.CellSpacing.Right);
        Assert.Equal(0, table.CellSpacing.Bottom);
    }

    [Fact]
    public void CellSpacing_Default_ChildFillsCellWithoutRightOrBottomGap()
    {
        // The reported bug: content ended 4px short of the right and bottom grid line.
        // With the default spacing the child must touch all four grid lines.
        var table = new TableLayoutPanel { RowCount = 2, ColumnCount = 2, Size = new Size(204, 204) };
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        var child = new Button { Name = "btn1" };
        table.Controls.Add(child);
        table.SetCellPosition(child, 0, 0);

        table.PerformLayout();

        // Grid starts at Padding.Left+2 = 2; cell (0,0) spans 2..102 in both axes.
        Assert.Equal(2, child.X);
        Assert.Equal(2, child.Y);
        Assert.Equal(100, child.Width);
        Assert.Equal(100, child.Height);
        Assert.Equal(102, child.X + child.Width);  // touches the right grid line
        Assert.Equal(102, child.Y + child.Height);  // touches the bottom grid line
    }

    [Fact]
    public void CellSpacing_NonZero_InsetsChildFromAllFourGridLines()
    {
        var table = new TableLayoutPanel { RowCount = 1, ColumnCount = 1, Size = new Size(204, 204) };
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        var child = new Button { Name = "btn1" };
        table.Controls.Add(child);
        table.SetCellPosition(child, 0, 0);

        table.CellSpacing = new Padding(3, 4, 5, 6);

        // Cell spans 2..102; content inset 3/4/5/6 from the grid lines.
        Assert.Equal(2 + 3, child.X);
        Assert.Equal(2 + 4, child.Y);
        Assert.Equal(100 - 3 - 5, child.Width);
        Assert.Equal(100 - 4 - 6, child.Height);
        Assert.Equal(5, 102 - (child.X + child.Width)); // right gap == CellSpacing.Right
        Assert.Equal(6, 102 - (child.Y + child.Height)); // bottom gap == CellSpacing.Bottom
    }

    [Fact]
    public void CellSpacing_AutoSize_KeepsChildAtOwnSize()
    {
        var table = new TableLayoutPanel { RowCount = 1, ColumnCount = 1, Size = new Size(400, 400) };
        table.RowStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.CellSpacing = new Padding(2);
        var child = new Button { Name = "btn1", Size = new Size(150, 100), PreferredSize = new Size(80, 50) };
        table.Controls.Add(child);
        table.SetCellPosition(child, 0, 0);

        var (autoColumns, autoRows) = table.ComputeAutoSizes(1, 1);

        Assert.Equal(154f, autoColumns[0]); // 150 + 2*2 spacing
        Assert.Equal(104f, autoRows[0]);    // 100 + 2*2 spacing
    }

    [Fact]
    public void CellSpacing_SetterClampsNegativeSidesToZero()
    {
        var table = new TableLayoutPanel();

        table.CellSpacing = new Padding(-5, -1, 3, -2);

        Assert.Equal(0, table.CellSpacing.Left);
        Assert.Equal(0, table.CellSpacing.Top);
        Assert.Equal(3, table.CellSpacing.Right);
        Assert.Equal(0, table.CellSpacing.Bottom);
    }

    [Fact]
    public void PropertyService_ExposesCellSpacingWithLayoutCategoryAndEditor()
    {
        var service = new PropertyService();
        var table = new TableLayoutPanel();

        var descriptor = service.GetProperties(table).FirstOrDefault(p => p.Name == nameof(TableLayoutPanel.CellSpacing));

        Assert.NotNull(descriptor);
        Assert.Equal("Layout", descriptor!.Category);
        Assert.NotNull(descriptor.Editor);
    }

    [Fact]
    public void PropertyGrid_SetCellSpacing_InsetsChildInLivePanel()
    {
        // Simulates the user flow: the PropertyGrid writes a Padding through the
        // descriptor and the live panel must re-layout its children with the inset.
        var table = new TableLayoutPanel { RowCount = 1, ColumnCount = 1, Size = new Size(204, 204) };
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        var child = new Button { Name = "btn1" };
        table.Controls.Add(child);
        table.SetCellPosition(child, 0, 0);
        table.PerformLayout();
        Assert.Equal(100, child.Width);

        var service = new PropertyService();
        var descriptor = service.GetProperties(table).First(p => p.Name == nameof(TableLayoutPanel.CellSpacing));

        descriptor.SetValue(new Padding(4));

        Assert.Equal(4, table.CellSpacing.Left);
        Assert.Equal(6, child.X); // 2 (border) + 4 (spacing)
        Assert.Equal(92, child.Width);
        Assert.Equal(92, child.Height);
    }

    [Fact]
    public void JsonRoundTrip_PreservesCellSpacing()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();

        var table = new TableLayoutPanel { Name = "table1", Size = new Size(300, 200) };
        surface.AddControl(table, new Point(20, 20));
        table.CellSpacing = new Padding(1, 2, 3, 4);

        var service = new JsonDocumentService("My.App", "MainForm");
        string json = service.SaveToJson(surface);

        var loaded = new DesignSurface { SnapToGrid = false };
        service.LoadFromJson(loaded, json);

        var reloaded = loaded.Items.Select(i => i.Control).OfType<TableLayoutPanel>().Single();

        Assert.Equal(1, reloaded.CellSpacing.Left);
        Assert.Equal(2, reloaded.CellSpacing.Top);
        Assert.Equal(3, reloaded.CellSpacing.Right);
        Assert.Equal(4, reloaded.CellSpacing.Bottom);
    }

    [Fact]
    public void CodeRoundTrip_PreservesCellSpacing()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();

        var table = new TableLayoutPanel { Name = "table1", Size = new Size(300, 200) };
        surface.AddControl(table, new Point(20, 20));
        table.CellSpacing = new Padding(1, 2, 3, 4);

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");
        Assert.Contains("table1.CellSpacing = new Padding(1, 2, 3, 4);", code);

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(code, loaded);

        var reloaded = loaded.Items.Select(i => i.Control).OfType<TableLayoutPanel>().Single();

        Assert.Equal(1, reloaded.CellSpacing.Left);
        Assert.Equal(2, reloaded.CellSpacing.Top);
        Assert.Equal(3, reloaded.CellSpacing.Right);
        Assert.Equal(4, reloaded.CellSpacing.Bottom);
    }

    [Fact]
    public void CodeGenerator_OmitsCellSpacing_WhenDefault()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();

        var table = new TableLayoutPanel { Name = "table1", Size = new Size(300, 200) };
        surface.AddControl(table, new Point(20, 20));

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");

        Assert.DoesNotContain("CellSpacing", code);
    }

    // ---- Cell position round-trips ----

    [Fact]
    public void JsonRoundTrip_PreservesCellPositions()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();

        var table = new TableLayoutPanel { Name = "table1", Size = new Size(300, 200) };
        surface.AddControl(table, new Point(20, 20));
        var child = new Button { Name = "btn1" };
        surface.AddControl(child, table, new Point(10, 10));
        table.SetCellPosition(child, 1, 0);

        var service = new JsonDocumentService("My.App", "MainForm");
        string json = service.SaveToJson(surface);

        var loaded = new DesignSurface { SnapToGrid = false };
        service.LoadFromJson(loaded, json);

        var reloadedTable = loaded.Items.Select(i => i.Control).OfType<TableLayoutPanel>().Single();
        var reloadedChild = reloadedTable.Controls.OfType<Button>().Single();

        var cell = reloadedTable.GetCellPosition(reloadedChild);
        Assert.NotNull(cell);
        Assert.Equal(1, cell!.Value.Row);
        Assert.Equal(0, cell.Value.Col);
    }

    // ---- Bug repros ----

    [Fact]
    public void CodeRoundTrip_PreservesCellPositions()
    {
        var surface = new DesignSurface { SnapToGrid = false };
        surface.BeginDesignForm();

        var table = new TableLayoutPanel { Name = "table1", Size = new Size(300, 200) };
        surface.AddControl(table, new Point(20, 20));
        var child = new Button { Name = "btn1" };
        surface.AddControl(child, table, new Point(10, 10));
        table.SetCellPosition(child, 1, 0);

        string code = new CodeGenerator(surface).Generate("Demo", "MainForm");
        Assert.Contains("table1.SetCellPosition(btn1, 1, 0);", code);

        var loaded = new DesignSurface { SnapToGrid = false };
        new FormParser().Parse(code, loaded);

        var reloadedTable = loaded.Items.Select(i => i.Control).OfType<TableLayoutPanel>().Single();
        var reloadedChild = reloadedTable.Controls.OfType<Button>().Single();

        var cell = reloadedTable.GetCellPosition(reloadedChild);
        Assert.NotNull(cell);
        Assert.Equal(1, cell!.Value.Row);
        Assert.Equal(0, cell.Value.Col);
    }

    [Fact]
    public void Bug3_AutoEmptyMinSize_ChangesEmptyAutoRowHeight()
    {
        var table = new TableLayoutPanel { RowCount = 1, ColumnCount = 1, Size = new Size(200, 200) };
        table.RowStyles.Add(new TableLayoutStyle(SizeType.AutoSize));

        table.AutoEmptyMinSize = 24f;
        var h1 = table.ComputeGridSizes().Heights[0];

        table.AutoEmptyMinSize = 100f;
        var h2 = table.ComputeGridSizes().Heights[0];

        Assert.Equal(24f, h1, 1);
        Assert.Equal(100f, h2, 1);
    }

    [Fact]
    public void Bug1_DialogApplyChanges_PersistsStyles_AndReopenSeedsThem()
    {
        var table = new TableLayoutPanel { RowCount = 2, ColumnCount = 2 };

        var dialog = new TableLayoutRowsColumnsEditorDialog(table);
        var rowWorking = (List<TableLayoutStyle>)ReadPrivateField(dialog, "_rowWorking")!;
        rowWorking.Clear();
        rowWorking.Add(new TableLayoutStyle(SizeType.AutoSize));
        rowWorking.Add(new TableLayoutStyle(SizeType.Fill));

        typeof(TableLayoutRowsColumnsEditorDialog)
            .GetMethod("ApplyChanges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(dialog, null);

        Assert.Equal(2, table.RowStyles.Count);
        Assert.Equal(SizeType.AutoSize, table.RowStyles[0].SizeType);
        Assert.Equal(SizeType.Fill, table.RowStyles[1].SizeType);

        // Reopen: the second dialog must seed the persisted styles, not the empty-default.
        var dialog2 = new TableLayoutRowsColumnsEditorDialog(table);
        var rowWorking2 = (List<TableLayoutStyle>)ReadPrivateField(dialog2, "_rowWorking")!;
        Assert.Equal(2, rowWorking2.Count);
        Assert.Equal(SizeType.AutoSize, rowWorking2[0].SizeType);
        Assert.Equal(SizeType.Fill, rowWorking2[1].SizeType);
    }

    private static object? ReadPrivateField(object target, string name)
        => target.GetType()
            .GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(target);

    [Fact]
    public void PropertyGrid_SetAutoEmptyMinSize_GrowsEmptyAutoRow()
    {
        // Simulates the exact user flow: empty AutoSize row, user edits AutoEmptyMinSize
        // through the PropertyGrid descriptor, the live panel must grow the row.
        var table = new TableLayoutPanel { RowCount = 1, ColumnCount = 1, Size = new Size(200, 200) };
        table.RowStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.AutoEmptyMinSize = 24f;
        Assert.Equal(24f, table.ComputeGridSizes().Heights[0], 1);

        var service = new PropertyService();
        var descriptor = service.GetProperties(table).First(p => p.Name == nameof(TableLayoutPanel.AutoEmptyMinSize));

        descriptor.SetValue(100f);

        Assert.Equal(100f, table.AutoEmptyMinSize);
        Assert.Equal(100f, table.ComputeGridSizes().Heights[0], 1);
    }

    [Fact]
    public void AutoEmptyMinSize_GrowsWithMultipleRows_FillsShrink()
    {
        // Realistic designer scenario: Fill + empty AutoSize + Fill. Raising the min
        // must grow the auto row while the fill rows absorb the difference.
        var table = new TableLayoutPanel { RowCount = 3, ColumnCount = 1, Size = new Size(200, 200) };
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Fill));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Fill));

        table.AutoEmptyMinSize = 24f;
        var h1 = table.ComputeGridSizes().Heights;
        Assert.Equal(24f, h1[1], 1);

        table.AutoEmptyMinSize = 100f;
        var h2 = table.ComputeGridSizes().Heights;
        Assert.Equal(100f, h2[1], 1);
        Assert.True(h2[0] < h1[0]);
        Assert.True(h2[2] < h1[2]);
    }

    // ---- Auto cells keep the child's own size (no stretch) ----

    [Fact]
    public void AutoRow_TwoControlsDifferentHeights_EachKeepsOwnHeight()
    {
        // A user-driven scenario: two controls of different heights in the same
        // auto-sized row. The row grows to the tallest child, but each control
        // keeps its own height (it is NOT stretched to the row height).
        var table = new TableLayoutPanel { RowCount = 1, ColumnCount = 2, Size = new Size(210, 200) };
        table.RowStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));

        var b1 = new Button { Name = "b1", Size = new Size(90, 40) };
        var b2 = new Button { Name = "b2", Size = new Size(90, 20) };
        table.Controls.Add(b1);
        table.Controls.Add(b2);
        table.SetCellPosition(b1, 0, 0);
        table.SetCellPosition(b2, 0, 1);

        table.PerformLayout();

        Assert.Equal(40, b1.Height);
        Assert.Equal(20, b2.Height);

        // The row height is the tallest child plus the cell gap. With the default
        // CellSpacing (Padding.Empty = 0) the auto row equals the tallest child.
        var heights = table.ComputeGridSizes().Heights;
        Assert.Equal(40f, heights[0], 1);
    }

    [Fact]
    public void AutoColumn_ControlKeepsOwnWidth()
    {
        // Symmetric case for an auto-sized column: the control keeps its own width.
        var table = new TableLayoutPanel { RowCount = 1, ColumnCount = 1, Size = new Size(200, 200) };
        table.RowStyles.Add(new TableLayoutStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.AutoSize));

        var b = new Button { Name = "b", Size = new Size(75, 30) };
        table.Controls.Add(b);
        table.SetCellPosition(b, 0, 0);

        table.PerformLayout();

        Assert.Equal(75, b.Width);
        Assert.Equal(30, b.Height);
    }

    // ---- Move an existing control into a specific cell (EndDrag logic) ----

    [Fact]
    public void MoveIntoCell_ControlLocationMapsToTargetCell()
    {
        // Simulates the cursor-based cell resolution that DragService.ContinueDrag
        // performs for a control dropped onto a table. HitTestCell is called with a
        // literal point because setting child.Location would itself trigger a layout
        // pass that snaps the control to its fill-order cell before we can read it.
        var table = new TableLayoutPanel { RowCount = 2, ColumnCount = 2, Size = new Size(210, 210) };
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.ColumnStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));
        table.RowStyles.Add(new TableLayoutStyle(SizeType.Absolute, 100f));

        var child = new Button { Name = "c" };
        table.Controls.Add(child);

        // Top-left of cell (1,1): colOffsets[1]=100 + padLeft(2) = 102, same for rows.
        var cell = table.HitTestCell(new Point(102, 102));
        Assert.NotNull(cell);
        Assert.Equal((1, 1), cell!.Value);

        // Persistence: the assigned cell survives a layout pass.
        table.SetCellPosition(child, cell.Value.Row, cell.Value.Col);
        table.PerformLayout();

        var assigned = table.GetCellPosition(child);
        Assert.NotNull(assigned);
        Assert.Equal((1, 1), assigned!.Value);
    }
}
