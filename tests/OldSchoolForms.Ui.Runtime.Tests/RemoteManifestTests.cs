using OldSchoolForms.Ui.Runtime.Sources;
using Xunit;

namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Tests für das Parsen des Server-Manifests (<c>manifest.json</c>).
/// </summary>
public class RemoteManifestTests
{
    [Fact]
    public void Parse_FullManifest()
    {
        const string json = """
        {
          "name": "Firmen-App",
          "version": "2.1.0",
          "entry": "app.js",
          "shell": { "url": "shell.zip", "sha256": "abc123" },
          "modules": [
            { "id": "customers", "version": "1.0.0", "url": "modules/customers.zip", "sha256": "def456" },
            { "id": "orders",    "version": "1.2.0", "url": "modules/orders.zip" }
          ]
        }
        """;

        var m = RemoteManifest.Parse(json);

        Assert.Equal("Firmen-App", m.Name);
        Assert.Equal("2.1.0", m.Version);
        Assert.Equal("app.js", m.Entry);
        Assert.Equal("shell.zip", m.Shell.Url);
        Assert.Equal("abc123", m.Shell.Sha256);

        Assert.Equal(2, m.Modules.Count);
        Assert.Equal("customers", m.Modules[0].Id);
        Assert.Equal("1.0.0", m.Modules[0].Version);
        Assert.Equal("modules/customers.zip", m.Modules[0].Url);
        Assert.Equal("def456", m.Modules[0].Sha256);
        Assert.Null(m.Modules[1].Sha256);
    }

    [Fact]
    public void Parse_MinimalManifest_UsesDefaults()
    {
        var m = RemoteManifest.Parse("{}");
        Assert.Equal("OldSchoolForms App", m.Name);
        Assert.Equal("0.0.0", m.Version);
        Assert.Null(m.Entry);
        Assert.Empty(m.Modules);
    }

    [Fact]
    public void Parse_CaseInsensitiveKeys()
    {
        const string json = """{ "Name": "X", "Version": "9.9.9", "Shell": { "Url": "s.zip" } }""";
        var m = RemoteManifest.Parse(json);
        Assert.Equal("X", m.Name);
        Assert.Equal("9.9.9", m.Version);
        Assert.Equal("s.zip", m.Shell.Url);
    }

    [Fact]
    public void Parse_NonObjectRoot_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RemoteManifest.Parse("[1,2,3]"));
    }
}
