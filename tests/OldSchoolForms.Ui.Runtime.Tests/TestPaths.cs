namespace OldSchoolForms.Ui.Runtime.Tests;

/// <summary>
/// Hilfen zum Auffinden des Sample-App-Pakets (<c>samples/RuntimeSampleApp</c>)
/// ausgehend vom Test-Assembly-Verzeichnis.
/// </summary>
internal static class TestPaths
{
    /// <summary>Verzeichnis der Sample-App.</summary>
    public static string SampleApp { get; } = Find();

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", "RuntimeSampleApp");
            if (File.Exists(Path.Combine(candidate, "app.json")))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Sample-App 'samples/RuntimeSampleApp' wurde ausgehend von " +
            AppContext.BaseDirectory + " nicht gefunden.");
    }
}
