using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WinformsMVP.Analyzers
{
    /// <summary>
    /// Analyzer for MVP Rule 13: View interface methods should not contain UI control type names.
    /// Methods should use domain-specific naming instead.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule13NoUIControlNamesAnalyzer : DiagnosticAnalyzer
    {
        private static readonly string[] UIControlTypes = new[]
        {
            "Button", "TextBox", "Label", "ListBox", "DataGrid", "DataGridView",
            "TreeView", "ListView", "TabControl", "Panel", "GroupBox",
            "ComboBox", "CheckBox", "RadioButton", "PictureBox", "ProgressBar",
            "TrackBar", "NumericUpDown", "DateTimePicker", "MonthCalendar",
            "RichTextBox", "WebBrowser", "SplitContainer", "FlowLayoutPanel",
            "TableLayoutPanel", "ToolStrip", "MenuStrip", "StatusStrip",
            "ContextMenuStrip", "PropertyGrid", "MaskedTextBox"
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule13NoUIControlNames);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Only analyze methods in interfaces
            if (!(methodDeclaration.Parent is InterfaceDeclarationSyntax interfaceDeclaration))
                return;

            var interfaceName = interfaceDeclaration.Identifier.Text;

            // Only analyze View interfaces (semantic: derives from WinformsMVP.MVP.Views.IViewBase)
            if (!MvpSymbols.IsViewInterface(context.SemanticModel.GetDeclaredSymbol(interfaceDeclaration, context.CancellationToken)))
                return;

            var methodName = methodDeclaration.Identifier.Text;

            // Check if method name contains any UI control type
            foreach (var controlType in UIControlTypes)
            {
                if (methodName.Contains(controlType))
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.MVPRule13NoUIControlNames,
                        methodDeclaration.Identifier.GetLocation(),
                        methodName,
                        interfaceName,
                        controlType);

                    context.ReportDiagnostic(diagnostic);
                    return; // Only report once per method
                }
            }
        }
    }
}
