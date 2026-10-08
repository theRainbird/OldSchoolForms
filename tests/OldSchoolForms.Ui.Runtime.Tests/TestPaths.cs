namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Helpers for locating the sample app package (<c>samples/RuntimeExampleApp</c>)
/// starting from the test assembly directory.
/// </summary>
internal static class TestPaths
{
    /// <summary>Directory of the sample app.</summary>
    public static string SampleApp { get; } = Find();

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "RuntimeExampleApp");
            if (File.Exists(Path.Combine(candidate, "app.json")))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Sample app 'samples/RuntimeExampleApp' was not found starting from " +
            AppContext.BaseDirectory + ".");
    }
}
