using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kibinago.Analyzers
{
    /// <summary>
    /// A Debouncer / Throttler holds a timer: one that is never disposed can still fire after its
    /// presenter or window is gone, and touch a disposed view. A new one stored in a field or property
    /// of the class must be disposed somewhere in that class — <c>.DisposeWith(Disposables)</c> at the
    /// creation, <c>.Dispose()</c> in <c>Cleanup</c> / <c>Dispose</c>, or handed to another method
    /// (e.g. <c>Disposables.Add(_d)</c>). Locals and values passed straight to a method are not checked.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule18UndisposedRateLimiterAnalyzer : DiagnosticAnalyzer
    {
        private const string CommonNamespace = "Kibinago.Common";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule18UndisposedRateLimiter);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeCreation,
                SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeCreation(SyntaxNodeAnalysisContext context)
        {
            var creation = (ExpressionSyntax)context.Node;
            var model = context.SemanticModel;
            var typeName = CreatedRateLimiter(creation, model, context);
            if (typeName == null)
                return;

            // Where the new instance goes: only a field / property of this class is checked.
            var outer = creation;
            while (outer.Parent is ParenthesizedExpressionSyntax || outer.Parent is CastExpressionSyntax)
                outer = (ExpressionSyntax)outer.Parent;

            ISymbol member = null;
            if (outer.Parent is AssignmentExpressionSyntax assignment && assignment.Right == outer &&
                assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
                member = model.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol;
            else if (outer.Parent is EqualsValueClauseSyntax equals &&
                     (equals.Parent is VariableDeclaratorSyntax || equals.Parent is PropertyDeclarationSyntax))
                member = model.GetDeclaredSymbol(equals.Parent, context.CancellationToken);

            if (!(member is IFieldSymbol || member is IPropertySymbol) || member.IsStatic)
                return;
            var type = member.ContainingType;
            var enclosing = model.GetEnclosingSymbol(creation.SpanStart, context.CancellationToken);
            var containing = enclosing as INamedTypeSymbol ?? enclosing?.ContainingType;
            if (type == null || !SymbolEqualityComparer.Default.Equals(type, containing) || type.Locations.All(l => !l.IsInSource))
                return;

            if (IsDisposedInType(member, type, context))
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MVPRule18UndisposedRateLimiter,
                creation.GetLocation(),
                member.Name, typeName));
        }

        // "Debouncer" / "Throttler" when the node creates one (new, or a RateLimit factory), else null.
        private static string CreatedRateLimiter(ExpressionSyntax node, SemanticModel model, SyntaxNodeAnalysisContext context)
        {
            if (node is InvocationExpressionSyntax)
            {
                var method = model.GetSymbolInfo(node, context.CancellationToken).Symbol as IMethodSymbol;
                if (method == null || !method.IsStatic || method.ContainingType?.Name != "RateLimit" ||
                    method.ContainingType.ContainingNamespace?.ToDisplayString() != CommonNamespace)
                    return null;
                return RateLimiterName(method.ReturnType);
            }

            return RateLimiterName(model.GetTypeInfo(node, context.CancellationToken).Type);
        }

        private static string RateLimiterName(ITypeSymbol type)
        {
            if (type == null || (type.Name != "Debouncer" && type.Name != "Throttler"))
                return null;
            return type.ContainingNamespace?.ToDisplayString() == CommonNamespace ? type.Name : null;
        }

        // Anywhere in the class (all partial parts): member.Dispose(), member?.Dispose(),
        // member.DisposeWith(...), using (member), or member passed to a method.
        private static bool IsDisposedInType(ISymbol member, INamedTypeSymbol type, SyntaxNodeAnalysisContext context)
        {
            foreach (var reference in type.DeclaringSyntaxReferences)
            {
                var root = reference.GetSyntax(context.CancellationToken);
                SemanticModel model = null;
                foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    if (name.Identifier.ValueText != member.Name)
                        continue;
                    ExpressionSyntax use = name;
                    if (use.Parent is MemberAccessExpressionSyntax access && access.Name == use)
                        use = access;   // this._d
                    if (!IsDisposingUse(use))
                        continue;

                    model = model ?? context.Compilation.GetSemanticModel(root.SyntaxTree);
                    if (SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, context.CancellationToken).Symbol, member))
                        return true;
                }
            }

            return false;
        }

        private static bool IsDisposingUse(ExpressionSyntax use)
        {
            while (use.Parent is ParenthesizedExpressionSyntax || use.Parent is CastExpressionSyntax)
                use = (ExpressionSyntax)use.Parent;

            switch (use.Parent)
            {
                case MemberAccessExpressionSyntax access when access.Expression == use:
                    return IsDisposeName(access.Name.Identifier.ValueText);
                case ConditionalAccessExpressionSyntax conditional when conditional.Expression == use:
                    return conditional.WhenNotNull is InvocationExpressionSyntax call &&
                           call.Expression is MemberBindingExpressionSyntax binding &&
                           IsDisposeName(binding.Name.Identifier.ValueText);
                case ArgumentSyntax _:
                    return true;
                case UsingStatementSyntax usingStatement:
                    return usingStatement.Expression == use;
                default:
                    return false;
            }
        }

        private static bool IsDisposeName(string name) => name == "Dispose" || name == "DisposeWith";
    }
}
