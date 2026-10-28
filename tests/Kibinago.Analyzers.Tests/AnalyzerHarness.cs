using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kibinago.Analyzers.Tests
{
    /// <summary>
    /// Minimal in-process harness: compiles a snippet together with <see cref="Stubs"/> and runs
    /// every MVP analyzer over it. All analyzers run together so cross-rule de-duplication
    /// (e.g. MVP003 vs MVP014) is exercised too.
    /// </summary>
    internal static class AnalyzerHarness
    {
        /// <summary>
        /// Stand-ins for the WinForms and framework types the analyzers match by metadata name.
        /// The analyzer has no reference to either, so these are indistinguishable from the real ones.
        /// </summary>
        public const string Stubs = @"
namespace System.Windows.Forms
{
    public class Component { }
    public class Control : Component { }
    public class ButtonBase : Control { }
    public class Button : ButtonBase { }
    public class TextBox : Control { }
    public class Label : Control { }
    public class TreeView : Control { }
    public class ContainerControl : Control { }
    public class Form : ContainerControl { }
    public class UserControl : ContainerControl { }
    public class CommonDialog : Component { }
    public class OpenFileDialog : CommonDialog { }
    public enum DialogResult { None, OK, Cancel }
    public static class MessageBox { public static DialogResult Show(string text) => default; }
    public class FormClosingEventArgs : System.EventArgs { }
    public interface IMessageFilter { }
}

namespace Kibinago.Events
{
    public interface IEventAggregator
    {
        System.IDisposable Subscribe<T>(System.Action<T> handler);
        void Publish<T>(T message);
    }
    public class EventAggregator : IEventAggregator
    {
        public System.IDisposable Subscribe<T>(System.Action<T> handler) => null;
        public void Publish<T>(T message) { }
    }
}

namespace Kibinago.Common
{
    public sealed class CompositeDisposable : System.IDisposable
    {
        public void Add(System.IDisposable item) { }
        public void Dispose() { }
    }
    public static class DisposableExtensions
    {
        public static T DisposeWith<T>(this T disposable, CompositeDisposable bag) where T : System.IDisposable => disposable;
    }
    public sealed class Debouncer : System.IDisposable
    {
        public Debouncer(System.TimeSpan delay, System.Action action) { }
        public void Signal() { }
        public void Dispose() { }
    }
    public sealed class Throttler : System.IDisposable
    {
        public Throttler(System.TimeSpan interval, System.Action action) { }
        public void Signal() { }
        public void Dispose() { }
    }
    public static class RateLimit
    {
        public static Debouncer Debounce(int milliseconds, System.Action action) => new Debouncer(System.TimeSpan.FromMilliseconds(milliseconds), action);
        public static Throttler Throttle(int milliseconds, System.Action action) => new Throttler(System.TimeSpan.FromMilliseconds(milliseconds), action);
    }
}

namespace Kibinago.Views
{
    public interface IViewBase { }
    public interface IWindowView : IViewBase { }
}

namespace Kibinago.Presenters
{
    using Kibinago.Views;

    public abstract class PresenterBase<TView> where TView : IViewBase
    {
        protected TView View { get; private set; }
        protected Kibinago.Common.CompositeDisposable Disposables { get; } = new Kibinago.Common.CompositeDisposable();
        protected virtual void Cleanup() { }
    }

    public abstract class WindowPresenterBase<TView> : PresenterBase<TView> where TView : IWindowView { }
    public abstract class ControlPresenterBase<TView> : PresenterBase<TView> where TView : IViewBase { }
}
";

        private static readonly ImmutableArray<MetadataReference> References =
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .ToImmutableArray();

        private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
            typeof(DiagnosticDescriptors).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t))
                .Select(t => (DiagnosticAnalyzer)Activator.CreateInstance(t))
                .ToImmutableArray();

        /// <summary>
        /// Returns the MVP diagnostics for <paramref name="source"/>, ordered by position.
        /// Fails if the snippet itself does not compile, so a typo can't masquerade as "no diagnostic".
        /// </summary>
        /// <param name="designer">Optional second part compiled as <c>Test.Designer.cs</c> (generated code).</param>
        public static ImmutableArray<Diagnostic> Analyze(string source, string designer = null)
        {
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Diagnose);
            var trees = new[]
            {
                CSharpSyntaxTree.ParseText(Stubs, parseOptions, path: "Stubs.cs"),
                CSharpSyntaxTree.ParseText(source, parseOptions, path: "Test.cs"),
            };
            if (designer != null)
                trees = trees.Append(CSharpSyntaxTree.ParseText(designer, parseOptions, path: "Test.Designer.cs")).ToArray();

            var compilation = CSharpCompilation.Create(
                "AnalyzerTest",
                trees,
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var errors = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();
            if (errors.Count > 0)
                throw new InvalidOperationException(
                    "Test source does not compile:\n" + string.Join("\n", errors));

            return compilation.WithAnalyzers(Analyzers)
                .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult()
                .Where(d => d.Location.SourceTree?.FilePath == "Test.cs")
                .OrderBy(d => d.Location.SourceSpan.Start)
                .ThenBy(d => d.Id)
                .ToImmutableArray();
        }

        /// <summary>Diagnostic IDs for <paramref name="source"/>, in source order.</summary>
        public static string[] Ids(string source, string designer = null) =>
            Analyze(source, designer).Select(d => d.Id).ToArray();

        /// <summary>"ID:flagged text" pairs, for asserting where a diagnostic lands.</summary>
        public static string[] IdsWithText(string source) =>
            Analyze(source)
                .Select(d => d.Id + ":" + d.Location.SourceTree.GetText().ToString(d.Location.SourceSpan))
                .ToArray();
    }
}
