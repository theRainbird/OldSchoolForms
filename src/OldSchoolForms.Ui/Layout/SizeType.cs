namespace OldSchoolForms.Ui.Layout;

/// <summary>
/// Specifies how the size of a table layout row or column is determined.
/// </summary>
public enum SizeType
{
    /// <summary>
    /// The row or column has a fixed size in pixels given by <see cref="TableLayoutStyle.Value"/>.
    /// </summary>
    Absolute,

    /// <summary>
    /// The row or column takes a proportional share of the remaining space.
    /// The share is relative to the sum of all percent values in the same direction.
    /// </summary>
    Percent,

    /// <summary>
    /// The row or column sizes itself to the largest preferred size of the child
    /// controls placed in it.
    /// </summary>
    AutoSize,

    /// <summary>
    /// The row or column fills the space that is left after absolute and auto-sized
    /// entries. Several fill entries in the same direction share the remaining space
    /// equally; unlike <see cref="Percent"/> no numeric value is required.
    /// </summary>
    Fill
}
