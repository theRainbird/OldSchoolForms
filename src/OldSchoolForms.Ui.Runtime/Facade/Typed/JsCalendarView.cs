using Jint;
using Jint.Native;
using OldSchoolForms.Ui.Controls.Advanced;
using OldSchoolForms.Ui.Runtime.Scripting;

namespace OldSchoolForms.Ui.Runtime.Facade.Typed;

/// <summary>
/// JS facade for a <see cref="CalendarView"/>. Provides view type and date
/// selection, appointment management and navigation. The <c>appointmentchanged</c>
/// event is cancelable: setting <c>e.handled = true</c> reverts the change.
/// </summary>
public class JsCalendarView : JsControl
{
    private readonly JsCalendarAppointmentCollection _appointments;

    /// <summary>The wrapped calendar view.</summary>
    public CalendarView Calendar { get; }

    /// <summary>
    /// Creates a facade around a calendar view.
    /// </summary>
    /// <param name="host">The JS engine for callbacks.</param>
    /// <param name="calendar">The wrapped calendar view.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="calendar"/> is <see langword="null"/>.</exception>
    public JsCalendarView(JsEngineHost host, CalendarView calendar)
        : base(host, calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        Calendar = calendar;
        _appointments = new JsCalendarAppointmentCollection(host, calendar);
    }

    /// <summary>
    /// The calendar view type as a string (<c>Month</c>, <c>Week</c>, <c>Day</c>).
    /// Invalid values are ignored.
    /// </summary>
    public string ViewType
    {
        get => Calendar.ViewType.ToString();
        set
        {
            if (Enum.TryParse<CalendarViewType>(value, true, out var type))
                Calendar.ViewType = type;
        }
    }

    /// <summary>The selected date as an ISO-8601 string.</summary>
    public JsValue SelectedDate
    {
        get => JsValue.FromObject(Host.Engine, JsMarshal.FromDateTime(Calendar.SelectedDate));
        set => Calendar.SelectedDate = JsMarshal.ToDateTime(value, Calendar.SelectedDate);
    }

    /// <summary>Whether the user may create and edit appointments.</summary>
    public bool AllowEdit
    {
        get => Calendar.AllowEdit;
        set => Calendar.AllowEdit = value;
    }

    /// <summary>The appointment collection facade.</summary>
    public JsCalendarAppointmentCollection Appointments => _appointments;

    /// <summary>The currently selected appointment facade, or <c>null</c>.</summary>
    public JsValue SelectedAppointment
        => Calendar.SelectedAppointment == null
            ? JsValue.Null
            : JsValue.FromObject(Host.Engine, new JsCalendarAppointment(Host, Calendar.SelectedAppointment));

    /// <summary>Navigates to the next period (day/week/month per view type).</summary>
    public void GoToNext() => Calendar.GoToNext();

    /// <summary>Navigates to the previous period.</summary>
    public void GoToPrevious() => Calendar.GoToPrevious();

    /// <summary>Navigates to today.</summary>
    public void GoToToday() => Calendar.GoToToday();

    /// <inheritdoc />
    protected override Delegate? WireCustom(string normalized, JsValue callback)
    {
        switch (normalized)
        {
            case "dateselected":
                EventHandler<DateSelectedEventArgs> dateSelected = (s, e) =>
                {
                    var jsEvent = BuildEvent("dateselected", e);
                    jsEvent.Value = JsMarshal.FromDateTime(e.Date);
                    RaiseEvent("dateselected", callback, jsEvent, e);
                };
                Calendar.DateSelected += dateSelected;
                return dateSelected;
            case "appointmentselected":
                EventHandler<AppointmentSelectedEventArgs> appointmentSelected = (s, e) =>
                {
                    var jsEvent = BuildEvent("appointmentselected", e);
                    jsEvent.Payload = new JsCalendarAppointment(Host, e.Appointment);
                    RaiseEvent("appointmentselected", callback, jsEvent, e);
                };
                Calendar.AppointmentSelected += appointmentSelected;
                return appointmentSelected;
            case "viewchanged":
                EventHandler viewChanged = (s, e) => Raise("viewchanged", callback, e);
                Calendar.ViewChanged += viewChanged;
                return viewChanged;
            case "appointmentchanged":
                EventHandler<CalendarAppointmentChangedEventArgs> appointmentChanged = (s, e) =>
                {
                    var jsEvent = BuildEvent("appointmentchanged", e);
                    jsEvent.Payload = new JsCalendarAppointment(Host, e.Appointment);
                    jsEvent.Value = e.ChangeType;
                    RaiseEvent("appointmentchanged", callback, jsEvent, e);
                };
                Calendar.AppointmentChanged += appointmentChanged;
                return appointmentChanged;
            default:
                return null;
        }
    }

    /// <inheritdoc />
    protected override bool UnwireCustom(string normalized, Delegate handler)
    {
        switch (normalized)
        {
            case "dateselected":
                Calendar.DateSelected -= (EventHandler<DateSelectedEventArgs>)handler;
                return true;
            case "appointmentselected":
                Calendar.AppointmentSelected -= (EventHandler<AppointmentSelectedEventArgs>)handler;
                return true;
            case "viewchanged":
                Calendar.ViewChanged -= (EventHandler)handler;
                return true;
            case "appointmentchanged":
                Calendar.AppointmentChanged -= (EventHandler<CalendarAppointmentChangedEventArgs>)handler;
                return true;
            default:
                return false;
        }
    }
}
