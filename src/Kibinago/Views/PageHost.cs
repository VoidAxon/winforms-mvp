using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.Services;

namespace Kibinago.Views
{
    /// <summary>
    /// A panel that shows the top page of a <see cref="PageStack"/>: put it on the Form (or UserControl) in the designer
    /// and expose it through the view interface as <see cref="IPageHost"/> (<c>public IPageHost Pages =&gt; pageHost1;</c>).
    /// Pages are created through the view mapping (<c>register.Register&lt;IOrderEditView, OrderEditControl&gt;()</c>), with the
    /// same lookup the window navigator uses; the pages below the top stay alive, hidden.
    /// </summary>
    /// <remarks>
    /// <para>In the designer it is a plain control: nothing can be dropped into it (it manages its own children).</para>
    /// <para>The breadcrumb is the view's: redraw it from <see cref="PageTitles"/> on <see cref="PagesChanged"/> and turn a
    /// crumb click into a view event / action the presenter answers with <c>PopToCountAsync(level + 1)</c>.</para>
    /// </remarks>
    // The designer treats it as a plain control, not a container: controls dropped into it at design time would mix
    // with the pages it manages. (Panel's own designer would accept them.)
    [Designer("System.Windows.Forms.Design.ControlDesigner, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
    public class PageHost : Panel, IPageHost
    {
        private readonly List<Page> _pages = new List<Page>();

        private sealed class Page
        {
            public Control Control;
            public string Title;
        }

        /// <summary>The pages' breadcrumb texts, bottom first.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<string> PageTitles => _pages.Select(p => p.Title).ToList();

        /// <summary>Raised after a page was added or removed (redraw the breadcrumb from <see cref="PageTitles"/>).</summary>
        public event EventHandler PagesChanged;

        /// <inheritdoc />
        public IControlView AddPage(Type viewInterface, string title, IServiceProvider services)
        {
            if (viewInterface == null) throw new ArgumentNullException(nameof(viewInterface));
            if (title == null) throw new ArgumentNullException(nameof(title));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);

            var register = services.GetService(typeof(IViewMappingRegister)) as IViewMappingRegister
                ?? throw new InvalidOperationException(
                    "No IViewMappingRegister in the presenter's services: register the page views at startup (MvpApplication / AddKibinago).");
            var control = Validate(register.WithServiceProvider(services).CreateInstance(viewInterface), viewInterface);

            var previous = _pages.Count > 0 ? _pages[_pages.Count - 1].Control : null;
            var page = new Page { Control = control, Title = title };
            bool listed = false;
            try
            {
                control.Dock = DockStyle.Fill;
                control.Visible = true;
                Controls.Add(control);
                control.BringToFront();
                _pages.Add(page);
                listed = true;
                if (previous != null) previous.Visible = false;   // hidden, not removed: it keeps its state
                control.SelectNextControl(control, true, true, true, true);
                PagesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception failure)
            {
                // Undo, so a failed add leaves the host as it was (the stack has nothing to compensate): the previous
                // top is shown again, the new control disposed, and — if the list had changed — the breadcrumb told
                // the restored state. Every failure on the way is reported with the original one.
                var failures = new List<Exception> { failure };
                _pages.Remove(page);
                try { if (previous != null && !previous.IsDisposed) previous.Visible = true; }
                catch (Exception restore) { failures.Add(restore); }
                try
                {
                    if (!control.IsDisposed)
                    {
                        Controls.Remove(control);
                        control.Dispose();
                    }
                }
                catch (Exception cleanup) { failures.Add(cleanup); }
                if (listed)
                {
                    try { PagesChanged?.Invoke(this, EventArgs.Empty); }
                    catch (Exception notify) { failures.Add(notify); }
                }
                if (failures.Count > 1) throw new AggregateException(failures);
                throw;
            }
            return (IControlView)control;
        }

        /// <inheritdoc />
        public void RemovePage(IControlView page)
        {
            int index = _pages.FindIndex(p => ReferenceEquals(p.Control, page));
            if (index < 0) throw new ArgumentException("This host did not add that page (or removed it already).", nameof(page));
            var control = _pages[index].Control;
            _pages.RemoveAt(index);
            // Whatever the disposal does, the host shows its new top and tells the breadcrumb; failures are collected.
            var failures = new List<Exception>();
            try
            {
                if (!control.IsDisposed)
                {
                    Controls.Remove(control);
                    control.Dispose();
                }
            }
            catch (Exception ex) { failures.Add(ex); }
            try
            {
                if (_pages.Count > 0)
                {
                    var top = _pages[_pages.Count - 1].Control;
                    if (!top.IsDisposed) top.Visible = true;   // its VisibleChanged handlers may throw
                }
            }
            catch (Exception ex) { failures.Add(ex); }
            try { PagesChanged?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex) { failures.Add(ex); }
            if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException(failures);
        }

        // A new, unplaced control implementing the interface — checked before the host changes anything. A rejected
        // instance is never disposed: the mapping (a factory) may return something shared, so it is not ours.
        private static Control Validate(object instance, Type viewInterface)
        {
            if (instance == null)
                throw new InvalidOperationException("The view mapping for " + viewInterface.Name + " returned null.");
            if (instance is IView asView && ViewOwnership.IsClaimed(asView))
                throw new InvalidOperationException(
                    "The view mapping for " + viewInterface.Name + " returned a view another live presenter uses; a page " +
                    "needs a new control each time (register a type or a factory that creates one).");
            var control = instance as Control;
            if (control == null || control is Form || !viewInterface.IsInstanceOfType(instance) || !(instance is IControlView))
                throw new InvalidOperationException(
                    "The view mapping for " + viewInterface.Name + " made a " + instance.GetType().Name +
                    "; a page view is a UserControl (not a Form) that implements " + viewInterface.Name + ".");
            if (control.IsDisposed)
                throw new InvalidOperationException("The view mapping for " + viewInterface.Name + " returned a disposed control.");
            if (control.Parent != null)
                throw new InvalidOperationException(
                    "The view mapping for " + viewInterface.Name + " returned a control that is already placed elsewhere; a page " +
                    "needs a new control each time (register a type or a factory that creates one).");
            return control;
        }
    }
}
