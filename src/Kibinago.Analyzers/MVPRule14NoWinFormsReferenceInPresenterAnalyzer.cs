using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kibinago.Analyzers
{
    /// <summary>
    /// Analyzer for MVP Rule 4 (code bodies): Presenters must never reference WinForms types.
    /// Detects any name in Presenter code that binds to a System.Windows.Forms type —
    /// <c>MessageBox.Show(...)</c>, <c>DialogResult.OK</c>, <c>(Form)View</c>, <c>View as Control</c>,
    /// <c>Button b = ...</c>, <c>var b = ...</c> of a WinForms type, <c>typeof(Form)</c>, and so on.
    /// Positions already covered by another rule are skipped to avoid duplicate diagnostics:
    /// object creation of a WinForms type (MVP003), presenter member signatures (MVP004 / MVP007),
    /// and concrete view types in the base list (MVP007).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule14NoWinFormsReferenceInPresenterAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule14NoWinFormsReferenceInPresenter);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeName, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
        }

        private void AnalyzeName(SyntaxNodeAnalysisContext context)
        {
            var name = (SimpleNameSyntax)context.Node;

            // XML doc comments (<see cref="Form"/>) are documentation, not code.
            if (name.IsPartOfStructuredTrivia())
                return;

            var presenter = MvpSymbols.GetContainingPresenter(context.ContainingSymbol);
            if (presenter == null)
                return;

            var type = BoundType(context, name);
            if (type == null)
            {
                ReportStaticImportedMember(context, name, presenter);
                return;
            }

            // 'var' stands for the whole inferred type (List<Button> included); a named type
            // stands for itself only — its type arguments are separate name nodes.
            var uiType = name.IsVar
                ? MvpSymbols.FindWinFormsType(type, skipConcreteViews: false)
                : (MvpSymbols.IsWinFormsType(type) ? type : null);
            if (uiType == null)
                return;

            if (IsCoveredByOtherRule(context, name, uiType))
                return;

            // 'var x = (Button)y;' — the explicit name in the initializer is already reported.
            if (name.IsVar && InitializerNamesWinFormsType(context, name))
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MVPRule14NoWinFormsReferenceInPresenter,
                name.GetLocation(),
                presenter.Name,
                uiType.Name));
        }

        /// <summary>
        /// An unqualified name bound to a WinForms member — only possible through
        /// <c>using static System.Windows.Forms.MessageBox;</c> and similar — such as <c>Show("x")</c>
        /// or <c>OK</c>. Qualified accesses (<c>MessageBox.Show</c>, <c>x.Close()</c>) are skipped:
        /// the qualifying type name or the variable's declared type is reported instead.
        /// </summary>
        private static void ReportStaticImportedMember(SyntaxNodeAnalysisContext context, SimpleNameSyntax name, INamedTypeSymbol presenter)
        {
            if (name.Parent is MemberAccessExpressionSyntax access && access.Name == name)
                return;
            if (name.Parent is MemberBindingExpressionSyntax)
                return;

            var info = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken);
            var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
            if (symbol == null || symbol is ITypeSymbol || symbol is INamespaceSymbol)
                return;
            if (symbol is ILocalSymbol || symbol is IParameterSymbol || symbol is IRangeVariableSymbol)
                return;

            var owner = symbol.ContainingType;
            if (owner == null || !MvpSymbols.IsWinFormsType(owner))
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MVPRule14NoWinFormsReferenceInPresenter,
                name.GetLocation(),
                presenter.Name,
                owner.Name));
        }

        private static ITypeSymbol BoundType(SyntaxNodeAnalysisContext context, SimpleNameSyntax name)
        {
            var symbol = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol;
            if (symbol is IAliasSymbol alias)
                symbol = alias.Target;

            return symbol as ITypeSymbol;
        }

        private static bool InitializerNamesWinFormsType(SyntaxNodeAnalysisContext context, SimpleNameSyntax varName)
        {
            if (!(varName.Parent is VariableDeclarationSyntax declaration))
                return false;

            foreach (var variable in declaration.Variables)
            {
                if (variable.Initializer == null)
                    continue;

                foreach (var inner in variable.Initializer.DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    var innerType = BoundType(context, inner);
                    if (innerType != null && MvpSymbols.IsWinFormsType(innerType))
                        return true;
                }
            }

            return false;
        }

        private static bool IsCoveredByOtherRule(SyntaxNodeAnalysisContext context, SyntaxNode name, ITypeSymbol uiType)
        {
            for (var node = name; node != null; node = node.Parent)
            {
                var parent = node.Parent;
                switch (parent)
                {
                    // MVP003: 'new Button()' (the created type itself is a WinForms type).
                    case ObjectCreationExpressionSyntax creation when creation.Type == node:
                        var created = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;
                        return created != null && MvpSymbols.IsWinFormsType(created);

                    // MVP004 / MVP007: types in member signatures of the presenter itself
                    // (members of nested helper types are not checked there, so report them here).
                    case VariableDeclarationSyntax declaration when declaration.Type == node:
                        return (declaration.Parent is FieldDeclarationSyntax || declaration.Parent is EventFieldDeclarationSyntax) &&
                               DeclaredDirectlyInPresenter(context);
                    case BasePropertyDeclarationSyntax property when property.Type == node:
                        return DeclaredDirectlyInPresenter(context);
                    case MethodDeclarationSyntax method when method.ReturnType == node:
                        return DeclaredDirectlyInPresenter(context);
                    case OperatorDeclarationSyntax op when op.ReturnType == node:
                        return DeclaredDirectlyInPresenter(context);
                    case ConversionOperatorDeclarationSyntax conversion when conversion.Type == node:
                        return DeclaredDirectlyInPresenter(context);
                    case ParameterSyntax parameter when parameter.Type == node:
                        return (parameter.Parent?.Parent is BaseMethodDeclarationSyntax ||
                                parameter.Parent?.Parent is IndexerDeclarationSyntax) &&
                               DeclaredDirectlyInPresenter(context);

                    // MVP007: 'class P : WindowPresenterBase<MyForm>'.
                    case BaseListSyntax _:
                        return MvpSymbols.IsConcreteView(uiType) &&
                               MvpSymbols.IsPresenter(context.ContainingSymbol as INamedTypeSymbol);
                }

                // Climb only through type syntax (e.g. List<Button>, Button[], Button?); once we
                // leave a type, the name is in an expression and therefore not covered elsewhere.
                if (!(node is TypeSyntax) && !(node is TypeArgumentListSyntax))
                    return false;
            }

            return false;
        }

        private static bool DeclaredDirectlyInPresenter(SyntaxNodeAnalysisContext context)
        {
            var member = context.ContainingSymbol;
            if (member is IParameterSymbol parameter)
                member = parameter.ContainingSymbol;

            return MvpSymbols.IsPresenter(member?.ContainingType);
        }
    }
}
