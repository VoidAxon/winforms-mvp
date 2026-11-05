using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Samples.PageNavigationDemo
{
    /// <summary>
    /// The page host: a row of LinkLabels as the breadcrumb, the pages stacked in one panel (only the top one
    /// visible; the ones below keep their state). A crumb click is the typed action Breadcrumb with its level.
    /// Swapping the breadcrumb control (AntdUI Breadcrumb, …) changes only this class.
    /// </summary>
    public partial class PageNavigationDemoControl : UserControl, IPageNavigationDemoView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();
        private readonly List<Page> _pages = new List<Page>();
        private Font _currentCrumbFont;   // one bold font for the current crumb, disposed with the control

        private sealed class Page
        {
            public Control View;
            public LinkLabel Crumb;
            public Label Separator;
        }

        public PageNavigationDemoControl()
        {
            InitializeComponent();
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        public ICustomerListView PushCustomerList() => Push(new CustomerListControl(), "顧客一覧");

        public ICustomerDetailView PushCustomerDetail(string title) => Push(new CustomerDetailControl(), title);

        public IOrderEditView PushOrderEdit(string title) => Push(new OrderEditControl(), title);

        public void RemovePage(IControlView page)
        {
            int index = _pages.FindIndex(p => ReferenceEquals(p.View, page));
            if (index < 0) return;
            var removed = _pages[index];
            _pages.RemoveAt(index);

            _binder.Remove(removed.Crumb);
            _breadcrumb.Controls.Remove(removed.Crumb);
            removed.Crumb.Dispose();
            if (removed.Separator != null)
            {
                _breadcrumb.Controls.Remove(removed.Separator);
                removed.Separator.Dispose();
            }
            if (!removed.View.IsDisposed)   // it may have been disposed elsewhere
            {
                _pageHost.Controls.Remove(removed.View);
                removed.View.Dispose();
            }

            if (_pages.Count > 0) _pages[_pages.Count - 1].View.Visible = true;
            UpdateCrumbs();
        }

        public void ShowStatus(string text) => _statusLabel.Text = text;

        private T Push<T>(T view, string title) where T : Control
        {
            if (_pages.Count > 0) _pages[_pages.Count - 1].View.Visible = false;   // hidden, not removed

            view.Dock = DockStyle.Fill;
            _pageHost.Controls.Add(view);
            view.BringToFront();

            var page = new Page { View = view };
            if (_pages.Count > 0)
            {
                page.Separator = new Label { Text = "›", AutoSize = true, Margin = new Padding(2, 6, 2, 0) };
                _breadcrumb.Controls.Add(page.Separator);
            }
            int level = _pages.Count;
            page.Crumb = new LinkLabel { Text = title, AutoSize = true, Margin = new Padding(2, 6, 2, 0) };
            _breadcrumb.Controls.Add(page.Crumb);
            _binder.Add(PageNavigationDemoActions.Breadcrumb, page.Crumb, c => level);
            _pages.Add(page);
            UpdateCrumbs();
            return view;
        }

        private void UpdateCrumbs()
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                bool current = i == _pages.Count - 1;
                var crumb = _pages[i].Crumb;
                crumb.LinkColor = current ? SystemColors.ControlText : Color.FromArgb(0, 102, 204);
                crumb.LinkBehavior = current ? LinkBehavior.NeverUnderline : LinkBehavior.HoverUnderline;
                crumb.Font = current ? CurrentCrumbFont : Font;
            }
        }

        private Font CurrentCrumbFont => _currentCrumbFont ?? (_currentCrumbFont = new Font(Font, FontStyle.Bold));

        protected override void Dispose(bool disposing)
        {
            if (disposing) _currentCrumbFont?.Dispose();
            base.Dispose(disposing);
        }
    }
}
