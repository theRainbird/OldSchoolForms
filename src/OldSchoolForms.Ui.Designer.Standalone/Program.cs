using OldSchoolForms.Ui.Core;
using OldSchoolForms.Ui.Designer.Roslyn;

namespace OldSchoolForms.Ui.Designer.Standalone;

/// <summary>
/// Entry point for the OldSchoolForms Form Designer standalone application.
/// Wires the Roslyn-backed document service and starts the designer.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    internal static void Main()
    {
        Application.Run(new DesignerForm(new RoslynDocumentService()));
    }
}
