using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WinformsMVP.Analyzers
{
    /// <summary>
    /// Analyzer for MVP Rule 4: No UI element types in View interfaces and Presenters.
    /// Detects when View interfaces or Presenters expose WinForms types (any type in
    /// System.Windows.Forms or derived from one: Button, Form, Control, DialogResult, ...)
    /// in properties, fields, events, parameters, or return types — including when wrapped
    /// in generics or arrays (List&lt;Button&gt;, Button[]).
    /// Concrete view types (a Form implementing a view interface) are left to MVP007.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule04NoUIElementTypesAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule04NoUIElementTypes);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeMember,
                SymbolKind.Field, SymbolKind.Property, SymbolKind.Event, SymbolKind.Method);
        }

        private void AnalyzeMember(SymbolAnalysisContext context)
        {
            var member = context.Symbol;
            if (member.IsImplicitlyDeclared)
                return;

            // Accessors are covered by their property/event; operators etc. are covered as ordinary methods.
            if (member is IMethodSymbol accessor &&
                (accessor.AssociatedSymbol != null || accessor.MethodKind == MethodKind.StaticConstructor))
                return;

            var containingType = member.ContainingType;
            string memberKind;
            if (MvpSymbols.IsViewInterface(containingType))
                memberKind = "Interface";
            else if (MvpSymbols.IsPresenter(containingType))
                memberKind = "Presenter";
            else
                return;

            switch (member)
            {
                case IFieldSymbol field:
                    Check(context, field.Type, memberKind + " field", containingType);
                    break;

                case IPropertySymbol property:
                    Check(context, property.Type, memberKind + " property", containingType);
                    foreach (var parameter in property.Parameters)
                        Check(context, parameter.Type, memberKind + " indexer parameter", containingType, parameter);
                    break;

                case IEventSymbol evt:
                    Check(context, evt.Type, memberKind + " event", containingType);
                    break;

                case IMethodSymbol method:
                    if (!method.ReturnsVoid)
                        Check(context, method.ReturnType, memberKind + " method", containingType);
                    foreach (var parameter in method.Parameters)
                        Check(context, parameter.Type, memberKind + " method parameter", containingType, parameter);
                    break;
            }
        }

        private void Check(
            SymbolAnalysisContext context,
            ITypeSymbol type,
            string memberType,
            INamedTypeSymbol containingType,
            ISymbol reportOn = null)
        {
            // Concrete views (MyForm : Form, IMyView) are reported by MVP007; don't double-report.
            var uiType = MvpSymbols.FindWinFormsType(type, skipConcreteViews: true);
            if (uiType == null)
                return;

            // Report on the parameter itself when there is one, otherwise on the member identifier.
            var location = (reportOn ?? context.Symbol).Locations.FirstOrDefault(l => l.IsInSource);
            if (location == null)
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MVPRule04NoUIElementTypes,
                location,
                memberType,
                containingType.Name,
                uiType.Name));
        }
    }
}
