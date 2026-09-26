using System;

namespace OldSchoolForms.Ui.Core;

/// <summary>
/// Assigns a toolbox icon to a control class. The icon is displayed next to the
/// control's display name in the designer toolbox.
/// </summary>
/// <remarks>
/// The icon is referenced by the fully qualified name of an embedded SVG resource,
/// for example <c>"OldSchoolForms.Ui.Resources.Icons.FluentColor.ic_fluent_checkbox_16_color.svg"</c>.
/// The SVG is rasterized resolution-independently via <see cref="SvgImage"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ToolboxIconAttribute : Attribute
{
    /// <summary>
    /// Gets the fully qualified name of the embedded SVG resource used as the toolbox icon.
    /// </summary>
    public string Resource { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolboxIconAttribute"/> class.
    /// </summary>
    /// <param name="resource">
    /// The fully qualified name of the embedded SVG resource,
    /// e.g. <c>"OldSchoolForms.Ui.Resources.Icons.FluentColor.ic_fluent_checkbox_16_color.svg"</c>.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="resource"/> is null, empty or whitespace.</exception>
    public ToolboxIconAttribute(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
            throw new ArgumentException("Icon resource name must not be empty.", nameof(resource));
        Resource = resource;
    }
}
