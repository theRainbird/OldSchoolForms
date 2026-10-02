using Jint.Native;

namespace OldSchoolForms.Ui.Runtime.Scripting;

/// <summary>
/// Zeitgeber-Scheduler für <c>setTimeout</c>/<c>setInterval</c>. Die Auslösung
/// erfolgt nicht durch eigene Threads, sondern durch <see cref="Pump"/> im
/// UI-Frame-Loop (<see cref="Platform.Platform.OnFrame"/>), damit alle
/// JavaScript-Callbacks deterministisch auf dem UI-Thread laufen.
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
    }

    private readonly Dictionary<int, TimerEntry> _timers = new();
    private readonly JsEngineHost _host;
    private int _nextId = 1;

    internal TimerScheduler(JsEngineHost host)
    {
        _host = host;
    }

    /// <summary>Anzahl aktiver Zeitgeber.</summary>
    public int Count => _timers.Count;

    /// <summary>
    /// Plant einen einmaligen Callback.
    /// </summary>
    /// <param name="callback">Die JavaScript-Funktion.</param>
    /// <param name="delayMs">Verzögerung in Millisekunden.</param>
    /// <returns>Die Zeitgeber-ID zum Abbrechen.</returns>
    public int SetTimeout(JsValue callback, double delayMs) => Add(callback, delayMs, false);

    /// <summary>
    /// Plant einen wiederkehrenden Callback.
    /// </summary>
    /// <param name="callback">Die JavaScript-Funktion.</param>
    /// <param name="intervalMs">Intervall in Millisekunden.</param>
    /// <returns>Die Zeitgeber-ID zum Abbrechen.</returns>
    public int SetInterval(JsValue callback, double intervalMs) => Add(callback, intervalMs, true);

    /// <summary>
    /// Bricht einen geplanten Zeitgeber ab.
    /// </summary>
    /// <param name="id">Die Zeitgeber-ID.</param>
    public void Clear(int id) => _timers.Remove(id);

    /// <summary>
    /// Entfernt alle Zeitgeber (z. B. beim Herunterfahren eines Moduls).
    /// </summary>
    public void ClearAll() => _timers.Clear();

    /// <summary>
    /// Führt alle fälligen Zeitgeber aus. Wird einmal pro Frame aufgerufen.
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

    private int Add(JsValue callback, double intervalMs, bool repeat)
    {
        if (callback is not Jint.Native.Function.Function)
            throw new ArgumentException("Erstes Argument von setTimeout/setInterval muss eine Funktion sein.", nameof(callback));

        if (double.IsNaN(intervalMs) || intervalMs < 0) intervalMs = 0;

        var id = _nextId++;
        _timers[id] = new TimerEntry
        {
            Id = id,
            Callback = callback,
            IntervalMs = intervalMs,
            NextDueUtc = DateTime.UtcNow.AddMilliseconds(intervalMs),
            Repeat = repeat
        };
        return id;
    }
}
