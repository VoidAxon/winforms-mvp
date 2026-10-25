using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Common.Extensions;
using Xunit;

namespace Kibinago.Samples.Tests.Common
{
    /// <summary>
    /// The short BindProperty overload and the typed Bind helpers bind with formatting enabled:
    /// interface-typed values convert, and bad input is reported through BindingComplete instead of
    /// throwing out of the control's event.
    /// </summary>
    public class BindingFormattingDefaultTests
    {
        private sealed class Input : INotifyPropertyChanged
        {
            private IReadOnlyList<int> _ids = new int[0];
            private int _price;
            private string _name;

            public event PropertyChangedEventHandler PropertyChanged;

            public IReadOnlyList<int> Ids { get => _ids; set { _ids = value; Raise(nameof(Ids)); } }
            public int Price { get => _price; set { _price = value; Raise(nameof(Price)); } }
            public string Name { get => _name; set { _name = value; Raise(nameof(Name)); } }

            private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>A custom control with an interface-typed property and the conventional {Prop}Changed event.</summary>
        private sealed class TagPicker : Control
        {
            private IReadOnlyList<int> _checkedIds = new int[0];

            public IReadOnlyList<int> CheckedIds
            {
                get => _checkedIds;
                set { _checkedIds = value; CheckedIdsChanged?.Invoke(this, EventArgs.Empty); }
            }

            public event EventHandler CheckedIdsChanged;
        }

        private static Form Host(params Control[] controls)
        {
            var form = new Form
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-32000, -32000),
            };
            form.Controls.AddRange(controls);
            form.Show();
            return form;
        }

        [Fact]
        public void ShortOverload_EnablesFormatting()
        {
            using (var textBox = new TextBox())
            {
                var binding = textBox.BindProperty(new Input(), m => m.Name, nameof(TextBox.Text));

                Assert.True(binding.FormattingEnabled);
                Assert.Equal(DataSourceUpdateMode.OnPropertyChanged, binding.DataSourceUpdateMode);
            }
        }

        [Fact]
        public void ExplicitFalse_KeepsTheLegacyMode()
        {
            using (var textBox = new TextBox())
            {
                var binding = textBox.BindProperty(new Input(), m => m.Name, nameof(TextBox.Text),
                    DataSourceUpdateMode.OnPropertyChanged, formattingEnabled: false);

                Assert.False(binding.FormattingEnabled);
            }
        }

        [Fact]
        public void InterfaceTypedValue_FlowsBothWays()
        {
            var picker = new TagPicker();
            using (Host(picker))
            {
                var input = new Input { Ids = new List<int> { 1, 2 } };   // a List<int> in an IReadOnlyList<int> property

                picker.BindProperty(input, m => m.Ids, nameof(TagPicker.CheckedIds));
                Assert.Equal(new[] { 1, 2 }, picker.CheckedIds);

                input.Ids = new List<int> { 3 };
                Assert.Equal(new[] { 3 }, picker.CheckedIds);

                picker.CheckedIds = new List<int> { 4, 5 };
                Assert.Equal(new[] { 4, 5 }, input.Ids);
            }
        }

        [Fact]
        public void TypedBind_BadInput_RaisesBindingComplete_AndKeepsTheModelValue()
        {
            var textBox = new TextBox();
            using (Host(textBox))
            {
                var input = new Input { Price = 100 };
                textBox.Bind(input, m => m.Price);
                var binding = textBox.DataBindings[nameof(TextBox.Text)];
                Assert.True(binding.FormattingEnabled);
                BindingCompleteState? state = null;
                binding.BindingComplete += (s, e) =>
                {
                    if (e.BindingCompleteContext == BindingCompleteContext.DataSourceUpdate)
                        state = e.BindingCompleteState;
                };

                textBox.Text = "abc";

                Assert.NotNull(state);
                Assert.NotEqual(BindingCompleteState.Success, state.Value);
                Assert.Equal(100, input.Price);

                textBox.Text = "250";   // recovers on the next valid input
                Assert.Equal(250, input.Price);
            }
        }

        [Fact]
        public void NullString_ShowsEmpty()
        {
            var textBox = new TextBox { Text = "old" };
            using (Host(textBox))
            {
                textBox.Bind(new Input { Name = null }, m => m.Name);

                Assert.Equal("", textBox.Text);
            }
        }

        [Fact]
        public void TypedHelpers_StillBindTheirProperties()
        {
            var check = new CheckBox();
            var numeric = new NumericUpDown { Maximum = 1000 };
            using (Host(check, numeric))
            {
                var model = new Flags { On = true, Amount = 12.5m };
                check.Bind(model, m => m.On);
                numeric.Bind(model, m => m.Amount);

                Assert.True(check.Checked);
                Assert.Equal(12.5m, numeric.Value);

                check.Checked = false;
                numeric.Value = 40m;
                Assert.False(model.On);
                Assert.Equal(40m, model.Amount);
            }
        }

        private sealed class Flags : INotifyPropertyChanged
        {
            private bool _on;
            private decimal _amount;
            public event PropertyChangedEventHandler PropertyChanged;
            public bool On { get => _on; set { _on = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(On))); } }
            public decimal Amount { get => _amount; set { _amount = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Amount))); } }
        }
    }
}
