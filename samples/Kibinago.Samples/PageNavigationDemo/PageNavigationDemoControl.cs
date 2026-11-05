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
        private readonly List<LinkLabel> _crumbs = new List<LinkLabel>();
        private readonly List<Label> _separators = new List<Label>();
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

        // Only the difference changes: a jump removes the crumbs above the kept level, a push adds one; the crumb that
        // was clicked is kept (it is not disposed inside its own Click).
        private void RebuildCrumbs()
        {
            var titles = _pageHost.PageTitles;
            while (_crumbs.Count > titles.Count) RemoveLastCrumb();
            for (int i = 0; i < _crumbs.Count; i++) _crumbs[i].Text = titles[i];
            while (_crumbs.Count < titles.Count) AddCrumb(titles[_crumbs.Count]);
            for (int i = 0; i < _crumbs.Count; i++)
            {
                bool current = i == _crumbs.Count - 1;
                var link = _crumbs[i];
                link.LinkColor = current ? SystemColors.ControlText : Color.FromArgb(0, 102, 204);
                link.LinkBehavior = current ? LinkBehavior.NeverUnderline : LinkBehavior.HoverUnderline;
                link.Font = current ? CurrentCrumbFont : Font;
            }
        }

        private void AddCrumb(string title)
        {
            if (_crumbs.Count > 0)
            {
                var separator = new Label { Text = "›", AutoSize = true, Margin = new Padding(2, 6, 2, 0) };
                _separators.Add(separator);
                _breadcrumb.Controls.Add(separator);
            }
            var link = new LinkLabel { Text = title, AutoSize = true, Margin = new Padding(2, 6, 2, 0) };
            int level = _crumbs.Count;
            _crumbs.Add(link);
            _breadcrumb.Controls.Add(link);
            _binder.Add(PageNavigationDemoActions.Breadcrumb, link, c => level);
        }

        private void RemoveLastCrumb()
        {
            var link = _crumbs[_crumbs.Count - 1];
            _crumbs.RemoveAt(_crumbs.Count - 1);
            _binder.Remove(link);
            _breadcrumb.Controls.Remove(link);
            link.Dispose();
            if (_separators.Count > 0)
            {
                var separator = _separators[_separators.Count - 1];
                _separators.RemoveAt(_separators.Count - 1);
                _breadcrumb.Controls.Remove(separator);
                separator.Dispose();
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
