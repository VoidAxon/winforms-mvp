using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Kibinago.Analyzers
{
    /// <summary>
    /// Semantic classification shared by the MVP analyzers.
    /// The analyzer has no reference to the framework or to WinForms, so every check matches
    /// by fully-qualified metadata name instead of by bare type name or name suffix.
    /// </summary>
    internal static class MvpSymbols
    {
        private const string PresentersNamespace = "Kibinago.Presenters";
        private const string PresenterBaseMetadataName = "PresenterBase`1";
        private const string ViewsNamespace = "Kibinago.Views";
        private const string ViewBaseName = "IViewBase";
        private const string WinFormsNamespace = "System.Windows.Forms";

        /// <summary>
        /// A presenter is a class deriving (transitively) from Kibinago.Presenters.PresenterBase&lt;TView&gt;.
        /// </summary>
        public static bool IsPresenter(INamedTypeSymbol type)
        {
            if (type == null || type.TypeKind != TypeKind.Class)
                return false;

            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current.MetadataName == PresenterBaseMetadataName &&
                    IsInNamespace(current, PresentersNamespace))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Walks outward through containing types so members of nested helper classes
        /// inside a presenter are attributed to that presenter.
        /// </summary>
        public static INamedTypeSymbol GetContainingPresenter(ISymbol symbol)
        {
            for (var type = symbol as INamedTypeSymbol ?? symbol?.ContainingType; type != null; type = type.ContainingType)
            {
                if (IsPresenter(type))
                    return type;
            }

            return null;
        }

        /// <summary>Kibinago.Views.IViewBase itself.</summary>
        public static bool IsViewBase(INamedTypeSymbol type) =>
            type != null && type.TypeKind == TypeKind.Interface &&
            type.MetadataName == ViewBaseName && IsInNamespace(type, ViewsNamespace);

        /// <summary>
        /// A view interface is an interface deriving (transitively) from Kibinago.Views.IViewBase.
        /// </summary>
        public static bool IsViewInterface(INamedTypeSymbol type)
        {
            if (type == null || type.TypeKind != TypeKind.Interface)
                return false;

            foreach (var baseInterface in type.AllInterfaces)
            {
                if (IsViewBase(baseInterface))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A concrete view is a non-interface type implementing a view interface (a Form or UserControl).
        /// </summary>
        public static bool IsConcreteView(ITypeSymbol type)
        {
            if (type == null || type.TypeKind == TypeKind.Interface || type.TypeKind == TypeKind.TypeParameter)
                return false;

            foreach (var baseInterface in type.AllInterfaces)
            {
                if (IsViewBase(baseInterface))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A WinForms type is declared in System.Windows.Forms (or a sub-namespace), or derives from one
        /// (e.g. a custom <c>MyButton : Button</c>).
        /// </summary>
        public static bool IsWinFormsType(ITypeSymbol type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (IsInWinFormsNamespace(current))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Enumerates the type and every type nested in it through arrays, pointers, nullable and
        /// generic type arguments, so <c>List&lt;Button&gt;</c> and <c>Button[]</c> are seen as Button.
        /// </summary>
        public static IEnumerable<ITypeSymbol> Flatten(ITypeSymbol type)
        {
            if (type == null)
                yield break;

            yield return type;

            switch (type)
            {
                case IArrayTypeSymbol array:
                    foreach (var inner in Flatten(array.ElementType))
                        yield return inner;
                    break;

                case IPointerTypeSymbol pointer:
                    foreach (var inner in Flatten(pointer.PointedAtType))
                        yield return inner;
                    break;

                case INamedTypeSymbol named when named.IsGenericType:
                    foreach (var argument in named.TypeArguments)
                        foreach (var inner in Flatten(argument))
                            yield return inner;
                    break;
            }
        }

        /// <summary>
        /// Returns the first WinForms type found in <paramref name="type"/> (including nested generic
        /// arguments / array elements), skipping concrete views when <paramref name="skipConcreteViews"/>
        /// is set (those are reported by MVP007 instead).
        /// </summary>
        public static ITypeSymbol FindWinFormsType(ITypeSymbol type, bool skipConcreteViews)
        {
            foreach (var candidate in Flatten(type))
            {
                if (skipConcreteViews && IsConcreteView(candidate))
                    continue;

                if (IsWinFormsType(candidate))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// Returns the first concrete WinForms view (a Form / UserControl implementing a view interface)
        /// found in <paramref name="type"/>, or null. Non-WinForms test doubles implementing a view
        /// interface are not matched.
        /// </summary>
        public static ITypeSymbol FindConcreteFormView(ITypeSymbol type)
        {
            foreach (var candidate in Flatten(type))
            {
                if (IsConcreteView(candidate) && IsWinFormsType(candidate))
                    return candidate;
            }

            return null;
        }

        private static bool IsInWinFormsNamespace(ITypeSymbol type)
        {
            var ns = type.ContainingNamespace?.ToDisplayString();
            return ns != null && (ns == WinFormsNamespace || ns.StartsWith(WinFormsNamespace + "."));
        }

        private static bool IsInNamespace(ISymbol symbol, string ns) =>
            symbol.ContainingNamespace?.ToDisplayString() == ns;
    }
}
