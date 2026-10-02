using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Modules;
using OldSchoolForms.Ui.Runtime.Sources;
using MessageBox = OldSchoolForms.Ui.MessageBox;

namespace OldSchoolForms.Ui.Runtime.Hosting;

/// <summary>
/// Die von JavaScript aus aufrufbaren Host-Dienste (<c>ctx.host</c>): Dialoge,
/// Statusleiste, Navigation und sandboxed Dateizugriff.
/// </summary>
public sealed class HostServices
{
    private readonly IAppSource _source;
    private ToolStripStatusLabel? _statusLabel;
    private readonly string _sandboxDir;

    /// <summary>
    /// Erzeugt die Host-Services.
    /// </summary>
    /// <param name="source">Die Paketquelle (für Lesezugriff).</param>
    /// <param name="appName">Der App-Name.</param>
    /// <param name="appVersion">Die App-Version.</param>
    public HostServices(IAppSource source, string appName, string appVersion)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        AppName = appName;
        AppVersion = appVersion;
        _sandboxDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OldSchoolForms.Ui.Runtime",
            Sanitize(appName));
    }

    /// <summary>Der App-Name.</summary>
    public string AppName { get; }

    /// <summary>Die App-Version.</summary>
    public string AppVersion { get; }

    /// <summary>Der Modul-Host für Navigation (wird nach der Konstruktion gesetzt).</summary>
    public ModuleHost? ModuleHost { get; set; }

    /// <summary>
    /// Verknüpft das Statusleisten-Label.
    /// </summary>
    /// <param name="label">Das <see cref="ToolStripStatusLabel"/>.</param>
    public void AttachStatusLabel(ToolStripStatusLabel? label) => _statusLabel = label;

    /// <summary>
    /// Zeigt eine Messagebox und liefert das Ergebnis als String.
    /// </summary>
    /// <param name="text">Die Meldung.</param>
    /// <param name="caption">Der Titel (Standard: App-Name).</param>
    /// <param name="buttons">Buttons: <c>ok</c>, <c>okcancel</c>, <c>yesno</c>, <c>yesnocancel</c>.</param>
    /// <param name="icon">Icon: <c>none</c>, <c>info</c>, <c>warning</c>, <c>error</c>, <c>question</c>.</param>
    /// <returns><c>"ok"</c>, <c>"cancel"</c>, <c>"yes"</c>, <c>"no"</c> o. ä.</returns>
    public string ShowMessageBox(string text, string? caption = null, string buttons = "ok", string icon = "none")
    {
        var msgButtons = buttons.ToLowerInvariant() switch
        {
            "okcancel" => MessageBoxButtons.OKCancel,
            "yesno" => MessageBoxButtons.YesNo,
            "yesnocancel" => MessageBoxButtons.YesNoCancel,
            _ => MessageBoxButtons.OK
        };

        var msgIcon = icon.ToLowerInvariant() switch
        {
            "info" => MessageBoxIcon.Information,
            "warning" => MessageBoxIcon.Warning,
            "error" => MessageBoxIcon.Error,
            "question" => MessageBoxIcon.Question,
            _ => MessageBoxIcon.None
        };

        var result = MessageBox.Show(text, caption ?? AppName, msgButtons, msgIcon);
        return result.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Setzt den Text der Statusleiste.
    /// </summary>
    /// <param name="text">Der neue Statustext.</param>
    public void SetStatus(string text)
    {
        if (_statusLabel != null)
            _statusLabel.Text = text;
    }

    /// <summary>
    /// Öffnet ein Modul (Navigation).
    /// </summary>
    /// <param name="id">Die Modul-ID.</param>
    public void OpenModule(string id) => ModuleHost?.OpenModule(id);

    /// <summary>
    /// Schließt ein Modul.
    /// </summary>
    /// <param name="id">Die Modul-ID.</param>
    public void CloseModule(string id) => ModuleHost?.CloseModule(id);

    /// <summary>
    /// Lädt eine Textdatei aus dem App-Paket.
    /// </summary>
    /// <param name="relativePath">Relativer Pfad zur Paketwurzel.</param>
    /// <returns>Der Dateiinhalt.</returns>
    public string ReadText(string relativePath) => _source.ReadText(relativePath);

    /// <summary>
    /// Prüft, ob eine Datei im Paket existiert.
    /// </summary>
    /// <param name="relativePath">Relativer Pfad zur Paketwurzel.</param>
    /// <returns><see langword="true"/>, wenn vorhanden.</returns>
    public bool FileExists(string relativePath) => _source.Exists(relativePath);

    /// <summary>
    /// Schreibt eine Textdatei in den Sandbox-Datenordner der App.
    /// </summary>
    /// <param name="relativePath">Relativer Pfad innerhalb der Sandbox.</param>
    /// <param name="content">Der Dateiinhalt.</param>
    public void WriteText(string relativePath, string content)
    {
        var full = ResolveSandbox(relativePath);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(full, content, System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Liest eine Textdatei aus der Sandbox.
    /// </summary>
    /// <param name="relativePath">Relativer Pfad innerhalb der Sandbox.</param>
    /// <returns>Der Inhalt oder <c>null</c>, wenn nicht vorhanden.</returns>
    public string? ReadSandboxText(string relativePath)
    {
        var full = ResolveSandbox(relativePath);
        return File.Exists(full) ? File.ReadAllText(full, System.Text.Encoding.UTF8) : null;
    }

    /// <summary>
    /// Loggt eine Nachricht.
    /// </summary>
    /// <param name="message">Die Nachricht.</param>
    public void Log(string message) => Console.WriteLine($"[host] {message}");

    private string ResolveSandbox(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Pfad darf nicht leer sein.", nameof(relativePath));

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(_sandboxDir, normalized));
        var rootWithSep = _sandboxDir.EndsWith(Path.DirectorySeparatorChar)
            ? _sandboxDir
            : _sandboxDir + Path.DirectorySeparatorChar;

        if (!full.StartsWith(rootWithSep, StringComparison.Ordinal))
            throw new UnauthorizedAccessException($"Sandbox-Pfad verletzt: {relativePath}");

        return full;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
