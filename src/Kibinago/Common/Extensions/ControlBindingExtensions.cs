using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Kibinago.Common.Extensions
{
    public static class ControlBindingExtensions
    {
        /// <summary>
        /// Two-way binding of <paramref name="controlPropertyName"/> to a model property, written back
        /// on every change (<see cref="DataSourceUpdateMode.OnPropertyChanged"/>), with formatting
        /// enabled: values convert through the formatting pipeline (an interface-typed property such
        /// as <c>IReadOnlyList&lt;int&gt;</c> accepts a <c>List&lt;int&gt;</c>), and input that cannot
        /// be converted leaves the model at its last valid value and is reported through the returned
        /// binding's <c>BindingComplete</c>. A previous binding of the same control property is replaced.
        /// </summary>
        /// <remarks>
        /// The typed <c>Bind</c> helpers use this overload. For the old WinForms conversion (exact type
        /// or subclass only), pass <c>formattingEnabled: false</c> to the other overload.
        /// </remarks>
        public static Binding BindProperty<TControl, TViewModel, TValue>(
            this TControl control,
            TViewModel viewModel,
            Expression<Func<TViewModel, TValue>> propertyExpression,
            string controlPropertyName)
            where TControl : Control
            where TViewModel : INotifyPropertyChanged
            => control.BindProperty(viewModel, propertyExpression, controlPropertyName,
                DataSourceUpdateMode.OnPropertyChanged, formattingEnabled: true);

        /// <summary>
        /// Binds <paramref name="controlPropertyName"/> to a model property with an explicit update
        /// mode and returns the WinForms <see cref="Binding"/>, so <c>FormatString</c>,
        /// <c>Format</c> / <c>Parse</c>, <c>NullValue</c> and <c>BindingComplete</c> (conversion
        /// failures; raised only when <paramref name="formattingEnabled"/> is true) are configured
        /// directly. A previous binding of the same control property is replaced.
        /// </summary>
        /// <remarks>
        /// The expression is one property of the model: <c>m =&gt; m.Name</c>. Paths
        /// (<c>m =&gt; m.Address.City</c>) are rejected: WinForms keeps a path bound to the old child
        /// after <c>Address</c> is replaced. Bind the child itself
        /// (<c>txtCity.Bind(model.Address, a =&gt; a.City)</c>, bound again when it is replaced) or
        /// expose the value on the model. Method calls, computed expressions and captured variables
        /// are rejected too — add a property to the model, or use <c>Format</c> / <c>Parse</c> on the
        /// returned binding.
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

            var dataMember = GetPropertyName(propertyExpression);

            // A native Value / Checked binding and the nullable-date binding would both drive the picker.
            if (control is DateTimePicker picker &&
                (controlPropertyName == nameof(DateTimePicker.Value) || controlPropertyName == nameof(DateTimePicker.Checked)) &&
                NullableDateBindings.TryGetValue(picker, out var nullableDate))
                nullableDate.Dispose();

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

        // "Name" for m => m.Name (a Convert around the body is allowed). Anything else is rejected
        // rather than silently reduced to its last member name: a path because WinForms does not
        // follow a replaced child (the binding would quietly keep editing the old object), and
        // calls / computations / captured variables because they are not bindable members.
        private static string GetPropertyName(LambdaExpression expression)
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

            if (names.Count > 1)
            {
                names.Reverse();
                var path = string.Join(".", names);
                var parent = string.Join(".", names.Take(names.Count - 1));
                throw new ArgumentException(
                    $"'{expression}' binds the path '{path}'. WinForms keeps such a binding on the old object when " +
                    $"'{parent}' is replaced. Bind the child itself, e.g. control.Bind(model.{parent}, x => x.{names[names.Count - 1]}) " +
                    "(bind again when it is replaced), or expose the value as a property of the model.",
                    nameof(expression));
            }

            return names[0];
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
        /// Two-way binds a nullable date to a <see cref="DateTimePicker"/> whose checkbox means
        /// "has a date": <c>null</c> unchecks it (the displayed date is kept), a date sets
        /// <see cref="DateTimePicker.Value"/> and checks it, and the user's ticks and date edits are
        /// written back (<c>Checked ? Value : null</c>).
        /// </summary>
        /// <remarks>
        /// Requires <see cref="DateTimePicker.ShowCheckBox"/> = true and a writable
        /// <c>DateTime?</c> property. A date outside <c>MinDate</c>..<c>MaxDate</c> throws
        /// <see cref="ArgumentOutOfRangeException"/> (from <c>Bind</c>, or from the model setter
        /// that raised the change) and leaves the picker unchanged — it is never clamped.
        /// Change the value through the model: a programmatic <c>picker.Checked = ...</c> is not a
        /// user edit and may not reach the model. Binding the picker again (this overload, or a
        /// <c>Value</c> / <c>Checked</c> binding) replaces this binding; it ends by itself when the
        /// picker is disposed. Dispose the returned handle only to unbind earlier. Change the model
        /// on the UI thread.
        /// </remarks>
        public static IDisposable Bind<TViewModel>(this DateTimePicker dateTimePicker, TViewModel viewModel, Expression<Func<TViewModel, DateTime?>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            if (dateTimePicker == null) throw new ArgumentNullException(nameof(dateTimePicker));
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            if (propertyExpression == null) throw new ArgumentNullException(nameof(propertyExpression));
            if (!dateTimePicker.ShowCheckBox)
                throw new InvalidOperationException(
                    "Binding a nullable date needs DateTimePicker.ShowCheckBox = true: the checkbox is what represents 'no date'.");

            var propertyName = GetPropertyName(propertyExpression);
            var propInfo = viewModel.GetType().GetProperty(propertyName);
            if (propInfo == null || propInfo.PropertyType != typeof(DateTime?) || !propInfo.CanRead || propInfo.GetSetMethod() == null)
                throw new ArgumentException($"'{propertyName}' must be a public readable and writable DateTime? property.", nameof(propertyExpression));

            return new NullableDateBinding(dateTimePicker, viewModel, propInfo);
        }

        // Each picker's current nullable-date binding, so binding it again replaces the old one.
        // Weak keys: the table never keeps a picker alive.
        private static readonly ConditionalWeakTable<DateTimePicker, IDisposable> NullableDateBindings =
            new ConditionalWeakTable<DateTimePicker, IDisposable>();

        private sealed class NullableDateBinding : IDisposable
        {
            private readonly DateTimePicker _picker;
            private readonly INotifyPropertyChanged _model;
            private readonly System.Reflection.PropertyInfo _property;
            private bool _syncing;
            private bool _disposed;

            public NullableDateBinding(DateTimePicker picker, INotifyPropertyChanged model, System.Reflection.PropertyInfo property)
            {
                _picker = picker;
                _model = model;
                _property = property;

                var initial = ReadModel();   // reject an out-of-range date before anything changes

                if (NullableDateBindings.TryGetValue(picker, out var previous))
                    previous.Dispose();
                for (int i = picker.DataBindings.Count - 1; i >= 0; i--)
                {
                    var name = picker.DataBindings[i].PropertyName;
                    if (name == nameof(DateTimePicker.Value) || name == nameof(DateTimePicker.Checked))
                        picker.DataBindings.RemoveAt(i);
                }
                NullableDateBindings.Add(picker, this);

                Apply(initial);   // before subscribing: the initial writes are not user input

                _picker.ValueChanged += OnPickerValueChanged;
                _picker.Disposed += OnPickerDisposed;
                _model.PropertyChanged += OnModelPropertyChanged;
            }

            private DateTime? ReadModel()
            {
                var value = (DateTime?)_property.GetValue(_model, null);
                if (value.HasValue && (value.Value < _picker.MinDate || value.Value > _picker.MaxDate))
                    throw new ArgumentOutOfRangeException(_property.Name, value.Value,
                        $"{_property.Name} is outside the picker's range {_picker.MinDate:d} - {_picker.MaxDate:d}.");
                return value;
            }

            private void Apply(DateTime? value)
            {
                _syncing = true;
                try
                {
                    if (value.HasValue)
                    {
                        _picker.Value = value.Value;
                        _picker.Checked = true;
                    }
                    else
                    {
                        _picker.Checked = false;   // keeps the displayed date for when the user ticks it again
                    }
                }
                finally
                {
                    _syncing = false;
                }
            }

            private void OnPickerValueChanged(object sender, EventArgs e)
            {
                if (_syncing) return;
                DateTime? value = _picker.Checked ? _picker.Value : (DateTime?)null;
                if (!Equals(_property.GetValue(_model, null), value))
                    _property.SetValue(_model, value, null);
            }

            private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                // null / empty name = "all properties changed"
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == _property.Name)
                    Apply(ReadModel());
            }

            private void OnPickerDisposed(object sender, EventArgs e) => Dispose();

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                _model.PropertyChanged -= OnModelPropertyChanged;
                _picker.ValueChanged -= OnPickerValueChanged;
                _picker.Disposed -= OnPickerDisposed;
                if (NullableDateBindings.TryGetValue(_picker, out var current) && ReferenceEquals(current, this))
                    NullableDateBindings.Remove(_picker);
            }
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
        /// Two-way binds the text of an editable combo box (<see cref="ComboBoxStyle.DropDown"/> /
        /// <see cref="ComboBoxStyle.Simple"/>): typed text that is not in the list is kept, and it is
        /// written back on every change. For a pick-from-list combo
        /// (<see cref="ComboBoxStyle.DropDownList"/>) bind the selection instead —
        /// <see cref="Bind{TViewModel, TValue}(ComboBox, TViewModel, Expression{Func{TViewModel, TValue}})"/>
        /// binds <c>SelectedValue</c>.
        /// </summary>
        public static Binding BindText<TViewModel>(this ComboBox comboBox, TViewModel viewModel, Expression<Func<TViewModel, string>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            return comboBox.BindProperty(viewModel, propertyExpression, nameof(comboBox.Text), DataSourceUpdateMode.OnPropertyChanged);
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

            var propertyName = GetPropertyName(propertyExpression);
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
