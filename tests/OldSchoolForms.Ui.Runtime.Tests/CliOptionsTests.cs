using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Tests für das Parsen der Kommandozeilenoptionen.
/// </summary>
public class CliOptionsTests
{
    [Fact]
    public void Parse_EmptyArgs_AllDefaults()
    {
        var o = CliOptions.Parse(Array.Empty<string>());
        Assert.Null(o.PositionalPath);
        Assert.Null(o.Source);
        Assert.False(o.Dev);
        Assert.False(o.ShowHelp);
    }

    [Fact]
    public void Parse_PositionalPath()
    {
        var o = CliOptions.Parse(new[] { "./app" });
        Assert.Equal("./app", o.PositionalPath);
    }

    [Fact]
    public void Parse_DevFlag()
    {
        var o = CliOptions.Parse(new[] { "--dev" });
        Assert.True(o.Dev);
    }

    [Fact]
    public void Parse_SourceSeparate()
    {
        var o = CliOptions.Parse(new[] { "--source", "https://host/app" });
        Assert.Equal("https://host/app", o.Source);
    }

    [Fact]
    public void Parse_SourceEqualsForm()
    {
        var o = CliOptions.Parse(new[] { "--source=https://host/app" });
        Assert.Equal("https://host/app", o.Source);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Parse_Help(string flag)
    {
        var o = CliOptions.Parse(new[] { flag });
        Assert.True(o.ShowHelp);
    }

    [Fact]
    public void Parse_PathPlusDev()
    {
        var o = CliOptions.Parse(new[] { "/checkout/app", "--dev" });
        Assert.Equal("/checkout/app", o.PositionalPath);
        Assert.True(o.Dev);
    }

    [Fact]
    public void Parse_OnlyFirstPositionalCaptured()
    {
        var o = CliOptions.Parse(new[] { "first", "second" });
        Assert.Equal("first", o.PositionalPath);
    }
}
