namespace OldSchoolForms.Ui.Layout;

/// <summary>
/// Represents the sizing behavior of a single row or column of a
/// <see cref="TableLayoutPanel"/>. The meaning of <see cref="Value"/> depends on
/// <see cref="SizeType"/>: pixels for <see cref="SizeType.Absolute"/>, a relative
/// percentage for <see cref="SizeType.Percent"/> and ignored for
/// <see cref="SizeType.AutoSize"/> and <see cref="SizeType.Fill"/>.
/// </summary>
public class TableLayoutStyle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableLayoutStyle"/> class with
    /// <see cref="SizeType.Percent"/> and a value of 1.
    /// </summary>
    public TableLayoutStyle()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableLayoutStyle"/> class with
    /// the specified size type and a value of 1.
    /// </summary>
    /// <param name="sizeType">The sizing behavior of the row or column.</param>
    public TableLayoutStyle(SizeType sizeType)
    {
        SizeType = sizeType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TableLayoutStyle"/> class with
    /// the specified size type and size value.
    /// </summary>
    /// <param name="sizeType">The sizing behavior of the row or column.</param>
    /// <param name="value">
    /// The size: pixels when <paramref name="sizeType"/> is <see cref="SizeType.Absolute"/>,
    /// a relative percentage when it is <see cref="SizeType.Percent"/>; ignored for
    /// <see cref="SizeType.AutoSize"/> and <see cref="SizeType.Fill"/>.
    /// </param>
    public TableLayoutStyle(SizeType sizeType, float value)
    {
        SizeType = sizeType;
        Value = value;
    }

    /// <summary>
    /// Gets or sets the sizing behavior of the row or column.
    /// </summary>
    public SizeType SizeType { get; set; } = SizeType.Percent;

    /// <summary>
    /// Gets or sets the size value. Interpreted as pixels for
    /// <see cref="SizeType.Absolute"/> and as a relative percentage for
    /// <see cref="SizeType.Percent"/>. Not used for <see cref="SizeType.AutoSize"/>
    /// or <see cref="SizeType.Fill"/>.
    /// </summary>
    public float Value { get; set; } = 1f;

    /// <summary>
    /// Creates an independent copy of this style.
    /// </summary>
    /// <returns>A new <see cref="TableLayoutStyle"/> with the same size type and value.</returns>
    public TableLayoutStyle Clone() => new TableLayoutStyle(SizeType, Value);
}
