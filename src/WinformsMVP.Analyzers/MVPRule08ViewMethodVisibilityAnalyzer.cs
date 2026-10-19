using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WinformsMVP.Analyzers
{
    /// <summary>
    /// Analyzer for MVP Rule 8: Public View methods should be defined in View interface.
    /// Ensures View implementations don't expose public methods not in the contract.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule08ViewMethodVisibilityAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule08ViewMethodVisibility);

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

            // Only analyze in classes (Forms/UserControls)
            var containingClass = methodDeclaration.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (containingClass == null)
                return;

            // Get class symbol
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(containingClass);
            if (classSymbol == null)
                return;

            // Check if class implements a view interface (semantic: WinformsMVP.MVP.Views.IViewBase)
            if (!MvpSymbols.IsConcreteView(classSymbol))
                return;

            // Get method symbol
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
            if (methodSymbol == null)
                return;

            var methodName = methodSymbol.Name;

            // Skip special methods
            if (IsSpecialMethod(methodName))
                return;

            // Skip methods with special attributes (e.g., [Browsable], [DesignerSerializationVisibility])
            if (methodSymbol.GetAttributes().Any())
                return;

            // Skip override methods (inherited from base classes like Form)
            if (methodSymbol.IsOverride)
                return;

            // Skip explicit interface implementations
            if (methodSymbol.ExplicitInterfaceImplementations.Any())
                return;

            // Check if method is defined in any View interface (IViewBase itself or a derived interface)
            var isInInterface = classSymbol.AllInterfaces
                .Where(i => MvpSymbols.IsViewBase(i) || MvpSymbols.IsViewInterface(i))
                .SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
                .Any(m => m.Name == methodName && SignaturesMatch(m, methodSymbol));

            if (!isInInterface)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.MVPRule08ViewMethodVisibility,
                    methodDeclaration.Identifier.GetLocation(),
                    methodName,
                    containingClass.Identifier.Text);

                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool IsSpecialMethod(string methodName)
        {
            // Skip WinForms designer-generated methods
            return methodName == "InitializeComponent" ||
                   methodName == "Dispose" ||
                   methodName == "GetType" ||
                   methodName == "ToString" ||
                   methodName == "Equals" ||
                   methodName == "GetHashCode";
        }

        private bool SignaturesMatch(IMethodSymbol interfaceMethod, IMethodSymbol implementationMethod)
        {
            // Simple signature matching - could be enhanced
            if (interfaceMethod.Parameters.Length != implementationMethod.Parameters.Length)
                return false;

            for (int i = 0; i < interfaceMethod.Parameters.Length; i++)
            {
                if (!SymbolEqualityComparer.Default.Equals(
                    interfaceMethod.Parameters[i].Type,
                    implementationMethod.Parameters[i].Type))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
