using Microsoft.CodeAnalysis;

namespace WinformsMVP.Analyzers
{
    /// <summary>
    /// Diagnostic descriptors for MVP design rule violations.
    /// Based on MVP-DESIGN-RULES.md
    /// </summary>
    public static class DiagnosticDescriptors
    {
        private const string Category = "MVP Design";

        // Rule 1: View naming convention
        public static readonly DiagnosticDescriptor MVPRule01ViewNaming = new DiagnosticDescriptor(
            id: "MVP001",
            title: "View interface should end with 'View'",
            messageFormat: "View interface '{0}' should end with 'View' suffix",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "All View interfaces must have a 'View' suffix (e.g., ITaskView, IUserEditorView). This is Rule 1 of MVP design rules.");

        // Rule 2: Presenter naming convention
        public static readonly DiagnosticDescriptor MVPRule02PresenterNaming = new DiagnosticDescriptor(
            id: "MVP002",
            title: "Presenter class should end with 'Presenter'",
            messageFormat: "Presenter class '{0}' should end with 'Presenter' suffix",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "All Presenter classes must have a 'Presenter' suffix (e.g., TaskPresenter, UserEditorPresenter). This is Rule 2 of MVP design rules.");

        // Rule 3: Presenter should not create UI controls
        public static readonly DiagnosticDescriptor MVPRule03NoUIInPresenter = new DiagnosticDescriptor(
            id: "MVP003",
            title: "Presenter should not create UI controls",
            messageFormat: "Presenter '{0}' should not create UI control of type '{1}'. This violates separation of concerns.",
            category: Category,
            // Warning, not Error: the analyzer now ships inside the WinformsMVP package, so an
            // Error would break the build of any consumer the moment they install the framework.
            // Teams that want it enforced can raise it to error via .editorconfig.
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Presenters must handle use-case logic only, not View logic. Creating UI controls is a View responsibility. This is Rule 3 of MVP design rules.");

        // Rule 4: No UI element types in View interfaces and Presenters
        public static readonly DiagnosticDescriptor MVPRule04NoUIElementTypes = new DiagnosticDescriptor(
            id: "MVP004",
            title: "No UI element types in View interfaces and Presenters",
            messageFormat: "{0} '{1}' contains UI element type '{2}'. View interfaces and Presenters must not expose UI types.",
            category: Category,
            // Warning, not Error: shipped inside the WinformsMVP package; see MVP003 note.
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "View interfaces and Presenters must never expose WinForms UI types (Button, TextBox, etc.) in properties, fields, parameters, or return types. This ensures proper separation of concerns and testability. This is Rule 4 of MVP design rules.");

        // Rule 6: No return values from public Presenter methods
        public static readonly DiagnosticDescriptor MVPRule06NoReturnValues = new DiagnosticDescriptor(
            id: "MVP006",
            title: "Public Presenter methods should return void",
            messageFormat: "Public method '{0}' in Presenter should return void (Tell, Don't Ask principle)",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Presenter methods should follow the 'Tell, Don't Ask' principle. Use void methods to tell the View what to do. This is Rule 6 of MVP design rules.");

        // Rule 7: Access View only through interface
        public static readonly DiagnosticDescriptor MVPRule07ViewInterfaceOnly = new DiagnosticDescriptor(
            id: "MVP007",
            title: "Presenter should access View only through interface",
            messageFormat: "Presenter '{0}' references concrete Form type '{1}'. Use View interface instead.",
            category: Category,
            // Warning, not Error: shipped inside the WinformsMVP package; see MVP003 note.
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Presenters must access View only through its interface, never through concrete Form types. This ensures testability and separation of concerns. This is Rule 7 of MVP design rules.");

        // Rule 8: No public methods in View unless in interface
        public static readonly DiagnosticDescriptor MVPRule08ViewMethodVisibility = new DiagnosticDescriptor(
            id: "MVP008",
            title: "Public View method not defined in interface",
            messageFormat: "Public method '{0}' in View '{1}' is not defined in View interface",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "View methods should not be public unless they are defined in the View interface. This is Rule 8 of MVP design rules.");

        // Rule 13: No UI control names in interface methods
        public static readonly DiagnosticDescriptor MVPRule13NoUIControlNames = new DiagnosticDescriptor(
            id: "MVP013",
            title: "Interface method name contains UI control type",
            messageFormat: "Method '{0}' in interface '{1}' contains UI control type '{2}'. Use domain-specific naming instead.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "View interface methods should use domain-specific names, not UI control type names (e.g., 'AddTaskGroup' not 'AddTreeViewNode'). This is Rule 13 of MVP design rules.");
    }
}
