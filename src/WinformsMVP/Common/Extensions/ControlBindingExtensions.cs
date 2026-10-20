using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace WinformsMVP.Common.Extensions
{
    public static class ControlBindingExtensions
    {
        /// <summary>
        /// Two-way binding of <paramref name="controlPropertyName"/> to a model property, written back
        /// on every change (<see cref="DataSourceUpdateMode.OnPropertyChanged"/>), no formatting.
        /// A previous binding of the same control property is replaced.
        /// </summary>
        public static Binding BindProperty<TControl, TViewModel, TValue>(
            this TControl control,
            TViewModel viewModel,
            Expression<Func<TViewModel, TValue>> propertyExpression,
            string controlPropertyName)
            where TControl : Control
            where TViewModel : INotifyPropertyChanged
            => control.BindProperty(viewModel, propertyExpression, controlPropertyName,
                DataSourceUpdateMode.OnPropertyChanged, formattingEnabled: false);

        /// <summary>
        /// Binds <paramref name="controlPropertyName"/> to a model property with an explicit update
        /// mode and returns the WinForms <see cref="Binding"/>, so <c>FormatString</c>,
        /// <c>Format</c> / <c>Parse</c>, <c>NullValue</c> and <c>BindingComplete</c> (conversion
        /// failures; raised only when <paramref name="formattingEnabled"/> is true) are configured
        /// directly. A previous binding of the same control property is replaced.
        /// </summary>
        /// <remarks>
        /// The expression is a member path from the model: <c>m =&gt; m.Name</c> or
        /// <c>m =&gt; m.Address.City</c> (bound as the WinForms navigation path <c>"Address.City"</c>;
        /// every object along the path should raise <c>PropertyChanged</c>). Method calls, computed
        /// expressions and captured variables are rejected — add a property to the model, or use
        /// <c>Format</c> / <c>Parse</c> on the returned binding.
        /// </remarks>
        public static Binding BindProperty<TControl, TViewModel, TValue>(
            this TControl control,
            TViewModel viewModel,
            Expression<Func<TViewModel, TValue>> propertyExpression,
            string controlPropertyName,
            DataSourceUpdateMode updateMode,
            bool formattingEnabled = true)
            where TControl : Control
            where TViewModel : INotifyPropertyChanged
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            if (propertyExpression == null) throw new ArgumentNullException(nameof(propertyExpression));
            if (string.IsNullOrEmpty(controlPropertyName)) throw new ArgumentNullException(nameof(controlPropertyName));

            var dataMember = GetMemberPath(propertyExpression);

            // Remove only a previous binding for the SAME control property, so that multiple
            // properties on one control (e.g. Text + ReadOnly, or a button's Text + Enabled) can
            // coexist. Clearing the whole DataBindings collection would wipe unrelated bindings.
            for (int i = control.DataBindings.Count - 1; i >= 0; i--)
            {
                if (string.Equals(control.DataBindings[i].PropertyName, controlPropertyName, StringComparison.Ordinal))
                    control.DataBindings.RemoveAt(i);
            }

            return control.DataBindings.Add(controlPropertyName, viewModel, dataMember, formattingEnabled, updateMode);
        }

        // "Name" for m => m.Name, "Address.City" for m => m.Address.City (a boxing Convert around
        // the body is allowed). Anything that is not a plain member chain from the parameter is
        // rejected rather than silently reduced to its last member name.
        private static string GetMemberPath(LambdaExpression expression)
        {
            var body = expression.Body;
            if (body is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
                body = unary.Operand;

            var names = new List<string>();
            while (body is MemberExpression member)
            {
                names.Add(member.Member.Name);
                body = member.Expression;
            }

            if (names.Count == 0 || !(body is ParameterExpression))
                throw new ArgumentException(
                    $"'{expression}' is not a property path of the model. Use m => m.Property (or m => m.Child.Property); " +
                    "for a computed value add a property to the model or use Format/Parse on the returned Binding.",
                    nameof(expression));

            names.Reverse();
            return string.Join(".", names);
        }

        public static void Bind<TViewModel>(this TextBox textBox, TViewModel viewModel, Expression<Func<TViewModel, object>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            textBox.BindProperty(viewModel, propertyExpression, nameof(textBox.Text));
        }

        public static void Bind<TViewModel>(this CheckBox checkBox, TViewModel viewModel, Expression<Func<TViewModel, bool>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            checkBox.BindProperty(viewModel, propertyExpression, nameof(checkBox.Checked));
        }

        public static void Bind<TViewModel>(this NumericUpDown numericUpDown, TViewModel viewModel, Expression<Func<TViewModel, decimal>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            numericUpDown.BindProperty(viewModel, propertyExpression, nameof(numericUpDown.Value));
        }

        public static void Bind<TViewModel, TValue>(this ComboBox comboBox, TViewModel viewModel, Expression<Func<TViewModel, TValue>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            comboBox.BindProperty(viewModel, propertyExpression, nameof(comboBox.SelectedValue));
        }

        /// <summary>
        /// Bind Label.Text to a ViewModel property, one way (model -&gt; label). Formatting is
        /// enabled, so numbers and dates can be shaped with the returned binding's FormatString.
        /// </summary>
        public static Binding Bind<TViewModel>(this Label label, TViewModel viewModel, Expression<Func<TViewModel, object>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            return label.BindProperty(viewModel, propertyExpression, nameof(label.Text), DataSourceUpdateMode.Never);
        }

        /// <summary>
        /// Bind DateTimePicker.Value to a ViewModel property
        /// </summary>
        public static void Bind<TViewModel>(this DateTimePicker dateTimePicker, TViewModel viewModel, Expression<Func<TViewModel, DateTime>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            dateTimePicker.BindProperty(viewModel, propertyExpression, nameof(dateTimePicker.Value));
        }

        /// <summary>
        /// Bind TrackBar.Value to a ViewModel property
        /// </summary>
        public static void Bind<TViewModel>(this TrackBar trackBar, TViewModel viewModel, Expression<Func<TViewModel, int>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            trackBar.BindProperty(viewModel, propertyExpression, nameof(trackBar.Value));
        }

        /// <summary>
        /// Bind ProgressBar.Value to a ViewModel property
        /// </summary>
        public static void Bind<TViewModel>(this ProgressBar progressBar, TViewModel viewModel, Expression<Func<TViewModel, int>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            progressBar.BindProperty(viewModel, propertyExpression, nameof(progressBar.Value));
        }

        /// <summary>
        /// Bind ListBox.SelectedItem to a ViewModel property
        /// </summary>
        public static void BindSelectedItem<TViewModel, TValue>(this ListBox listBox, TViewModel viewModel, Expression<Func<TViewModel, TValue>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            listBox.BindProperty(viewModel, propertyExpression, nameof(listBox.SelectedItem));
        }

        /// <summary>
        /// Bind ListBox.SelectedIndex to a ViewModel property
        /// </summary>
        public static void BindSelectedIndex<TViewModel>(this ListBox listBox, TViewModel viewModel, Expression<Func<TViewModel, int>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            listBox.BindProperty(viewModel, propertyExpression, nameof(listBox.SelectedIndex));
        }

        /// <summary>
        /// Bind RichTextBox.Text to a ViewModel property
        /// </summary>
        public static void Bind<TViewModel>(this RichTextBox richTextBox, TViewModel viewModel, Expression<Func<TViewModel, object>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            richTextBox.BindProperty(viewModel, propertyExpression, nameof(richTextBox.Text));
        }

        /// <summary>
        /// Bind MaskedTextBox.Text to a ViewModel property
        /// </summary>
        public static void Bind<TViewModel>(this MaskedTextBox maskedTextBox, TViewModel viewModel, Expression<Func<TViewModel, object>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            maskedTextBox.BindProperty(viewModel, propertyExpression, nameof(maskedTextBox.Text));
        }

        /// <summary>
        /// Bind PictureBox.ImageLocation to a ViewModel property (for image URLs)
        /// </summary>
        public static void BindImageLocation<TViewModel>(this PictureBox pictureBox, TViewModel viewModel, Expression<Func<TViewModel, object>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            pictureBox.BindProperty(viewModel, propertyExpression, nameof(pictureBox.ImageLocation));
        }

        /// <summary>
        /// Bind ComboBox.SelectedIndex to a ViewModel property (for index-based selection)
        /// </summary>
        public static void BindSelectedIndex<TViewModel>(this ComboBox comboBox, TViewModel viewModel, Expression<Func<TViewModel, int>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            comboBox.BindProperty(viewModel, propertyExpression, nameof(comboBox.SelectedIndex));
        }

        /// <summary>
        /// Bind ComboBox.SelectedItem to a ViewModel property (for item-based selection)
        /// </summary>
        public static void BindSelectedItem<TViewModel, TValue>(this ComboBox comboBox, TViewModel viewModel, Expression<Func<TViewModel, TValue>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            comboBox.BindProperty(viewModel, propertyExpression, nameof(comboBox.SelectedItem));
        }

        /// <summary>
        /// Two-way binds a group of radio buttons to one model property: the button whose value
        /// equals the property is checked, and checking a button writes its value back.
        /// </summary>
        /// <remarks>
        /// The binding ends by itself when any of the buttons is disposed (the form closes), so the
        /// model does not keep the form alive. Binding a button again replaces its previous group
        /// binding. Dispose the returned handle only to unbind earlier. Change the model on the UI
        /// thread.
        /// </remarks>
        public static IDisposable BindRadioGroup<TViewModel, TValue>(this IEnumerable<KeyValuePair<RadioButton, TValue>> radioPairs, TViewModel viewModel, Expression<Func<TViewModel, TValue>> propertyExpression) where TViewModel : INotifyPropertyChanged
        {
            if (radioPairs == null) throw new ArgumentNullException(nameof(radioPairs));
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            if (propertyExpression == null) throw new ArgumentNullException(nameof(propertyExpression));

            var propertyName = GetMemberPath(propertyExpression);
            if (propertyName.Contains("."))
                throw new ArgumentException(
                    $"BindRadioGroup binds a property of the model itself; '{propertyName}' is a path.",
                    nameof(propertyExpression));
            var propInfo = viewModel.GetType().GetProperty(propertyName);
            if (propInfo == null) throw new ArgumentException($"Property {propertyName} not found.", nameof(propertyExpression));

            return new RadioGroupBinding<TValue>(radioPairs.ToList(), viewModel, propInfo);
        }

        // Each radio button's current group binding, so binding it again replaces the old one.
        // Weak keys: the table never keeps a button alive.
        private static readonly ConditionalWeakTable<RadioButton, IDisposable> RadioGroupBindings =
            new ConditionalWeakTable<RadioButton, IDisposable>();

        private sealed class RadioGroupBinding<TValue> : IDisposable
        {
            private readonly List<KeyValuePair<RadioButton, TValue>> _pairs;
            private readonly INotifyPropertyChanged _model;
            private readonly System.Reflection.PropertyInfo _property;
            private bool _disposed;

            public RadioGroupBinding(List<KeyValuePair<RadioButton, TValue>> pairs, INotifyPropertyChanged model, System.Reflection.PropertyInfo property)
            {
                _pairs = pairs;
                _model = model;
                _property = property;

                foreach (var radio in _pairs.Select(p => p.Key).Distinct())
                {
                    if (RadioGroupBindings.TryGetValue(radio, out var previous))
                        previous.Dispose();
                    RadioGroupBindings.Add(radio, this);
                }

                SyncFromModel();   // before subscribing: the initial Checked writes are not user input

                foreach (var pair in _pairs)
                {
                    pair.Key.CheckedChanged += OnCheckedChanged;
                    pair.Key.Disposed += OnRadioDisposed;
                }
                _model.PropertyChanged += OnModelPropertyChanged;
            }

            private void OnCheckedChanged(object sender, EventArgs e)
            {
                var radio = (RadioButton)sender;
                if (!radio.Checked) return;
                foreach (var pair in _pairs)
                {
                    if (pair.Key != radio) continue;
                    if (!Equals(_property.GetValue(_model, null), pair.Value))
                        _property.SetValue(_model, pair.Value, null);
                    return;
                }
            }

            private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                // null / empty name = "all properties changed"
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == _property.Name)
                    SyncFromModel();
            }

            private void SyncFromModel()
            {
                var current = _property.GetValue(_model, null);
                foreach (var pair in _pairs)
                    pair.Key.Checked = Equals(current, pair.Value);
            }

            private void OnRadioDisposed(object sender, EventArgs e) => Dispose();

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                _model.PropertyChanged -= OnModelPropertyChanged;
                foreach (var pair in _pairs)
                {
                    pair.Key.CheckedChanged -= OnCheckedChanged;
                    pair.Key.Disposed -= OnRadioDisposed;
                    if (RadioGroupBindings.TryGetValue(pair.Key, out var current) && ReferenceEquals(current, this))
                        RadioGroupBindings.Remove(pair.Key);
                }
            }
        }
    }
}
