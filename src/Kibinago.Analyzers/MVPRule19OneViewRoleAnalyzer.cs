using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kibinago.Analyzers
{
    /// <summary>
    /// A view interface or view class that is both an IWindowView and an IControlView. Reported on the
    /// interface that first combines them; a class is reported only when it combines them itself
    /// (none of its interfaces already does).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MVPRule19OneViewRoleAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.MVPRule19OneViewRole);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
        }

        private static void AnalyzeType(SymbolAnalysisContext context)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (type.TypeKind != TypeKind.Interface && type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
                return;
            if (!HasBothRoles(type))
                return;

            // Report where the two roles meet, not on every type downstream.
            if (type.Interfaces.Any(HasBothRoles) || (type.BaseType != null && HasBothRoles(type.BaseType)))
                return;

            var location = type.Locations.FirstOrDefault(l => l.IsInSource);
            if (location != null)
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MVPRule19OneViewRole, location, type.Name));
        }

        private static bool HasBothRoles(INamedTypeSymbol type) =>
            type.AllInterfaces.Any(MvpSymbols.IsWindowRole) && type.AllInterfaces.Any(MvpSymbols.IsControlRole);
    }
}
