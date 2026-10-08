using OldSchoolForms.Server.Runtime.Manifest;
using Xunit;

namespace OldSchoolForms.Server.Runtime.Tests;

public class ManifestTests
{
    [Fact]
    public void Load_ParsesAllSections()
    {
        using var pkg = new TestPackage();
        pkg.WriteDefaultPackage();

        var manifest = ServerManifest.Load(pkg.Root);

        Assert.Equal("TestServer", manifest.Name);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal("127.0.0.1", manifest.Host);
        Assert.Equal(19555, manifest.Port);
        Assert.False(manifest.Encryption);
        Assert.Equal(3, manifest.Services.Count);
        Assert.Equal("math", manifest.Services[0].Id);
        Assert.Equal("singleton", manifest.Services[0].Lifetime);
        Assert.Equal("singlecall", manifest.Services[2].Lifetime);
        Assert.NotNull(manifest.Shared);
        Assert.Equal("shared", manifest.Shared!.Dir);
        Assert.Equal("1.0.0", manifest.Shared.Version);
    }

    [Fact]
    public void Load_MissingManifest_Throws()
    {
        using var pkg = new TestPackage();
        Assert.Throws<InvalidOperationException>(() => ServerManifest.Load(pkg.Root));
    }

    [Fact]
    public void Load_ServiceWithoutId_Throws()
    {
        using var pkg = new TestPackage();
        pkg.Write("server.json", """
        { "name": "X", "services": [ { "script": "a.js" } ] }
        """);
        Assert.Throws<InvalidOperationException>(() => ServerManifest.Load(pkg.Root));
    }
}
