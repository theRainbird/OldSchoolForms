using System.Globalization;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Localization;
using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Unit tests for the reusable <see cref="RuntimeI18n"/> localization service:
/// bundle loading, the culture fallback chain, placeholder formatting and
/// <c>tr:</c> reference resolution.
/// </summary>
public class RuntimeI18nTests : IDisposable
{
    private readonly string _root;

    public RuntimeI18nTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "osf-i18n-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "i18n"));
        File.WriteAllText(Path.Combine(_root, "i18n", "de.json"),
            """{ "Hello": "Hallo", "Welcome": "Willkommen, {0}!" }""");
        File.WriteAllText(Path.Combine(_root, "i18n", "en.json"),
            """{ "Hello": "Hello", "Only_En": "English only" }""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private RuntimeI18n Create() => new(new FileSystemAppSource(_root), "i18n", "en");

    [Fact]
    public void T_ResolvesCurrentCulture()
    {
        var i18n = Create();
        LocalizationManager.SetCulture(new CultureInfo("de"));
        Assert.Equal("Hallo", i18n.T("Hello"));
    }

    [Fact]
    public void T_FallsBackToDefaultCultureWhenKeyMissing()
    {
        var i18n = Create();
        LocalizationManager.SetCulture(new CultureInfo("de"));
        Assert.Equal("English only", i18n.T("Only_En"));
    }

    [Fact]
    public void T_ReturnsKeyWhenUnknown()
    {
        var i18n = Create();
        LocalizationManager.SetCulture(new CultureInfo("de"));
        Assert.Equal("Nope", i18n.T("Nope"));
    }

    [Fact]
    public void T_FormatsPlaceholders()
    {
        var i18n = Create();
        LocalizationManager.SetCulture(new CultureInfo("de"));
        Assert.Equal("Willkommen, Anna!", i18n.T("Welcome", "Anna"));
    }

    [Fact]
    public void Resolve_TranslatesTrPrefixAndKeepsPlainStrings()
    {
        var i18n = Create();
        LocalizationManager.SetCulture(new CultureInfo("de"));
        Assert.Equal("Hallo", i18n.Resolve("tr:Hello"));
        Assert.Equal("plain text", i18n.Resolve("plain text"));
        Assert.Equal(string.Empty, i18n.Resolve(null));
    }

    [Fact]
    public void IsReference_DetectsPrefix()
    {
        Assert.True(RuntimeI18n.IsReference("tr:Foo"));
        Assert.False(RuntimeI18n.IsReference("Foo"));
        Assert.False(RuntimeI18n.IsReference(null));
    }

    [Fact]
    public void Supports_ChecksBundlePresence()
    {
        var i18n = Create();
        Assert.True(i18n.Supports("de"));
        Assert.True(i18n.Supports("de-DE"));
        Assert.False(i18n.Supports("fr"));
    }

    [Fact]
    public void AvailableCultures_ProbesBundles()
    {
        var i18n = Create();
        Assert.Contains("de", i18n.AvailableCultures);
        Assert.Contains("en", i18n.AvailableCultures);
    }

    [Fact]
    public void Locale_ReflectsCurrentCulture()
    {
        var i18n = Create();
        LocalizationManager.SetCulture(new CultureInfo("en"));
        Assert.Equal("en", i18n.Locale);
    }
}
