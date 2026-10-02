using OldSchoolForms.Ui.Runtime.Scripting;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Tests für die Pfadnormalisierung und -auflösung von ES-Modul-Spezifizierern.
/// </summary>
public class ModulePathTests
{
    [Fact]
    public void Normalize_KeepsSimplePath()
    {
        Assert.Equal("modules/customers/index.js", ModulePath.Normalize("modules/customers/index.js"));
    }

    [Fact]
    public void Normalize_ResolvesDotSegments()
    {
        Assert.Equal("a/b", ModulePath.Normalize("a/./b"));
        Assert.Equal("a/c", ModulePath.Normalize("a/b/../c"));
    }

    [Fact]
    public void Normalize_ConvertsBackslashes()
    {
        Assert.Equal("a/b/c", ModulePath.Normalize("a\\b\\c"));
    }

    [Fact]
    public void Normalize_ThrowsWhenEscapingRoot()
    {
        Assert.Throws<InvalidOperationException>(() => ModulePath.Normalize("a/../../b"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("./")]
    public void Normalize_ThrowsOnEmpty(string path)
    {
        Assert.Throws<InvalidOperationException>(() => ModulePath.Normalize(path));
    }

    [Fact]
    public void ResolveRelative_SiblingModule()
    {
        var key = ModulePath.ResolveRelative("modules/customers/index.js", "./util.js");
        Assert.Equal("modules/customers/util.js", key);
    }

    [Fact]
    public void ResolveRelative_ParentDirectory()
    {
        var key = ModulePath.ResolveRelative("modules/customers/index.js", "../shared/util.js");
        Assert.Equal("modules/shared/util.js", key);
    }

    [Fact]
    public void ResolveRelative_AppPrefixIsRooted()
    {
        var key = ModulePath.ResolveRelative("modules/customers/index.js", "@app/shared/util.js");
        Assert.Equal("shared/util.js", key);
    }

    [Fact]
    public void ResolveRelative_BareSpecifierIsRooted()
    {
        var key = ModulePath.ResolveRelative("modules/customers/index.js", "shared/util.js");
        Assert.Equal("shared/util.js", key);
    }

    [Fact]
    public void ResolveRelative_FromRootReferencer()
    {
        var key = ModulePath.ResolveRelative("", "shared/util.js");
        Assert.Equal("shared/util.js", key);
    }

    [Fact]
    public void GetDirectory_ReturnsParentFolder()
    {
        Assert.Equal("modules/customers", ModulePath.GetDirectory("modules/customers/index.js"));
    }

    [Fact]
    public void GetDirectory_RootFileIsEmpty()
    {
        Assert.Equal(string.Empty, ModulePath.GetDirectory("app.js"));
    }
}
