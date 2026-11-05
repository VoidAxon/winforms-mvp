using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Samples.PageNavigationDemo
{
    /// <summary>
    /// A row of LinkLabels as the breadcrumb over a PageHost. The host creates, hides and removes the page controls;
    /// this view only redraws the crumbs from <see cref="PageHost.PageTitles"/>. A crumb click is the typed action
    /// Breadcrumb with its level. Swapping the breadcrumb control (AntdUI Breadcrumb, …) changes only this class.
    /// </summary>
    public partial class PageNavigationDemoControl : UserControl, IPageNavigationDemoView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();
        private readonly List<Control> _crumbs = new List<Control>();
        private Font _currentCrumbFont;   // one bold font for the current crumb, disposed with the control

        public PageNavigationDemoControl()
        {
            InitializeComponent();
            _pageHost.PagesChanged += (s, e) => RebuildCrumbs();
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IPageHost Pages => _pageHost;

        public void ShowStatus(string text) => _statusLabel.Text = text;

        private void RebuildCrumbs()
        {
            foreach (var crumb in _crumbs)
            {
                if (crumb is LinkLabel link) _binder.Remove(link);
                _breadcrumb.Controls.Remove(crumb);
                crumb.Dispose();
            }
            _crumbs.Clear();

            var titles = _pageHost.PageTitles;
            for (int i = 0; i < titles.Count; i++)
            {
                if (i > 0) AddCrumb(new Label { Text = "›", AutoSize = true, Margin = new Padding(2, 6, 2, 0) });
                bool current = i == titles.Count - 1;
                var link = new LinkLabel
                {
                    Text = titles[i],
                    AutoSize = true,
                    Margin = new Padding(2, 6, 2, 0),
                    LinkColor = current ? SystemColors.ControlText : Color.FromArgb(0, 102, 204),
                    LinkBehavior = current ? LinkBehavior.NeverUnderline : LinkBehavior.HoverUnderline,
                    Font = current ? CurrentCrumbFont : Font,
                };
                AddCrumb(link);
                int level = i;
                _binder.Add(PageNavigationDemoActions.Breadcrumb, link, c => level);
            }
        }

        private void AddCrumb(Control crumb)
        {
            _crumbs.Add(crumb);
            _breadcrumb.Controls.Add(crumb);
        }

        private Font CurrentCrumbFont => _currentCrumbFont ?? (_currentCrumbFont = new Font(Font, FontStyle.Bold));

        protected override void Dispose(bool disposing)
        {
            if (disposing) _currentCrumbFont?.Dispose();
            base.Dispose(disposing);
        }
    }
}
