using System;
using OldSchoolForms.Ui.Designer.Services;

namespace OldSchoolForms.Ui.Designer.Roslyn;

/// <summary>
/// Roslyn-backed implementation of <see cref="IDesignDocumentService"/> that uses
/// <see cref="FormParser"/> to load forms and <see cref="CodeGenerator"/> to save them.
/// </summary>
public sealed class RoslynDocumentService : IDesignDocumentService
{
    /// <summary>
    /// Parses the given source code and reconstructs the control hierarchy on the surface.
    /// </summary>
    /// <param name="sourceCode">The full C# source code of the form file.</param>
    /// <param name="surface">The design surface to populate.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="sourceCode"/> or <paramref name="surface"/> is null.</exception>
    public void Load(string sourceCode, DesignSurface surface)
    {
        if (sourceCode == null) throw new ArgumentNullException(nameof(sourceCode));
        if (surface == null) throw new ArgumentNullException(nameof(surface));

        var parser = new FormParser();
        parser.Parse(sourceCode, surface);
    }

    /// <summary>
    /// Generates the <c>InitializeComponent</c> source for the current surface state.
    /// </summary>
    /// <param name="surface">The design surface to serialize.</param>
    /// <param name="namespaceName">The namespace for the generated partial class.</param>
    /// <param name="className">The class name of the generated form.</param>
    /// <param name="existingSource">Optional previously generated source used to preserve
    /// user-edited event handler bodies during regeneration.</param>
    /// <returns>A string containing the generated source code.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="surface"/> is null.</exception>
    public string Save(DesignSurface surface, string namespaceName, string className, string? existingSource = null)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));

        var generator = new CodeGenerator(surface);
        return generator.Generate(namespaceName, className, existingSource);
    }
}
