using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WinformsMVP.Analyzers
{
    /// <summary>
    /// IEventAggregator subscriptions are strong references that end only when their token is
    /// disposed. A <c>Subscribe(...)</c> call used as a bare statement throws the token away, so the
    /// subscription (and whatever its handler captures) lives for the aggregator's lifetime.
    /// An explicit discard (<c>_ = events.Subscribe(...)</c>) is accepted as an intentional choice.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule17UndisposedSubscriptionAnalyzer : DiagnosticAnalyzer
    {
        private const string EventAggregatorInterface = "WinformsMVP.Common.EventAggregator.IEventAggregator";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule17UndisposedSubscription);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeStatement, SyntaxKind.ExpressionStatement);
        }

        private static void AnalyzeStatement(SyntaxNodeAnalysisContext context)
        {
            var statement = (ExpressionStatementSyntax)context.Node;
            if (!(statement.Expression is InvocationExpressionSyntax invocation))
                return;

            var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
            if (method == null || method.Name != "Subscribe" || !IsEventAggregatorSubscribe(method))
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MVPRule17UndisposedSubscription,
                invocation.GetLocation(),
                method.ContainingType.Name + ".Subscribe"));
        }

        private static bool IsEventAggregatorSubscribe(IMethodSymbol method)
        {
            var type = method.ContainingType;
            if (IsEventAggregatorInterface(type))
                return true;

            // An implementation's own Subscribe (e.g. calling it on the concrete EventAggregator).
            return type.AllInterfaces.Any(IsEventAggregatorInterface);
        }

        private static bool IsEventAggregatorInterface(INamedTypeSymbol type)
            => type != null && type.ToDisplayString() == EventAggregatorInterface;
    }
}
