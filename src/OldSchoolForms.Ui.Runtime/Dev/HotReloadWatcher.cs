using System.Collections.Concurrent;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Runtime.Dev;

/// <summary>
/// Watches an app directory for file changes and raises a
/// <see cref="Changed"/> event per changed file after a debounce phase.
/// Only active in dev mode.
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
    /// Creates the watcher for a directory.
    /// </summary>
    /// <param name="rootDirectory">The directory to watch (recursive).</param>
    /// <param name="debounceMs">Debounce time in milliseconds.</param>
    public HotReloadWatcher(string rootDirectory, int debounceMs = 300)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException(LangRes.GetString("Runtime_DirectoryMissing"), nameof(rootDirectory));

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
    /// Raised when a file has changed (path relative to the root).
    /// </summary>
    public event Action<string>? Changed;

    /// <summary>
    /// Starts the monitoring.
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
                // The watcher must never crash.
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
