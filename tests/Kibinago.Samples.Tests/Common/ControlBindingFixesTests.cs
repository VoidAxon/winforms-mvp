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
    /// Expression checks, the configurable BindProperty overload, one-way Label binding and the
    /// self-releasing BindRadioGroup.
    /// </summary>
    public class ControlBindingFixesTests
    {
        private abstract class Notifier : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public int HandlerCount => PropertyChanged?.GetInvocationList().Length ?? 0;
            protected void Set<T>(ref T field, T value, string name)
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        private sealed class Address : Notifier
        {
            private string _city;
            public string City { get => _city; set => Set(ref _city, value, nameof(City)); }
        }

        private enum Priority { Low, Normal, High }

        private sealed class Order : Notifier
        {
            private string _name;
            private int _price;
            private Address _address = new Address();
            private Priority _priority;
            public string Name { get => _name; set => Set(ref _name, value, nameof(Name)); }
            public int Price { get => _price; set => Set(ref _price, value, nameof(Price)); }
            public Address Address { get => _address; set => Set(ref _address, value, nameof(Address)); }
            public Priority Priority { get => _priority; set => Set(ref _priority, value, nameof(Priority)); }
            public string Upper() => Name?.ToUpperInvariant();
        }

        // Bindings become active only on created controls with a BindingContext, so the host is
        // shown (as other WinForms tests here do), off-screen.
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

        // ---- expressions ----------------------------------------------------------------------

        [Fact]
        public void Path_IsRejected_WithAHintToBindTheChild()
        {
            using (var textBox = new TextBox())
            {
                var ex = Assert.Throws<ArgumentException>(() => textBox.Bind(new Order(), m => m.Address.City));

                Assert.Contains("x => x.City", ex.Message);
                Assert.Empty(textBox.DataBindings);
            }
        }

        [Fact]
        public void BindingTheChildDirectly_Works()
        {
            var textBox = new TextBox();
            using (Host(textBox))
            {
                var order = new Order();
                order.Address.City = "Tokyo";
                textBox.Bind(order.Address, a => a.City);
                Assert.Equal("Tokyo", textBox.Text);

                textBox.Text = "Osaka";
                Assert.Equal("Osaka", order.Address.City);
            }
        }

        [Fact]
        public void NonMemberExpressions_AreRejected()
        {
            using (var textBox = new TextBox())
            {
                var order = new Order();
                var captured = new Order();

                Assert.Throws<ArgumentException>(() => textBox.Bind(order, m => m.Upper()));
                Assert.Throws<ArgumentException>(() => textBox.Bind(order, m => captured.Name));
                Assert.Throws<ArgumentException>(() => textBox.Bind(order, m => m.Name + "!"));
            }
        }

        [Fact]
        public void NullArguments_Throw()
        {
            using (var textBox = new TextBox())
            {
                Assert.Throws<ArgumentNullException>(() => textBox.Bind<Order>(null, m => m.Name));
                Assert.Throws<ArgumentNullException>(() => ((TextBox)null).Bind(new Order(), m => m.Name));
                Assert.Throws<ArgumentNullException>(() =>
                    new KeyValuePair<RadioButton, Priority>[0].BindRadioGroup<Order, Priority>(null, m => m.Priority));
            }
        }

        // ---- configurable overload -------------------------------------------------------------

        [Fact]
        public void Overload_ReturnsTheNativeBinding_WithTheRequestedSettings()
        {
            using (var textBox = new TextBox())
            {
                var binding = textBox.BindProperty(new Order(), m => m.Price, nameof(TextBox.Text),
                    DataSourceUpdateMode.OnValidation);

                Assert.Same(binding, textBox.DataBindings[nameof(TextBox.Text)]);
                Assert.Equal(DataSourceUpdateMode.OnValidation, binding.DataSourceUpdateMode);
                Assert.True(binding.FormattingEnabled);
            }
        }

        [Fact]
        public void Overload_ConversionFailure_RaisesBindingComplete_AndKeepsTheModelValue()
        {
            var textBox = new TextBox();
            using (Host(textBox))
            {
                var order = new Order { Price = 100 };
                var binding = textBox.BindProperty(order, m => m.Price, nameof(TextBox.Text),
                    DataSourceUpdateMode.OnPropertyChanged);
                BindingCompleteState? state = null;
                binding.BindingComplete += (s, e) =>
                {
                    if (e.BindingCompleteContext == BindingCompleteContext.DataSourceUpdate)
                        state = e.BindingCompleteState;
                };

                textBox.Text = "abc";

                Assert.NotNull(state);
                Assert.NotEqual(BindingCompleteState.Success, state.Value);
                Assert.Equal(100, order.Price);
            }
        }

        // ---- Label -----------------------------------------------------------------------------

        [Fact]
        public void Label_IsOneWay()
        {
            var label = new Label();
            using (Host(label))
            {
                var order = new Order { Name = "a" };
                var binding = label.Bind(order, m => m.Name);
                Assert.Equal(DataSourceUpdateMode.Never, binding.DataSourceUpdateMode);
                Assert.Equal("a", label.Text);

                order.Name = "b";
                Assert.Equal("b", label.Text);

                label.Text = "typed in code";
                Assert.Equal("b", order.Name);
            }
        }

        // ---- radio group -----------------------------------------------------------------------

        private static KeyValuePair<RadioButton, Priority>[] Radios(out RadioButton low, out RadioButton normal, out RadioButton high)
        {
            low = new RadioButton();
            normal = new RadioButton();
            high = new RadioButton();
            return new[]
            {
                new KeyValuePair<RadioButton, Priority>(low, Priority.Low),
                new KeyValuePair<RadioButton, Priority>(normal, Priority.Normal),
                new KeyValuePair<RadioButton, Priority>(high, Priority.High),
            };
        }

        [Fact]
        public void RadioGroup_SyncsBothWays()
        {
            var pairs = Radios(out var low, out var normal, out var high);
            using (Host(low, normal, high))
            {
                var order = new Order { Priority = Priority.Normal };
                pairs.BindRadioGroup(order, m => m.Priority);
                Assert.True(normal.Checked);

                high.Checked = true;
                Assert.Equal(Priority.High, order.Priority);

                order.Priority = Priority.Low;
                Assert.True(low.Checked);
                Assert.False(high.Checked);
            }
        }

        [Fact]
        public void RadioGroup_ReleasesTheModel_WhenTheFormIsDisposed()
        {
            var pairs = Radios(out var low, out var normal, out var high);
            var order = new Order();
            var form = Host(low, normal, high);
            pairs.BindRadioGroup(order, m => m.Priority);
            Assert.Equal(1, order.HandlerCount);

            form.Dispose();

            Assert.Equal(0, order.HandlerCount);
            order.Priority = Priority.High;   // no longer touches the disposed buttons
        }

        [Fact]
        public void RadioGroup_BindingAgain_ReplacesThePreviousBinding()
        {
            var pairs = Radios(out var low, out var normal, out var high);
            using (Host(low, normal, high))
            {
                var order = new Order();
                pairs.BindRadioGroup(order, m => m.Priority);
                pairs.BindRadioGroup(order, m => m.Priority);

                Assert.Equal(1, order.HandlerCount);
            }
        }

        [Fact]
        public void RadioGroup_DisposeHandle_Unbinds()
        {
            var pairs = Radios(out var low, out var normal, out var high);
            using (Host(low, normal, high))
            {
                var order = new Order();
                var handle = pairs.BindRadioGroup(order, m => m.Priority);

                handle.Dispose();
                high.Checked = true;

                Assert.Equal(0, order.HandlerCount);
                Assert.Equal(Priority.Low, order.Priority);
            }
        }

        [Fact]
        public void RadioGroup_PathIsRejected()
        {
            var pairs = new KeyValuePair<RadioButton, string>[0];
            Assert.Throws<ArgumentException>(() => pairs.BindRadioGroup(new Order(), m => m.Address.City));
        }
    }
}
