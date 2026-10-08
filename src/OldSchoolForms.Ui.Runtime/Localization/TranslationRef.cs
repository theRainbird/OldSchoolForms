using System.Reflection;

namespace OldSchoolForms.Ui.Runtime.Localization;

/// <summary>
/// A resolved translation binding captured during layout loading: re-applying it
/// sets the target property to the currently localized value of the key. Used to
/// re-localize the shell chrome when the UI culture changes at runtime.
/// </summary>
/// <param name="Target">The object whose property was translated.</param>
/// <param name="Property">The property that received the translated value.</param>
/// <param name="Key">The translation key (without the <c>tr:</c> prefix).</param>
public sealed record TranslationRef(object Target, PropertyInfo Property, string Key)
{
    /// <summary>
    /// Re-applies the localized value of this binding for the current culture.
    /// Silently ignores targets that can no longer be set (e.g. disposed controls).
    /// </summary>
    /// <param name="i18n">The localization service.</param>
    public void Reapply(RuntimeI18n i18n)
    {
        ArgumentNullException.ThrowIfNull(i18n);
        try
        {
            Property.SetValue(Target, i18n.T(Key));
        }
        catch (Exception)
        {
            // The target may have been disposed after a module close; ignore.
        }
    }
}
