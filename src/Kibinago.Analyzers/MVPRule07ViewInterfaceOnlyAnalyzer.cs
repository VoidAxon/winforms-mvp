using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kibinago.Analyzers
{
    /// <summary>
    /// Analyzer for MVP Rule 7: Presenter should access View only through interface.
    /// Detects when Presenters reference concrete Form types (a WinForms class implementing a view
    /// interface, e.g. <c>MyForm : Form, IMyView</c>) instead of the View interface — in member
    /// signatures, in generics/arrays, and as the TView argument of the presenter base class.
    /// Other WinForms types are reported by MVP004; non-WinForms test doubles are not flagged.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule07ViewInterfaceOnlyAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule07ViewInterfaceOnly);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzePresenterType, SymbolKind.NamedType);
            context.RegisterSymbolAction(AnalyzeMember,
                SymbolKind.Field, SymbolKind.Property, SymbolKind.Event, SymbolKind.Method);
        }

        private void AnalyzePresenterType(SymbolAnalysisContext context)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (!MvpSymbols.IsPresenter(type) || type.BaseType == null)
                return;

            // class MyPresenter : WindowPresenterBase<MyForm>
            foreach (var argument in type.BaseType.TypeArguments)
            {
                var concreteView = MvpSymbols.FindConcreteFormView(argument);
                if (concreteView != null)
                {
                    Report(context, type, type, concreteView);
                    return;
                }
            }
        }

        private void AnalyzeMember(SymbolAnalysisContext context)
        {
            var member = context.Symbol;
            if (member.IsImplicitlyDeclared)
                return;

            if (member is IMethodSymbol accessor && accessor.AssociatedSymbol != null)
                return;

            var presenter = member.ContainingType;
            if (!MvpSymbols.IsPresenter(presenter))
                return;

            switch (member)
            {
                case IFieldSymbol field:
                    Check(context, presenter, member, field.Type);
                    break;

                case IPropertySymbol property:
                    Check(context, presenter, member, property.Type);
                    break;

                case IEventSymbol evt:
                    Check(context, presenter, member, evt.Type);
                    break;

                case IMethodSymbol method:
                    if (!method.ReturnsVoid)
                        Check(context, presenter, member, method.ReturnType);
                    foreach (var parameter in method.Parameters)
                        Check(context, presenter, parameter, parameter.Type);
                    break;
            }
        }

        private void Check(SymbolAnalysisContext context, INamedTypeSymbol presenter, ISymbol reportOn, ITypeSymbol type)
        {
            var concreteView = MvpSymbols.FindConcreteFormView(type);
            if (concreteView != null)
                Report(context, presenter, reportOn, concreteView);
        }

        private void Report(SymbolAnalysisContext context, INamedTypeSymbol presenter, ISymbol reportOn, ITypeSymbol concreteView)
        {
            var location = reportOn.Locations.FirstOrDefault(l => l.IsInSource);
            if (location == null)
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MVPRule07ViewInterfaceOnly,
                location,
                presenter.Name,
                concreteView.Name));
        }
    }
}
