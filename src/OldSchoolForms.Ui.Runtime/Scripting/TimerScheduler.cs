using Jint.Native;
using OldSchoolForms.Ui.Core;

namespace OldSchoolForms.Ui.Runtime.Scripting;

/// <summary>
/// Timer scheduler for <c>setTimeout</c>/<c>setInterval</c>. Firing is done not
/// by dedicated threads but by <see cref="Pump"/> in the UI frame loop
/// (<see cref="Platform.Platform.OnFrame"/>), so all JavaScript callbacks run
/// deterministically on the UI thread.
/// </summary>
public sealed class TimerScheduler
{
    private sealed class TimerEntry
    {
        public int Id;
        public JsValue Callback = JsValue.Undefined;
        public double IntervalMs;
        public DateTime NextDueUtc;
        public bool Repeat;
        public string? OwnerId;
    }

    private readonly Dictionary<int, TimerEntry> _timers = new();
    private readonly JsEngineHost _host;
    private int _nextId = 1;

    internal TimerScheduler(JsEngineHost host)
    {
        _host = host;
    }

    /// <summary>Number of active timers.</summary>
    public int Count => _timers.Count;

    /// <summary>
    /// Schedules a one-shot callback.
    /// </summary>
    /// <param name="callback">The JavaScript function.</param>
    /// <param name="delayMs">Delay in milliseconds.</param>
    /// <param name="ownerId">Optional owning module ID; timers of a closed module are removed via <see cref="ClearForModule"/>.</param>
    /// <returns>The timer ID used to cancel it.</returns>
    public int SetTimeout(JsValue callback, double delayMs, string? ownerId = null) => Add(callback, delayMs, false, ownerId);

    /// <summary>
    /// Schedules a repeating callback.
    /// </summary>
    /// <param name="callback">The JavaScript function.</param>
    /// <param name="intervalMs">Interval in milliseconds.</param>
    /// <param name="ownerId">Optional owning module ID; timers of a closed module are removed via <see cref="ClearForModule"/>.</param>
    /// <returns>The timer ID used to cancel it.</returns>
    public int SetInterval(JsValue callback, double intervalMs, string? ownerId = null) => Add(callback, intervalMs, true, ownerId);

    /// <summary>
    /// Cancels a scheduled timer.
    /// </summary>
    /// <param name="id">The timer ID.</param>
    public void Clear(int id) => _timers.Remove(id);

    /// <summary>
    /// Removes all timers owned by the given module. Called when a module closes so
    /// that a lingering <c>setInterval</c> cannot keep the module's JS instance alive.
    /// </summary>
    /// <param name="ownerId">The module ID whose timers should be removed.</param>
    public void ClearForModule(string ownerId)
    {
        if (string.IsNullOrEmpty(ownerId)) return;

        List<int>? toRemove = null;
        foreach (var kvp in _timers)
        {
            if (string.Equals(kvp.Value.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
                (toRemove ??= new List<int>()).Add(kvp.Key);
        }

        if (toRemove != null)
        {
            foreach (var id in toRemove)
                _timers.Remove(id);
        }
    }

    /// <summary>
    /// Removes all timers (e.g. when the engine shuts down).
    /// </summary>
    public void ClearAll() => _timers.Clear();

    /// <summary>
    /// Executes all due timers. Called once per frame.
    /// </summary>
    public void Pump()
    {
        if (_timers.Count == 0) return;

        var now = DateTime.UtcNow;
        List<TimerEntry>? due = null;
        foreach (var entry in _timers.Values)
        {
            if (entry.NextDueUtc <= now)
                (due ??= new List<TimerEntry>()).Add(entry);
        }

        if (due == null) return;
        due.Sort(static (a, b) => a.NextDueUtc.CompareTo(b.NextDueUtc));

        foreach (var entry in due)
        {
            if (entry.Repeat)
                entry.NextDueUtc = now.AddMilliseconds(Math.Max(entry.IntervalMs, 1));
            else
                _timers.Remove(entry.Id);

            try
            {
                _host.CallFunction(entry.Callback);
            }
            catch (Exception ex)
            {
                _host.ReportError($"setTimeout/interval #{entry.Id}", ex);
            }
        }
    }

    private int Add(JsValue callback, double intervalMs, bool repeat, string? ownerId)
    {
        if (callback is not Jint.Native.Function.Function)
            throw new ArgumentException(LangRes.GetEnglish("Runtime_TimerCallbackMustBeFunction"), nameof(callback));

        if (double.IsNaN(intervalMs) || intervalMs < 0) intervalMs = 0;

        var id = _nextId++;
        _timers[id] = new TimerEntry
        {
            Id = id,
            Callback = callback,
            IntervalMs = intervalMs,
            NextDueUtc = DateTime.UtcNow.AddMilliseconds(intervalMs),
            Repeat = repeat,
            OwnerId = ownerId
        };
        return id;
    }
}
