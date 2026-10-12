using System.Collections;
using System.ComponentModel;
using System.Reflection;

namespace OldSchoolForms.Ui.Data;

/// <summary>
/// Reads member values from data records in a uniform way. Supported record shapes:
/// CLR objects with public properties or fields, <see cref="IDictionary{TKey,TValue}"/>
/// records (e.g. records built from JSON or script objects) and wrapper types that
/// expose their columns through <see cref="ICustomTypeDescriptor"/>
/// (e.g. <see cref="System.Data.DataRowView"/>).
/// </summary>
public static class DataRecord
{
    /// <summary>
    /// Gets the value of the named member from the given record.
    /// </summary>
    /// <param name="record">The record object to read from; may be <see langword="null"/>.</param>
    /// <param name="memberName">The member (property, dictionary key or custom column) name; may be <see langword="null"/>.</param>
    /// <returns>The member value, or <see langword="null"/> when the record or member is missing.</returns>
    public static object? GetValue(object? record, string? memberName)
    {
        if (record == null || string.IsNullOrEmpty(memberName))
            return null;

        if (record is IDictionary<string, object?> dict)
            return dict.TryGetValue(memberName, out var value) ? value : null;

        if (record is IDictionary<string, object> plainDict)
            return plainDict.TryGetValue(memberName, out var plainValue) ? plainValue : null;

        var type = record.GetType();
        var prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop != null && prop.CanRead)
            return prop.GetValue(record);

        var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field != null)
            return field.GetValue(record);

        if (record is ICustomTypeDescriptor ctd)
        {
            foreach (PropertyDescriptor descriptor in ctd.GetProperties())
            {
                if (string.Equals(descriptor.Name, memberName, StringComparison.OrdinalIgnoreCase))
                    return descriptor.GetValue(record);
            }
        }

        return null;
    }
}
