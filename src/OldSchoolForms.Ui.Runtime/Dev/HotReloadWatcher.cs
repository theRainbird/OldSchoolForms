using System.Collections.Concurrent;

namespace OldSchoolForms.Ui.Runtime.Dev;

/// <summary>
/// Überwacht ein App-Verzeichnis auf Dateiänderungen und löst nach einer
/// Debounce-Phase ein <see cref="Changed"/>-Event pro geänderter Datei aus.
/// Nur im Dev-Modus aktiv.
/// </summary>
public sealed class HotReloadWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _debounce;
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _debounceMs;
    private bool _started;
    private bool _disposed;

    /// <summary>
    /// Erzeugt den Watcher für ein Verzeichnis.
    /// </summary>
    /// <param name="rootDirectory">Das zu überwachende Verzeichnis (rekursiv).</param>
    /// <param name="debounceMs">Debounce-Zeit in Millisekunden.</param>
    public HotReloadWatcher(string rootDirectory, int debounceMs = 300)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Verzeichnis fehlt.", nameof(rootDirectory));

        _debounceMs = debounceMs;
        _watcher = new FileSystemWatcher(rootDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
        };
        _watcher.Changed += OnFsEvent;
        _watcher.Created += OnFsEvent;
        _watcher.Renamed += OnFsEvent;
        _debounce = new Timer(Flush, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Tritt auf, wenn eine Datei geändert wurde (relativer Pfad zur Wurzel).
    /// </summary>
    public event Action<string>? Changed;

    /// <summary>
    /// Startet die Überwachung.
    /// </summary>
    public void Start()
    {
        if (_started || _disposed) return;
        _started = true;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;
        var path = e.FullPath.Replace('\\', '/');
        if (path.Contains("/bin/") || path.Contains("/obj/") || path.Contains("/.git/"))
            return;

        _pending[path] = 0;
        _debounce.Change(_debounceMs, Timeout.Infinite);
    }

    private void Flush(object? state)
    {
        if (_disposed) return;
        var snapshot = _pending.Keys.ToList();
        _pending.Clear();
        foreach (var path in snapshot)
        {
            try
            {
                Changed?.Invoke(path);
            }
            catch
            {
                // Watcher darf nie crashen.
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _debounce.Dispose();
    }
}
