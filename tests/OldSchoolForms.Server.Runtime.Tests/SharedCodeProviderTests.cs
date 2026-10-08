using System.Security.Cryptography;
using System.Text;
using OldSchoolForms.Server.Runtime.Manifest;
using OldSchoolForms.Server.Runtime.SharedCode;
using Xunit;

namespace OldSchoolForms.Server.Runtime.Tests;

public class SharedCodeProviderTests
{
    private static SharedCodeProvider CreateProvider(TestPackage pkg) =>
        new(pkg.Root, new SharedDescriptor { Dir = "shared", Version = "1.0.0" }, "TestServer");

    [Fact]
    public void GetManifest_ListsFilesWithHashes()
    {
        using var pkg = new TestPackage();
        pkg.WriteDefaultPackage();
        var provider = CreateProvider(pkg);

        var manifest = provider.GetManifest();

        Assert.Equal("TestServer", manifest.AppName);
        Assert.Single(manifest.Files);
        var file = manifest.Files[0];
        Assert.Equal("validation/customers.js", file.Path);
        Assert.Null(file.Content);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(pkg.Root, "shared/validation/customers.js")))).ToLowerInvariant(),
            file.Sha256);
        Assert.True(file.Size > 0);
    }

    [Fact]
    public void GetFile_ReturnsContent()
    {
        using var pkg = new TestPackage();
        pkg.WriteDefaultPackage();
        var provider = CreateProvider(pkg);

        var file = provider.GetFile("validation/customers.js", null);

        Assert.Contains("validateName", file.Content);
        Assert.Equal("1.0.0", file.Version);
    }

    [Fact]
    public void GetFile_TraversalRejected()
    {
        using var pkg = new TestPackage();
        pkg.WriteDefaultPackage();
        pkg.Write("secret.txt", "top secret");
        var provider = CreateProvider(pkg);

        Assert.Throws<InvalidOperationException>(() => provider.GetFile("../secret.txt", null));
        Assert.Throws<InvalidOperationException>(() => provider.GetFile("validation/../../secret.txt", null));
        Assert.Throws<InvalidOperationException>(() => provider.GetFile("", null));
    }

    [Fact]
    public void GetFile_DisallowedExtensionRejected()
    {
        using var pkg = new TestPackage();
        pkg.WriteDefaultPackage();
        pkg.Write("shared/evil.dll", "binary");
        var provider = CreateProvider(pkg);

        Assert.Throws<InvalidOperationException>(() => provider.GetFile("evil.dll", null));
    }

    [Fact]
    public void GetManifest_MissingDir_ReturnsEmpty()
    {
        using var pkg = new TestPackage();
        pkg.Write("server.json", """{ "name": "X" }""");
        var provider = CreateProvider(pkg);

        var manifest = provider.GetManifest();
        Assert.Empty(manifest.Files);
    }
}
