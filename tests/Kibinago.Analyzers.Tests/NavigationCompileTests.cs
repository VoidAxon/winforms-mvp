using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Kibinago.Analyzers.Tests
{
    /// <summary>
    /// Compile-time guarantees of the navigation API, checked by compiling snippets against the real
    /// <c>Kibinago.Core</c> with C# 7.3 (the default for net472 projects). They live here because this
    /// is the project that already compiles code with Roslyn and runs on any OS.
    /// </summary>
    public class NavigationCompileTests
    {
        private const string Presenters = @"
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.Services;
using Kibinago.Views;

public interface IEditView : IWindowView { }
public interface IPanelView : IViewBase { }
public sealed class PlainPresenter : WindowPresenterBase<IEditView> { }
public sealed class ParamPresenter : WindowPresenterBase<IEditView, int> { protected override void OnInitialize(int id) { } }
public sealed class PanelPresenter : ControlPresenterBase<IPanelView> { }
";

        [Theory]
        [InlineData("nav.For(new PlainPresenter()).ShowAsModal();")]
        [InlineData("InteractionResult<int> r = nav.For(new PlainPresenter()).ShowAsModal<int>();")]
        [InlineData("nav.For(new PlainPresenter()).ShowWindow(key: 5);")]
        [InlineData("nav.For(new PlainPresenter()).ShowWindow<int>(owner, key: \"a\", onClosed: r => { });")]
        [InlineData("nav.For(new ParamPresenter()).WithParam(3).ShowAsModal<int>();")]
        [InlineData("nav.For(new ParamPresenter()).WithParam(3).ShowWindow<int>(key: 3, onClosed: r => { });")]
        [InlineData("nav.ShowWindowAsModal<PlainPresenter, int>(new PlainPresenter());")]
        [InlineData("nav.ShowWindow(new PlainPresenter(), key: 1);")]
        [InlineData("nav.ShowWindowAsModal<ParamPresenter, int, string>(new ParamPresenter(), 3);")]
        [InlineData("nav.ShowWindow(new ParamPresenter(), 3, key: 3);")]
        [InlineData("IWindowView v = nav.For(new PlainPresenter()).PrepareWindow();")]
        [InlineData("IWindowView v = nav.For(new PlainPresenter()).PrepareWindow<int>(key: 5, onClosed: r => { });")]
        [InlineData("IWindowView v = nav.For(new ParamPresenter()).WithParam(3).PrepareWindow(key: 3);")]
        [InlineData("IWindowView v = nav.For(new ParamPresenter()).WithParam(3).PrepareWindow<string>(onClosed: r => { });")]
        [InlineData("IWindowView v = nav.PrepareWindow(new PlainPresenter(), key: 1);")]
        [InlineData("IWindowView v = nav.PrepareWindow<ParamPresenter, int, string>(new ParamPresenter(), 3, key: 3, onClosed: r => { });")]
        public void ValidNavigation_Compiles(string call)
        {
            Assert.Empty(Errors(call));
        }

        [Theory]
        // A control presenter has no window.
        [InlineData("nav.For(new PanelPresenter());")]
        [InlineData("nav.ShowWindowAsModal<PanelPresenter, int>(new PanelPresenter());")]
        [InlineData("nav.ShowWindow(new PanelPresenter());")]
        // A presenter that needs parameters cannot be opened without them.
        [InlineData("nav.For(new ParamPresenter()).ShowAsModal<int>();")]
        [InlineData("nav.For(new ParamPresenter()).ShowWindow();")]
        [InlineData("nav.ShowWindowAsModal<ParamPresenter, int>(new ParamPresenter());")]
        [InlineData("nav.ShowWindow(new ParamPresenter());")]
        // Parameters of a type the presenter does not take.
        [InlineData("nav.For(new PlainPresenter()).WithParam(3);")]
        [InlineData("nav.For(new ParamPresenter()).WithParam(\"x\");")]
        [InlineData("nav.ShowWindowAsModal<ParamPresenter, string, int>(new ParamPresenter(), \"x\");")]
        [InlineData("nav.For(new ParamPresenter()).PrepareWindow();")]
        [InlineData("nav.PrepareWindow(new ParamPresenter());")]
        [InlineData("nav.PrepareWindow(new PanelPresenter());")]
        [InlineData("nav.For(new PlainPresenter()).PrepareWindow(owner: owner);")]   // no owner: the container places it
        public void InvalidNavigation_DoesNotCompile(string call)
        {
            Assert.NotEmpty(Errors(call));
        }

        private static Diagnostic[] Errors(string call)
        {
            var source = Presenters + @"
public static class Caller
{
    public static void Run(IWindowNavigator nav, IWindowView owner)
    {
        " + call + @"
    }
}";
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(System.IO.Path.PathSeparator)
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .Append(MetadataReference.CreateFromFile(typeof(Kibinago.Services.IWindowNavigator).Assembly.Location));

            var compilation = CSharpCompilation.Create(
                "NavigationSnippet",
                new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp7_3)) },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        }
    }
}
