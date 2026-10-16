using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WinformsMVP.Analyzers
{
    /// <summary>
    /// Analyzer for MVP Rule 1: View interfaces should end with 'View'.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule01ViewNamingAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule01ViewNaming);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInterfaceDeclaration, SyntaxKind.InterfaceDeclaration);
        }

        private void AnalyzeInterfaceDeclaration(SyntaxNodeAnalysisContext context)
        {
            var interfaceDeclaration = (InterfaceDeclarationSyntax)context.Node;
            var interfaceName = interfaceDeclaration.Identifier.Text;

            // Only analyze interfaces that start with 'I' (convention for interfaces)
            if (!interfaceName.StartsWith("I"))
                return;

            // Check if this interface inherits from IViewBase or IWindowView
            if (!InheritsFromViewBase(context, interfaceDeclaration))
                return;

            // Check if interface name ends with 'View'
            if (!interfaceName.EndsWith("View"))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.MVPRule01ViewNaming,
                    interfaceDeclaration.Identifier.GetLocation(),
                    interfaceName);

                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool InheritsFromViewBase(SyntaxNodeAnalysisContext context, InterfaceDeclarationSyntax interfaceDeclaration)
        {
            if (interfaceDeclaration.BaseList == null)
                return false;

            // Semantic check: derives (transitively) from WinformsMVP.MVP.Views.IViewBase,
            // matched by namespace + name rather than bare name.
            var interfaceSymbol = context.SemanticModel.GetDeclaredSymbol(interfaceDeclaration);
            return MvpSymbols.IsViewInterface(interfaceSymbol);
        }
    }
}
