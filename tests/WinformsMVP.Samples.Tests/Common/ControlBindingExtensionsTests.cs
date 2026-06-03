using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using WinformsMVP.Common.Extensions;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class ControlBindingExtensionsTests
    {
        private class BindingViewModel : INotifyPropertyChanged
        {
            private string _name;
            private bool _isReadOnly;

            public string Name
            {
                get => _name;
                set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
            }

            public bool IsReadOnly
            {
                get => _isReadOnly;
                set { _isReadOnly = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsReadOnly))); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        [Fact]
        public void BindProperty_TwoPropertiesOnSameControl_KeepsBothBindings()
        {
            using (var textBox = new TextBox())
            {
                var vm = new BindingViewModel();

                textBox.Bind(vm, m => m.Name);                                  // binds Text
                textBox.BindProperty(vm, m => m.IsReadOnly, nameof(textBox.ReadOnly));

                Assert.Equal(2, textBox.DataBindings.Count);
                Assert.Contains(textBox.DataBindings.Cast<Binding>(), b => b.PropertyName == nameof(textBox.Text));
                Assert.Contains(textBox.DataBindings.Cast<Binding>(), b => b.PropertyName == nameof(textBox.ReadOnly));
            }
        }

        [Fact]
        public void BindProperty_RebindingSameControlProperty_DoesNotDuplicate()
        {
            using (var textBox = new TextBox())
            {
                var vm = new BindingViewModel();

                textBox.Bind(vm, m => m.Name);
                textBox.Bind(vm, m => m.Name);   // rebinding the same control property

                Assert.Equal(1, textBox.DataBindings.Count);
                Assert.Equal(nameof(textBox.Text), textBox.DataBindings.Cast<Binding>().Single().PropertyName);
            }
        }
    }
}
