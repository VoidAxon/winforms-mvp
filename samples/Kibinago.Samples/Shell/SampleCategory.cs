using System;
using System.Collections.Generic;
using Kibinago.Presenters;
using Kibinago.Views;

namespace Kibinago.Samples.Shell
{
    /// <summary>One entry of the sidebar: a feature area with its explanation and demos.</summary>
    public sealed class SampleCategory
    {
        public SampleCategory(string id, string title, string summary, string explanation,
            IReadOnlyList<WikiLink> links, IReadOnlyList<SampleDemo> demos, IReadOnlyList<string> sourceFiles = null)
        {
            Id = id;
            Title = title;
            Summary = summary;
            Explanation = explanation;
            Links = links ?? Array.Empty<WikiLink>();
            Demos = demos ?? Array.Empty<SampleDemo>();
            SourceFiles = sourceFiles ?? Array.Empty<string>();
        }

        public string Id { get; }
        public string Title { get; }

        /// <summary>One line under the title in the sidebar.</summary>
        public string Summary { get; }

        /// <summary>The 説明 tab's text.</summary>
        public string Explanation { get; }

        public IReadOnlyList<WikiLink> Links { get; }
        public IReadOnlyList<SampleDemo> Demos { get; }

        /// <summary>Source files of the category itself (not of a demo), shown in the コード tab.</summary>
        public IReadOnlyList<string> SourceFiles { get; }
    }

    /// <summary>A wiki page of the repository (<c>wiki/{Page}.md</c>).</summary>
    public sealed class WikiLink
    {
        public WikiLink(string title, string page)
        {
            Title = title;
            Page = page;
        }

        public string Title { get; }
        public string Page { get; }
    }

    /// <summary>How a demo appears in its tab.</summary>
    public enum DemoKind
    {
        /// <summary>The demo runs inside the tab: a UserControl with a child presenter.</summary>
        Embedded,

        /// <summary>The tab explains the demo and opens its own window (it is about windows, or not migrated yet).</summary>
        Window,
    }

    /// <summary>One tab of a category.</summary>
    public sealed class SampleDemo
    {
        private SampleDemo(string id, string title, string description, DemoKind kind,
            Func<IDemoAttacher, IViewBase, IDisposable> attach, IReadOnlyList<string> sourceFiles)
        {
            Id = id;
            Title = title;
            Description = description;
            Kind = kind;
            Attach = attach;
            SourceFiles = sourceFiles ?? Array.Empty<string>();
        }

        /// <summary>
        /// A demo that runs in the tab. <paramref name="attach"/> gets the view the shell's view created for
        /// <paramref name="id"/> and attaches the demo's presenter to it, e.g.
        /// <c>(a, v) =&gt; a.Attach(new ToDoDemoPresenter(), (IToDoView)v)</c>.
        /// </summary>
        public static SampleDemo Embedded(string id, string title, string description,
            Func<IDemoAttacher, IViewBase, IDisposable> attach, params string[] sourceFiles) =>
            new SampleDemo(id, title, description, DemoKind.Embedded, attach ?? throw new ArgumentNullException(nameof(attach)), sourceFiles);

        /// <summary>
        /// A demo that runs in the tab with no presenter: what it shows is a view concern (positioning,
        /// painting, UI timing), so the control is the whole demo.
        /// </summary>
        public static SampleDemo ViewOnly(string id, string title, string description, params string[] sourceFiles) =>
            new SampleDemo(id, title, description, DemoKind.Embedded, (a, v) => NoPresenter.Instance, sourceFiles);

        /// <summary>A demo that opens its own window from a button in the tab.</summary>
        public static SampleDemo Window(string id, string title, string description, params string[] sourceFiles) =>
            new SampleDemo(id, title, description, DemoKind.Window, null, sourceFiles);

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public DemoKind Kind { get; }

        /// <summary>For <see cref="DemoKind.Embedded"/>: attaches the demo's presenter to its view.</summary>
        public Func<IDemoAttacher, IViewBase, IDisposable> Attach { get; }

        /// <summary>Paths relative to the sample project (<c>ToDoDemo/ToDoDemoPresenter.cs</c>).</summary>
        public IReadOnlyList<string> SourceFiles { get; }
    }

    /// <summary>What a catalog entry uses to attach its presenter as a child of the shell's presenter.</summary>
    public interface IDemoAttacher
    {
        IDisposable Attach<TView>(ControlPresenterBase<TView> presenter, TView view) where TView : IViewBase;
    }

    // The "handle" of a view-only demo: nothing to release.
    internal sealed class NoPresenter : IDisposable
    {
        public static readonly NoPresenter Instance = new NoPresenter();
        public void Dispose() { }
    }
}
