using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WinformsMVP.Analyzers
{
    /// <summary>
    /// Analyzer for MVP Rule 3: Presenter should not create UI controls.
    /// Detects when Presenters instantiate WinForms types (controls, forms, common dialogs, ...),
    /// including target-typed <c>new()</c>.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule03NoUIInPresenterAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule03NoUIInPresenter);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation,
                SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);
        }

        private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            // Only analyze in Presenter classes (semantic: derives from PresenterBase<TView>)
            var presenter = MvpSymbols.GetContainingPresenter(context.ContainingSymbol);
            if (presenter == null)
                return;

            // Get the type being created (works for both 'new Button()' and target-typed 'new()')
            var createdType = context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken).Type;
            if (createdType == null || !MvpSymbols.IsWinFormsType(createdType))
                return;

            var location = context.Node is ObjectCreationExpressionSyntax explicitCreation
                ? explicitCreation.Type.GetLocation()
                : context.Node.GetLocation();

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MVPRule03NoUIInPresenter,
                location,
                presenter.Name,
                createdType.Name));
        }
    }
}
