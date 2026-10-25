using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kibinago.Analyzers
{
    /// <summary>
    /// Analyzer for MVP Rule 17 (Presenter event visibility): Presenters should expose no public events.
    /// Window results go through the base RequestClose(...); shared / observable state belongs on a
    /// Service or IEventAggregator, not on a Presenter event.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule16NoPublicPresenterEventsAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule16NoPublicPresenterEvents);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeEvent, SymbolKind.Event);
        }

        private void AnalyzeEvent(SymbolAnalysisContext context)
        {
            var evt = (IEventSymbol)context.Symbol;
            if (evt.DeclaredAccessibility != Accessibility.Public || evt.IsImplicitlyDeclared)
                return;

            var presenter = evt.ContainingType;
            if (!MvpSymbols.IsPresenter(presenter))
                return;

            var location = evt.Locations.FirstOrDefault(l => l.IsInSource);
            if (location == null)
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MVPRule16NoPublicPresenterEvents,
                location,
                evt.Name,
                presenter.Name));
        }
    }
}
