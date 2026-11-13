using System;

namespace OldSchoolForms.Ui.Designer.Services;

/// <summary>
/// Abstraction for loading and saving designed forms to and from source code.
/// Implemented by the Roslyn-backed <c>RoslynDocumentService</c> so that the
/// designer UI stays free of a direct dependency on the code-parsing layer.
/// </summary>
public interface IDesignDocumentService
{
    /// <summary>
    /// Loads the given form source code into the supplied design surface,
    /// reconstructing the control hierarchy.
    /// </summary>
    /// <param name="sourceCode">The full C# source code of the form file.</param>
    /// <param name="surface">The design surface to populate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="sourceCode"/> or <paramref name="surface"/> is null.</exception>
    void Load(string sourceCode, DesignSurface surface);

    /// <summary>
    /// Generates source code representing the current state of the design surface.
    /// </summary>
    /// <param name="surface">The design surface to serialize.</param>
    /// <param name="namespaceName">The namespace for the generated partial class.</param>
    /// <param name="className">The class name of the generated form.</param>
    /// <param name="existingSource">Optional previously generated source used to preserve
    /// user-edited event handler bodies during regeneration.</param>
    /// <returns>A string containing the generated source code.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="surface"/> is null.</exception>
    string Save(DesignSurface surface, string namespaceName, string className, string? existingSource = null);
}
