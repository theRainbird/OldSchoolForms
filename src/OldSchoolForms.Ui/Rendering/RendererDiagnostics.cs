using System.Text;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Rendering;

/// <summary>
/// Temporary diagnostics for the rendering pipeline, enabled through the
/// OSF_SKIA_DIAG environment variable (1 or true). Writes timestamped entries
/// to osf_skia_diag.log in the temp directory. All methods are no-ops when the
/// flag is not set and never throw, so diagnostics cannot break rendering.
/// </summary>
internal static class RendererDiagnostics
{
    private static readonly bool EnabledValue = DetectEnabled();
    private static readonly object Gate = new();
    private static readonly HashSet<string> ReportedTypefaces = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ReportedSkips = new(StringComparer.Ordinal);
    private static int _frameCounter;

    /// <summary>
    /// Gets or sets the font used to draw the on-screen frame stamp. Gets
    /// self-seeded from the first rendered text command (which is known to
    /// resolve to a real typeface on the current machine); falls back to
    /// <see cref="Core.Font.Default"/>.
    /// </summary>
    public static Core.Font? StampFont;

    /// <summary>
    /// Gets whether renderer diagnostics are enabled (OSF_SKIA_DIAG=1|true).
    /// </summary>
    public static bool Enabled => EnabledValue;

    private static bool DetectEnabled()
    {
        try
        {
            var value = Environment.GetEnvironmentVariable("OSF_SKIA_DIAG");
            if (string.IsNullOrEmpty(value)) return false;
            var normalized = value.Trim();
            return normalized == "1" || normalized.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Appends a timestamped line to the diagnostics log file.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Log(string message)
    {
        if (!Enabled) return;
        try
        {
            lock (Gate)
            {
                var line = $"{DateTime.Now:HH:mm:ss.fff} [T{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}";
                var path = Path.Combine(Path.GetTempPath(), "osf_skia_diag.log");
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never interfere with rendering.
        }
    }

    private static DateTime _lastThrottledLog;

    /// <summary>
    /// Logs at most once per second regardless of call frequency; the first call
    /// always logs. Used for spammy render-loop diagnostics like gate decisions.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void LogThrottled(string message)
    {
        if (!Enabled) return;
        var now = DateTime.Now;
        if ((now - _lastThrottledLog).TotalMilliseconds < 1000) return;
        _lastThrottledLog = now;
        Log(message);
    }

    private static DateTime _lastFrameLog;

    /// <summary>
    /// Bumps the global frame counter and returns the new number. Called once
    /// per rendered frame so the on-screen frame stamp and the log line share
    /// the same identifier.
    /// </summary>
    /// <returns>The new frame number.</returns>
    public static int NextFrame() => Interlocked.Increment(ref _frameCounter);

    /// <summary>
    /// Builds the on-screen frame stamp text (frame number + clock). This is
    /// drawn into the overlay of every frame so the user can verify visually
    /// whether the GL presentation path actually reaches the screen.
    /// </summary>
    /// <param name="frame">The current frame number.</param>
    /// <returns>The stamp text, e.g. "F42 17:55:53".</returns>
    public static string FrameStamp(int frame) => $"F{frame} {DateTime.Now:HH:mm:ss}";

    /// <summary>
    /// Logs a per-frame summary at most once per second, plus immediately for
    /// interesting states (no commands at all, or any text dropped due to a
    /// null font). This yields continuous coverage of the paint pipeline with
    /// a bounded log size.
    /// </summary>
    /// <param name="frame">The frame number from <see cref="NextFrame"/>.</param>
    /// <param name="title">The window title for identification.</param>
    /// <param name="commands">The number of draw commands in the frame.</param>
    /// <param name="rightCmds">The number of commands in the property grid zone (right 30 %).</param>
    /// <param name="gridTexts">The number of text commands in the property grid zone.</param>
    /// <param name="surfaceTexts">The number of text commands in the left zone (surface/toolbox).</param>
    /// <param name="skippedNullFont">The number of DrawString commands whose font was null.</param>
    /// <param name="skippedEmptyText">The number of DrawString commands whose text was null or empty.</param>
    public static void LogFrame(int frame, string title, int commands, int rightCmds, int gridTexts,
        int surfaceTexts, int skippedNullFont, int skippedEmptyText)
    {
        if (!Enabled) return;
        var now = DateTime.Now;
        var immediate = skippedNullFont > 0 || commands == 0 || frame % 250 == 0;
        if (!immediate && (now - _lastFrameLog).TotalMilliseconds < 1000) return;
        _lastFrameLog = now;
        Log($"Frame#{frame} window='{title}' commands={commands} rightCmds={rightCmds} gridTexts={gridTexts} surfaceTexts={surfaceTexts} skipFont={skippedNullFont} skipEmpty={skippedEmptyText}");
    }

    /// <summary>
    /// Logs a skipped DrawString command once per distinct (kind, text, position).
    /// Skips with a non-empty text mean dropped glyphs; skips with an empty text
    /// are harmless.
    /// </summary>
    /// <param name="text">The skipped text (may be empty).</param>
    /// <param name="x">The x-coordinate of the command.</param>
    /// <param name="y">The y-coordinate of the command.</param>
    /// <param name="fontNull">Whether the command font was null.</param>
    public static void LogSkippedText(string text, float x, float y, bool fontNull)
    {
        if (!Enabled) return;
        var key = $"f={fontNull} p=({x:0},{y:0}) t={(text.Length <= 40 ? text : text[..40])}";
        lock (Gate)
        {
            if (!ReportedSkips.Add(key)) return;
        }
        Log($"DrawString SKIPPED fontNull={fontNull} at=({x:0},{y:0}) text='{text}'");
    }

    /// <summary>
    /// Logs a failed typeface resolution once per font name/style combination.
    /// Failed resolution silently drops all text of that font.
    /// </summary>
    /// <param name="fontName">The font family name that failed to resolve.</param>
    /// <param name="style">The requested font style.</param>
    public static void LogTypefaceMissing(string fontName, string style)
    {
        if (!Enabled) return;
        var key = fontName + "/" + style;
        lock (Gate)
        {
            if (!ReportedTypefaces.Add(key)) return;
        }
        Log($"Fonts: typeface resolve FAILED '{fontName}' style={style} -> text will be missing");
    }
}
