using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kibinago.Analyzers
{
    /// <summary>
    /// Analyzer for MVP Rule 2: Presenter classes should end with 'Presenter'.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule02PresenterNamingAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule02PresenterNaming);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
        }

        private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var className = classDeclaration.Identifier.Text;

            // Check if this class inherits from PresenterBase
            if (!InheritsFromPresenterBase(context, classDeclaration))
                return;

            // An abstract class is a shared base of presenters, not a screen's presenter: it may be
            // named like the framework's own bases (XxxPresenterBase).
            if (classDeclaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
                return;

            // Check if class name ends with 'Presenter'
            if (!className.EndsWith("Presenter"))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.MVPRule02PresenterNaming,
                    classDeclaration.Identifier.GetLocation(),
                    className);

                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool InheritsFromPresenterBase(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
        {
            if (classDeclaration.BaseList == null)
                return false;

            // Semantic check: derives (transitively) from Kibinago.Presenters.PresenterBase<TView>.
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
            return MvpSymbols.IsPresenter(classSymbol);
        }
    }
}
