using Jint;
using Jint.Native;
using Jint.Native.Object;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for the appointments of a <see cref="CalendarView"/>. Appointments
/// are added via configuration objects, e.g.
/// <c>cal.appointments.add({ subject: 'Meeting', startTime: '2026-03-15T10:00:00', endTime: '2026-03-15T11:00:00', color: '#ff0000' })</c>.
/// </summary>
public class JsCalendarAppointmentCollection
{
    private readonly JsEngineHost _host;
    private readonly CalendarView _calendar;

    /// <summary>
    /// Creates the facade over the calendar's appointment collection.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="calendar">The owning calendar view.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="calendar"/> is <see langword="null"/>.</exception>
    public JsCalendarAppointmentCollection(JsEngineHost host, CalendarView calendar)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(calendar);
        _host = host;
        _calendar = calendar;
    }

    /// <summary>The number of appointments.</summary>
    public int Count => _calendar.Appointments.Count;

    /// <summary>
    /// Gets the appointment facade at the given index.
    /// </summary>
    /// <param name="index">The zero-based appointment index.</param>
    /// <returns>The appointment facade.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside the collection.</exception>
    public JsCalendarAppointment GetAt(int index) => new(_host, _calendar.Appointments[index]);

    /// <summary>
    /// Adds an appointment from a JS configuration object. Recognized members:
    /// <c>subject</c>, <c>startTime</c>, <c>endTime</c> (ISO-8601),
    /// <c>location</c>, <c>color</c> (hex), <c>allDay</c>, <c>readOnly</c>.
    /// </summary>
    /// <param name="config">The configuration object.</param>
    /// <returns>The new appointment facade.</returns>
    /// <exception cref="ArgumentException">The config is not an object.</exception>
    public JsCalendarAppointment Add(JsValue config)
    {
        if (config is not ObjectInstance obj)
            throw new ArgumentException("Appointment configuration must be an object.", nameof(config));

        var appointment = new CalendarAppointment
        {
            Subject = JsMarshal.GetString(obj, "subject") ?? string.Empty,
            StartTime = JsMarshal.ToDateTime(JsMarshal.GetMember(obj, "startTime"), DateTime.Now),
            EndTime = JsMarshal.ToDateTime(JsMarshal.GetMember(obj, "endTime"), DateTime.Now.AddHours(1)),
            Location = JsMarshal.GetString(obj, "location"),
            IsAllDay = JsMarshal.GetBool(obj, "allDay") ?? false,
            IsReadOnly = JsMarshal.GetBool(obj, "readOnly") ?? false
        };
        var color = JsMarshal.GetMember(obj, "color");
        if (!color.IsUndefined() && !color.IsNull())
            appointment.CategoryColor = JsMarshal.ToColor(color, appointment.CategoryColor);

        _calendar.Appointments.Add(appointment);
        _calendar.Invalidate();
        return new JsCalendarAppointment(_host, appointment);
    }

    /// <summary>
    /// Removes the appointment at the given index.
    /// </summary>
    /// <param name="index">The zero-based appointment index.</param>
    /// <returns><see langword="true"/> when an appointment was removed.</returns>
    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= _calendar.Appointments.Count)
            return false;
        _calendar.Appointments.RemoveAt(index);
        _calendar.Invalidate();
        return true;
    }

    /// <summary>Removes all appointments.</summary>
    public void Clear()
    {
        _calendar.Appointments.Clear();
        _calendar.Invalidate();
    }
}
