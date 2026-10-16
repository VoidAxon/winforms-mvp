using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WinformsMVP.Analyzers
{
    /// <summary>
    /// Analyzer for MVP Rule 6: Public Presenter methods should return void.
    /// Enforces "Tell, Don't Ask" principle.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule06NoReturnValuesAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule06NoReturnValues);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Only analyze public methods
            if (!methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                return;

            // Only analyze in Presenter classes (semantic: derives from PresenterBase<TView>)
            var containingClass = methodDeclaration.Parent as ClassDeclarationSyntax;
            if (containingClass == null)
                return;

            if (!MvpSymbols.IsPresenter(context.SemanticModel.GetDeclaredSymbol(containingClass, context.CancellationToken)))
                return;

            // Skip methods that start with "On" (event handlers - allowed to be public in some cases)
            var methodName = methodDeclaration.Identifier.Text;

            // Check if return type is not void
            var returnType = methodDeclaration.ReturnType;
            if (returnType is PredefinedTypeSyntax predefinedType &&
                predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword))
            {
                return; // Method returns void - OK
            }

            // Skip if it's an async method returning Task (acceptable pattern)
            var typeInfo = context.SemanticModel.GetTypeInfo(returnType);
            if (typeInfo.Type != null)
            {
                var typeName = typeInfo.Type.Name;
                if (typeName == "Task" && typeInfo.Type.ContainingNamespace?.ToString() == "System.Threading.Tasks")
                {
                    return; // Async Task methods are OK
                }
            }

            // Skip override methods (framework requirements)
            if (methodDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
                return;

            // Skip interface implementations
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
            if (methodSymbol != null && methodSymbol.ExplicitInterfaceImplementations.Any())
                return;

            // Report diagnostic
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.MVPRule06NoReturnValues,
                methodDeclaration.Identifier.GetLocation(),
                methodName);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
