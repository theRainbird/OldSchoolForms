using System.Collections.Generic;
using System.Threading;

namespace OldSchoolForms.Ui.Core;

/// <summary>
/// Implemented by controls that paint a blinking text caret and therefore need a
/// periodic repaint while focused, even when the render loop is otherwise idle.
/// </summary>
internal interface IBlinkHost
{
    /// <summary>
    /// Gets whether the caret should currently be animating (focused, enabled and visible).
    /// </summary>
    bool ShouldBlink { get; }

    /// <summary>
    /// Invalidates the rendering surface that contains the caret.
    /// </summary>
    void InvalidateForBlink();
}

/// <summary>
/// Drives caret blinking for all focused caret hosts using a single shared timer.
/// The timer runs only while at least one caret host is registered, so an idle
/// application performs no blink-related wake-ups. Each tick invalidates the
/// registered hosts; because the tick runs on a thread-pool thread, the
/// cross-thread invalidation wakes the render loop via the platform's empty-event
/// post, keeping the caret in sync with the blink phase computed from the system
/// tick count at paint time.
/// </summary>
internal static class CursorBlinkDriver
{
    private static readonly object _sync = new();
    private static readonly List<IBlinkHost> _hosts = new();
    private static Timer? _timer;

    private const int IntervalMs = TextEditorEngine.CursorBlinkInterval;

    /// <summary>
    /// Registers a caret host to be repainted on each blink tick. Starts the shared
    /// timer if this is the first registered host.
    /// </summary>
    /// <param name="host">The caret host to register.</param>
    public static void Register(IBlinkHost host)
    {
        lock (_sync)
        {
            if (_hosts.Contains(host))
                return;
            _hosts.Add(host);
            _timer ??= new Timer(Tick, null, IntervalMs, IntervalMs);
        }
    }

    /// <summary>
    /// Unregisters a caret host, disposing the shared timer when no hosts remain.
    /// </summary>
    /// <param name="host">The caret host to unregister.</param>
    public static void Unregister(IBlinkHost host)
    {
        lock (_sync)
        {
            if (_hosts.Remove(host) && _hosts.Count == 0)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }
    }

    private static void Tick(object? state)
    {
        IBlinkHost[] snapshot;
        lock (_sync)
        {
            if (_hosts.Count == 0)
                return;
            snapshot = _hosts.ToArray();
        }

        foreach (var host in snapshot)
        {
            try
            {
                if (host.ShouldBlink)
                    host.InvalidateForBlink();
            }
            catch
            {
                // A host may be mid-disposal; ignore and continue with the others.
            }
        }
    }
}
