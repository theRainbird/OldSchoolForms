using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="CalendarAppointment"/>. Date/time values are
/// exchanged as ISO-8601 strings, the category color as a hex string.
/// </summary>
public class JsCalendarAppointment
{
    private readonly JsEngineHost _host;

    /// <summary>The wrapped appointment.</summary>
    public CalendarAppointment Appointment { get; }

    /// <summary>
    /// Creates a facade around an appointment.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="appointment">The wrapped appointment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="appointment"/> is <see langword="null"/>.</exception>
    public JsCalendarAppointment(JsEngineHost host, CalendarAppointment appointment)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(appointment);
        _host = host;
        Appointment = appointment;
    }

    /// <summary>The unique identifier of the appointment.</summary>
    public string Id => Appointment.Id.ToString();

    /// <summary>The appointment subject.</summary>
    public string Subject
    {
        get => Appointment.Subject;
        set => Appointment.Subject = value ?? string.Empty;
    }

    /// <summary>The start time as an ISO-8601 string.</summary>
    public JsValue StartTime
    {
        get => JsValue.FromObject(_host.Engine, JsMarshal.FromDateTime(Appointment.StartTime));
        set => Appointment.StartTime = JsMarshal.ToDateTime(value, Appointment.StartTime);
    }

    /// <summary>The end time as an ISO-8601 string.</summary>
    public JsValue EndTime
    {
        get => JsValue.FromObject(_host.Engine, JsMarshal.FromDateTime(Appointment.EndTime));
        set => Appointment.EndTime = JsMarshal.ToDateTime(value, Appointment.EndTime);
    }

    /// <summary>The appointment location, or <c>null</c>.</summary>
    public JsValue Location
    {
        get => Appointment.Location == null ? JsValue.Null : JsValue.FromObject(_host.Engine, Appointment.Location);
        set
        {
            if (value is null || value.IsNull() || value.IsUndefined())
                Appointment.Location = null;
            else
                Appointment.Location = value.ToString();
        }
    }

    /// <summary>The category color as a hex string.</summary>
    public JsValue CategoryColor
    {
        get => JsValue.FromObject(_host.Engine, JsMarshal.FromColor(Appointment.CategoryColor));
        set => Appointment.CategoryColor = JsMarshal.ToColor(value, Appointment.CategoryColor);
    }

    /// <summary>Whether the appointment spans the entire day.</summary>
    public bool IsAllDay
    {
        get => Appointment.IsAllDay;
        set => Appointment.IsAllDay = value;
    }

    /// <summary>Whether the appointment is read-only.</summary>
    public bool IsReadOnly
    {
        get => Appointment.IsReadOnly;
        set => Appointment.IsReadOnly = value;
    }
}
