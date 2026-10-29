using System;
using System.Collections.Generic;
using System.ComponentModel;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Services;
using Kibinago.Testing;

namespace Kibinago.Samples.Tests.Composition
{
    // A parent edit window with one fixed panel, and a shared input model.

    public sealed class EditInput : INotifyPropertyChanged
    {
        private string _name;
        public event PropertyChangedEventHandler PropertyChanged;
        public string Name
        {
            get => _name;
            set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
        }
    }

    public static class PanelActions
    {
        public static readonly ViewAction Fill = ViewAction.Create("Panel.Fill");
    }

    public interface INamePanelView : IView, IControlView
    {
        string Shown { set; }
    }

    public sealed class FakeNamePanelView : INamePanelView
    {
        public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
        public IViewActionBinder ActionBinder => Binder;
        public string Displayed;
        public string Shown { set => Displayed = value; }
    }

    public sealed class NameSource
    {
        public string Next = "Alice";
    }

    /// <summary>A panel presenter with a business dependency and the shared input as its parameter.</summary>
    public sealed class NamePanelPresenter : ControlPresenterBase<INamePanelView, EditInput>
    {
        private readonly NameSource _source;
        private readonly List<string> _log;
        private readonly string _tag;
        private EditInput _input;
        public Exception ThrowOnInitialize;
        public Exception ThrowOnCleanup;
        public bool Disposed;
        public IMessageService SeenMessages;

        public NamePanelPresenter(NameSource source, List<string> log = null, string tag = "panel",
            bool useServicesInConstructor = false)
        {
            _source = source;
            _log = log;
            _tag = tag;
            if (useServicesInConstructor) { var _ = Services; }   // fixes the ambient provider (a mistake)
        }

        protected override void RegisterViewActions()
            => Dispatcher.Register(PanelActions.Fill, () => { _input.Name = _source.Next; SeenMessages = Messages; });

        protected override void OnInitialize(EditInput input)
        {
            if (ThrowOnInitialize != null) throw ThrowOnInitialize;
            _input = input;
            View.Shown = input.Name;
        }

        protected override void Cleanup()
        {
            Disposed = true;
            _log?.Add("cleanup:" + _tag);
            if (ThrowOnCleanup != null) throw ThrowOnCleanup;
        }
    }

    public interface IEditView : IWindowView
    {
        INamePanelView NamePanel { get; }
        INamePanelView OtherPanel { get; }
    }

    public sealed class FakeEditView : IEditView
    {
        public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
        public IViewActionBinder ActionBinder => Binder;
        public FakeNamePanelView NamePanel { get; } = new FakeNamePanelView();
        public FakeNamePanelView OtherPanel { get; } = new FakeNamePanelView();
        INamePanelView IEditView.NamePanel => NamePanel;
        INamePanelView IEditView.OtherPanel => OtherPanel;
    }

    /// <summary>A parent whose composition is configured per test.</summary>
    public sealed class EditPresenter : WindowPresenterBase<IEditView, EditInput>
    {
        private readonly Action<EditPresenter, EditInput> _compose;
        private readonly List<string> _log;
        public readonly List<IDisposable> Handles = new List<IDisposable>();
        public Action OnCleanup;

        public EditPresenter(Action<EditPresenter, EditInput> compose, List<string> log = null)
        {
            _compose = compose;
            _log = log;
        }

        protected override void OnInitialize(EditInput input)
        {
            _compose(this, input);
            if (_log != null) Disposable.Create(() => _log.Add("disposables:parent")).DisposeWith(Disposables);
        }

        protected override void Cleanup()
        {
            _log?.Add("cleanup:parent");
            OnCleanup?.Invoke();
        }

        // Test access to the protected API (the presenter's own code calls it in real screens).
        internal IDisposable Attach(NamePanelPresenter child, INamePanelView view, EditInput input)
        {
            var handle = AttachChild(child, view, input);
            Handles.Add(handle);
            return handle;
        }
    }
}
