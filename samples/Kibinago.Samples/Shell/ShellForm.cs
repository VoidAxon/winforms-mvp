using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Samples.Shell
{
    /// <summary>
    /// The sample's main window: the categories on the left, the selected category's tabs on the right.
    /// Which control or window belongs to a demo id is in <see cref="DemoViews"/>; the presenter knows only ids.
    /// </summary>
    public partial class ShellForm : Form, IShellView
    {
        private const string WikiBase = "https://github.com/VoidAxon/winforms-mvp/wiki/";

        private readonly Dictionary<string, Control> _demoViews = new Dictionary<string, Control>();
        private IReadOnlyList<SampleCategory> _categories = Array.Empty<SampleCategory>();
        private bool _showingCategory;
        private bool _selectingCategory;

        public ShellForm()
        {
            InitializeComponent();

            _categoryList.DrawItem += DrawCategory;
            _categoryList.SelectedIndexChanged += (s, e) =>
            {
                if (!_selectingCategory && _categoryList.SelectedIndex >= 0)
                    CategorySelected?.Invoke(this, _categories[_categoryList.SelectedIndex].Id);
            };
            _tabs.SelectedIndexChanged += (s, e) =>
            {
                if (!_showingCategory && _tabs.SelectedTab?.Tag is string demoId)
                    DemoSelected?.Invoke(this, demoId);
            };
            _sourceFileList.SelectedIndexChanged += (s, e) => ShowSelectedSource();
        }

        /// <summary>Started with <c>--profile custom</c>: the customized framework configuration is active.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool CustomProfile
        {
            get => _customProfile;
            set
            {
                _customProfile = value;
                Text = value ? "Kibinago サンプル (カスタム設定)" : "Kibinago サンプル";
            }
        }
        private bool _customProfile;

        #region IShellView

        public event EventHandler<string> CategorySelected;
        public event EventHandler<string> DemoSelected;

        // The shell has no actions of its own: the sidebar and the tabs raise view events.
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => null;

        public void ShowCategories(IReadOnlyList<SampleCategory> categories)
        {
            _categories = categories;
            _categoryList.Items.Clear();
            foreach (var category in categories)
                _categoryList.Items.Add(category.Title);
        }

        public void SelectCategory(string categoryId)
        {
            var index = _categories.ToList().FindIndex(c => c.Id == categoryId);
            if (index < 0)
                return;

            // Raised here rather than by the list, which may not report a change before its handle exists.
            _selectingCategory = true;
            try { _categoryList.SelectedIndex = index; }
            finally { _selectingCategory = false; }
            CategorySelected?.Invoke(this, categoryId);
        }

        public void ShowCategory(SampleCategory category)
        {
            _showingCategory = true;
            try
            {
                _tabs.SuspendLayout();
                // The source viewer lives across categories: take it out of the old コード tab first.
                _sourceText.Parent?.Controls.Remove(_sourceText);
                _sourceFileList.Parent?.Controls.Remove(_sourceFileList);
                var old = _tabs.TabPages.Cast<TabPage>().ToList();
                _tabs.TabPages.Clear();
                foreach (var page in old)
                    page.Dispose();

                _tabs.TabPages.Add(CreateExplanationPage(category));
                foreach (var demo in category.Demos)
                    _tabs.TabPages.Add(CreateDemoPage(demo));
                _tabs.TabPages.Add(CreateSourcePage());
                _tabs.SelectedIndex = 0;
                _tabs.ResumeLayout();
            }
            finally
            {
                _showingCategory = false;
            }
        }

        public IView CreateDemoView(string demoId)
        {
            var page = _tabs.TabPages.Cast<TabPage>().First(p => (string)p.Tag == demoId);
            var control = DemoViews.Create(demoId);
            control.Dock = DockStyle.Fill;
            page.Controls.Add(control);
            _demoViews[demoId] = control;
            return (IView)control;
        }

        public void RemoveDemoView(string demoId)
        {
            if (_demoViews.TryGetValue(demoId, out var control))
            {
                _demoViews.Remove(demoId);
                control.Parent?.Controls.Remove(control);
                control.Dispose();
            }
        }

        public void RemoveDemoViews()
        {
            foreach (var demoId in _demoViews.Keys.ToList())
                RemoveDemoView(demoId);
        }

        public void ShowSource(IReadOnlyList<string> sourceFiles)
        {
            _sourceFileList.Items.Clear();
            var names = sourceFiles.GroupBy(System.IO.Path.GetFileName).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            foreach (var file in sourceFiles)
                _sourceFileList.Items.Add(new SourceFile(file, withFolder: names.Contains(System.IO.Path.GetFileName(file))));
            if (_sourceFileList.Items.Count > 0)
                _sourceFileList.SelectedIndex = 0;
            else
                _sourceText.ShowText("");
        }

        #endregion

        private void ShowSelectedSource()
        {
            var path = (_sourceFileList.SelectedItem as SourceFile)?.Path;
            _sourceText.ShowText(path == null ? "" : (SampleSources.Read(path) ?? $"// {path} は埋め込まれていません。"));
        }

        // A file in the コード tab's list: shown by its name (the class), plus its folder when two files share
        // a name; opened by its path.
        private sealed class SourceFile
        {
            private readonly string _text;

            public SourceFile(string path, bool withFolder)
            {
                Path = path;
                var name = System.IO.Path.GetFileName(path);
                _text = withFolder ? $"{name}  ({System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path))})" : name;
            }

            public string Path { get; }
            public override string ToString() => _text;
        }

        private TabPage CreateExplanationPage(SampleCategory category)
        {
            var page = new TabPage("説明") { BackColor = Color.White, Padding = new Padding(24) };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
            };
            flow.Controls.Add(new Label
            {
                Text = category.Title,
                Font = new Font(Font.FontFamily, 18f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12),
            });
            flow.Controls.Add(new Label
            {
                Text = category.Explanation,
                AutoSize = true,
                MaximumSize = new Size(820, 0),
                Font = new Font(Font.FontFamily, 11f),
                Margin = new Padding(0, 0, 0, 16),
            });
            if (category.Links.Count > 0)
                flow.Controls.Add(new Label { Text = "関連ページ (wiki)", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 0, 0, 4) });
            foreach (var link in category.Links)
            {
                var label = new LinkLabel { Text = link.Title, AutoSize = true, Font = new Font(Font.FontFamily, 11f), Margin = new Padding(12, 2, 0, 2) };
                var url = WikiBase + link.Page;
                label.LinkClicked += (s, e) => Process.Start(url);
                flow.Controls.Add(label);
            }
            page.Controls.Add(flow);
            return page;
        }

        private TabPage CreateDemoPage(SampleDemo demo)
        {
            var page = new TabPage(demo.Title) { Tag = demo.Id, BackColor = Color.White };
            if (demo.Kind == DemoKind.Embedded)
                return page;   // the control is created the first time the tab is opened (CreateDemoView)

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(24),
            };
            flow.Controls.Add(new Label
            {
                Text = demo.Title,
                Font = new Font(Font.FontFamily, 14f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
            });
            flow.Controls.Add(new Label
            {
                Text = demo.Description,
                AutoSize = true,
                MaximumSize = new Size(820, 0),
                Font = new Font(Font.FontFamily, 11f),
                Margin = new Padding(0, 0, 0, 16),
            });
            var open = new Button
            {
                Text = demo.Id == DemoIds.Customization && !CustomProfile ? "カスタム設定で別プロセスを起動" : "開く",
                AutoSize = true,
                MinimumSize = new Size(140, 36),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            open.FlatAppearance.BorderSize = 0;
            open.Click += (s, e) => DemoViews.Open(demo.Id, this);
            flow.Controls.Add(open);
            page.Controls.Add(flow);
            return page;
        }

        private TabPage CreateSourcePage()
        {
            var page = new TabPage("コード") { BackColor = Color.White };
            page.Controls.Add(_sourceText);
            page.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Color.Gainsboro });
            page.Controls.Add(_sourceFileList);
            return page;
        }

        private void DrawCategory(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _categories.Count)
                return;
            var category = _categories[e.Index];
            var selected = (e.State & DrawItemState.Selected) != 0;
            using (var background = new SolidBrush(selected ? Color.White : _categoryList.BackColor))
                e.Graphics.FillRectangle(background, e.Bounds);
            if (selected)
                using (var accent = new SolidBrush(Color.FromArgb(0, 120, 215)))
                    e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top + 8, 4, e.Bounds.Height - 16);

            using (var titleFont = new Font(Font.FontFamily, 11f, FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, category.Title, titleFont,
                    new Rectangle(e.Bounds.Left + 16, e.Bounds.Top + 7, e.Bounds.Width - 20, 22), Color.FromArgb(32, 32, 32),
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            TextRenderer.DrawText(e.Graphics, category.Summary, Font,
                new Rectangle(e.Bounds.Left + 16, e.Bounds.Top + 30, e.Bounds.Width - 20, 20), Color.DimGray,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
