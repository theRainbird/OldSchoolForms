using System.Globalization;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Runtime.Localization;
using OldSchoolForms.Ui.Runtime.Modules;
using OldSchoolForms.Ui.Runtime.Sources;
using MessageBox = OldSchoolForms.Ui.MessageBox;

namespace OldSchoolForms.Ui.Runtime.Hosting;

/// <summary>
/// Host services callable from JavaScript (<c>ctx.host</c>): dialogs,
/// status bar, navigation, and sandboxed file access.
/// </summary>
public sealed class HostServices
{
    private readonly IAppSource _source;
    private ToolStripStatusLabel? _statusLabel;
    private readonly string _sandboxDir;

    /// <summary>
    /// Creates the host services.
    /// </summary>
    /// <param name="source">The package source (for read access).</param>
    /// <param name="appName">The app name.</param>
    /// <param name="appVersion">The app version.</param>
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

    /// <summary>The app name.</summary>
    public string AppName { get; }

    /// <summary>The app version.</summary>
    public string AppVersion { get; }

    /// <summary>The localization service (set after construction, may be <c>null</c>).</summary>
    public RuntimeI18n? I18n { get; set; }

    /// <summary>The module host for navigation (set after construction).</summary>
    public ModuleHost? ModuleHost { get; set; }

    /// <summary>The cultures available as i18n bundles in the app package.</summary>
    public IReadOnlyList<string> AvailableCultures => I18n?.AvailableCultures ?? new[] { "en" };

    /// <summary>
    /// Switches the UI culture at runtime and persists the choice.
    /// Raises <see cref="LocalizationManager.CultureChanged"/> so the shell and
    /// the open modules can re-localize themselves.
    /// </summary>
    /// <param name="cultureName">The culture name (e.g. <c>"de"</c> or <c>"en"</c>).</param>
    /// <exception cref="ArgumentException">The culture name is empty.</exception>
    /// <exception cref="CultureNotFoundException">The culture name is invalid.</exception>
    public void SetCulture(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        LocalizationManager.SetCulture(new CultureInfo(cultureName));
        LocalizationManager.SaveCurrentCulture();
    }

    /// <summary>
    /// Attaches the status bar label.
    /// </summary>
    /// <param name="label">The <see cref="ToolStripStatusLabel"/>.</param>
    public void AttachStatusLabel(ToolStripStatusLabel? label) => _statusLabel = label;

    /// <summary>
    /// Shows a message box and returns the result as a string.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <param name="caption">The title (default: app name).</param>
    /// <param name="buttons">Buttons: <c>ok</c>, <c>okcancel</c>, <c>yesno</c>, <c>yesnocancel</c>.</param>
    /// <param name="icon">Icon: <c>none</c>, <c>info</c>, <c>warning</c>, <c>error</c>, <c>question</c>.</param>
    /// <returns><c>"ok"</c>, <c>"cancel"</c>, <c>"yes"</c>, <c>"no"</c>, or similar.</returns>
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
    /// Sets the status bar text.
    /// </summary>
    /// <param name="text">The new status text.</param>
    public void SetStatus(string text)
    {
        if (_statusLabel != null)
            _statusLabel.Text = text;
    }

    /// <summary>
    /// Opens a module (navigation).
    /// </summary>
    /// <param name="id">The module ID.</param>
    public void OpenModule(string id) => ModuleHost?.OpenModule(id);

    /// <summary>
    /// Closes a module.
    /// </summary>
    /// <param name="id">The module ID.</param>
    public void CloseModule(string id) => ModuleHost?.CloseModule(id);

    /// <summary>
    /// Loads a text file from the app package.
    /// </summary>
    /// <param name="relativePath">Relative path to the package root.</param>
    /// <returns>The file content.</returns>
    public string ReadText(string relativePath) => _source.ReadText(relativePath);

    /// <summary>
    /// Checks whether a file exists in the package.
    /// </summary>
    /// <param name="relativePath">Relative path to the package root.</param>
    /// <returns><see langword="true"/> if present.</returns>
    public bool HasFile(string relativePath) => _source.Exists(relativePath);

    /// <summary>
    /// Writes a text file to the app's sandbox data folder.
    /// </summary>
    /// <param name="relativePath">Relative path within the sandbox.</param>
    /// <param name="content">The file content.</param>
    public void WriteText(string relativePath, string content)
    {
        var full = ResolveSandbox(relativePath);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(full, content, System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Reads a text file from the sandbox.
    /// </summary>
    /// <param name="relativePath">Relative path within the sandbox.</param>
    /// <returns>The content, or <c>null</c> if not present.</returns>
    public string? ReadSandboxText(string relativePath)
    {
        var full = ResolveSandbox(relativePath);
        return File.Exists(full) ? File.ReadAllText(full, System.Text.Encoding.UTF8) : null;
    }

    /// <summary>
    /// Logs a message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Log(string message) => Console.WriteLine($"[host] {message}");

    private string ResolveSandbox(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException(LangRes.GetEnglish("Runtime_PathEmpty"), nameof(relativePath));

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(_sandboxDir, normalized));
        var rootWithSep = _sandboxDir.EndsWith(Path.DirectorySeparatorChar)
            ? _sandboxDir
            : _sandboxDir + Path.DirectorySeparatorChar;

        if (!full.StartsWith(rootWithSep, StringComparison.Ordinal))
            throw new UnauthorizedAccessException(string.Format(LangRes.GetEnglish("Runtime_SandboxPathViolation"), relativePath));

        return full;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
