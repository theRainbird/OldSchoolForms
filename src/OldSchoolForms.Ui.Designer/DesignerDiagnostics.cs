using System;
using System.IO;
using System.Text;

namespace OldSchoolForms.Ui.Designer;

/// <summary>
/// Lightweight opt-in file diagnostics used to capture the exact designer state during
/// the report-control placement/selection/property-grid flow. Enabled by setting the
/// environment variable <c>OSF_DESIGNER_DIAG=1</c>. When disabled, <see cref="Log"/>
/// is a no-op so production runs are unaffected.
/// </summary>
internal static class DesignerDiagnostics
{
    private static readonly object _gate = new();
    private static readonly string? _path;

    static DesignerDiagnostics()
    {
        var enabled = Environment.GetEnvironmentVariable("OSF_DESIGNER_DIAG");
        if (string.Equals(enabled, "1", StringComparison.Ordinal) ||
            string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.Combine(Path.GetTempPath());
            _path = Path.Combine(dir, "osf_designer_diag.log");
            try
            {
                File.AppendAllText(_path,
                    $"==== designer diagnostics session {DateTime.Now:O} ===={Environment.NewLine}");
            }
            catch
            {
                _path = null;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether diagnostics are enabled.
    /// </summary>
    public static bool Enabled => _path != null;

    /// <summary>
    /// Appends a diagnostic line to the log file when enabled.
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    public static void Log(string message)
    {
        if (_path == null) return;
        try
        {
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}";
            lock (_gate)
            {
                File.AppendAllText(_path, line);
            }
        }
        catch
        {
            // Diagnostics must never break the designer.
        }
    }

    /// <summary>
    /// Appends a diagnostic line together with the current managed stack trace when
    /// enabled. Used to reveal the exact caller that mutates designer state (for
    /// example, what empties the selection).
    /// </summary>
    /// <param name="message">The diagnostic message.</param>
    public static void LogStack(string message)
    {
        if (_path == null) return;
        try
        {
            var stack = Environment.StackTrace;
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}{stack}{Environment.NewLine}----{Environment.NewLine}";
            lock (_gate)
            {
                File.AppendAllText(_path, line);
            }
        }
        catch
        {
            // Diagnostics must never break the designer.
        }
    }

    /// <summary>
    /// Describes a control for diagnostics (type, name, bounds, parent type).
    /// </summary>
    /// <param name="control">The control to describe.</param>
    /// <returns>A short diagnostic description.</returns>
    public static string Describe(object? control)
    {
        if (control == null) return "<null>";
        if (control is OldSchoolForms.Ui.Core.Control c)
        {
            var parent = c.Parent?.GetType().Name ?? "<none>";
            return $"{c.GetType().Name} name='{c.Name}' bounds=({c.X},{c.Y},{c.Width},{c.Height}) parent={parent}";
        }
        return control.GetType().Name;
    }

    private static readonly System.Collections.Generic.Dictionary<object, string> _proxyPaintSummaries = new();

    /// <summary>
    /// Logs a report-control proxy paint summary, only when the summary for the proxy
    /// instance changes. Reveals whether the WYSIWYG offscreen render produced visible
    /// commands (caption present) or came out empty/failed (blank proxy on the surface).
    /// </summary>
    /// <param name="proxy">The design report control proxy being painted.</param>
    /// <param name="renderError">The render exception message, or null on success.</param>
    /// <param name="offscreenCommands">Number of commands the offscreen render produced.</param>
    /// <param name="text">The wrapped label's text when the proxy wraps a label; otherwise null.</param>
    /// <param name="pageZoom">The page zoom used for the command import.</param>
    /// <param name="visible">Whether the proxy is marked visible.</param>
    public static void LogProxyPaint(object proxy, string? renderError, int offscreenCommands,
        string? text, float pageZoom, bool visible)
    {
        if (_path == null) return;
        var summary =
            $"proxy={Describe(proxy)} offscreenCommands={offscreenCommands} " +
            $"text='{text}' zoom={pageZoom} visible={visible} " +
            $"{(!string.IsNullOrEmpty(renderError) ? "err=" + renderError : "paint=ok")}";
        lock (_gate)
        {
            if (_proxyPaintSummaries.TryGetValue(proxy, out var last) &&
                string.Equals(last, summary, StringComparison.Ordinal))
            {
                return;
            }
            _proxyPaintSummaries[proxy] = summary;
        }
        Log($"DesignReportControl.Paint {summary}");
    }
}
