using System.Collections.Concurrent;
using System.Collections.Immutable;
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
            // Per class (all partial parts): collect the creations stored in its members and the
            // members it disposes, then report the creations whose member is never disposed.
            context.RegisterSymbolStartAction(start =>
            {
                var type = (INamedTypeSymbol)start.Symbol;
                if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
                    return;

                var created = new ConcurrentBag<(ISymbol Member, Location Location, string TypeName)>();
                var disposed = new ConcurrentDictionary<ISymbol, bool>(SymbolEqualityComparer.Default);

                start.RegisterSyntaxNodeAction(c => CollectCreation(c, type, created),
                    SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression, SyntaxKind.InvocationExpression);
                start.RegisterSyntaxNodeAction(c => CollectDisposal(c, disposed), SyntaxKind.IdentifierName);
                start.RegisterSymbolEndAction(end =>
                {
                    foreach (var (member, location, typeName) in created)
                    {
                        if (!disposed.ContainsKey(member))
                            end.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.MVPRule18UndisposedRateLimiter, location, member.Name, typeName));
                    }
                });
            }, SymbolKind.NamedType);
        }

        private static void CollectCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol type,
            ConcurrentBag<(ISymbol, Location, string)> created)
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
                (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) || assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression)))
                member = model.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol;
            else if (outer.Parent is EqualsValueClauseSyntax equals &&
                     (equals.Parent is VariableDeclaratorSyntax || equals.Parent is PropertyDeclarationSyntax))
                member = model.GetDeclaredSymbol(equals.Parent, context.CancellationToken);

            if (!(member is IFieldSymbol || member is IPropertySymbol) || member.IsStatic ||
                !SymbolEqualityComparer.Default.Equals(member.ContainingType, type))
                return;
            created.Add((member.OriginalDefinition, creation.GetLocation(), typeName));
        }

        // member.Dispose(), member?.Dispose(), member.DisposeWith(...), using (member),
        // using var x = member, or member passed to a method.
        private static void CollectDisposal(SyntaxNodeAnalysisContext context, ConcurrentDictionary<ISymbol, bool> disposed)
        {
            var name = (IdentifierNameSyntax)context.Node;
            ExpressionSyntax use = name;
            if (use.Parent is MemberAccessExpressionSyntax access && access.Name == use)
                use = access;   // this._d
            if (!IsDisposingUse(use))
                return;

            var symbol = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol;
            if (symbol is IFieldSymbol || symbol is IPropertySymbol)
                disposed.TryAdd(symbol.OriginalDefinition, true);
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
                case EqualsValueClauseSyntax equals:
                    // using (var x = member) { } / using var x = member;
                    var declaration = equals.Parent?.Parent as VariableDeclarationSyntax;
                    return declaration?.Parent is UsingStatementSyntax ||
                           (declaration?.Parent is LocalDeclarationStatementSyntax local && local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword));
                default:
                    return false;
            }
        }

        private static bool IsDisposeName(string name) => name == "Dispose" || name == "DisposeWith";
    }
}
