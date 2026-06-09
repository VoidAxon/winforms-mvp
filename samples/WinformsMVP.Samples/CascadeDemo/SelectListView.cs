using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Samples.CascadeDemo
{
    /// <summary>A list view of <typeparamref name="T"/> with single selection.</summary>
    public interface ISelectListView<T> : IViewBase where T : class
    {
        /// <summary>Replaces the list. Setting this MUST NOT raise <see cref="SelectionChanged"/>.</summary>
        IList<T> Items { set; }

        /// <summary>The user-selected item, or null.</summary>
        T Selected { get; }

        /// <summary>Raised only when the USER changes the selection (not on repopulation).</summary>
        event EventHandler SelectionChanged;
    }

    /// <summary>ListBox-backed control. A title label on top, the list filling the rest.</summary>
    public sealed class SelectListControl<T> : UserControl, ISelectListView<T> where T : class
    {
        private readonly ListBox _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly Label _title = new Label { Dock = DockStyle.Top, Height = 22 };
        private bool _suppress;

        public SelectListControl(string title)
        {
            _title.Text = title;
            Controls.Add(_list);
            Controls.Add(_title);
            // Only a genuine user change should flow to the store; repopulation must not.
            _list.SelectedIndexChanged += delegate
            {
                if (_suppress) return;
                var h = SelectionChanged;
                if (h != null) h(this, EventArgs.Empty);
            };
        }

        public IList<T> Items
        {
            set
            {
                _suppress = true;
                try
                {
                    _list.Items.Clear();
                    if (value != null)
                        foreach (var item in value)
                            _list.Items.Add(item);
                    _list.ClearSelected();
                }
                finally { _suppress = false; }
            }
        }

        public T Selected { get { return _list.SelectedItem as T; } }

        public event EventHandler SelectionChanged;
    }
}
