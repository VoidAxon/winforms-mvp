using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows.Forms;

namespace WinformsMVP.Common.Extensions
{
    public static class ControlBindingExtensions
    {
        public static void BindProperty<TControl, TViewModel, TValue>(
            this TControl control,
            TViewModel viewModel,
            Expression<Func<TViewModel, TValue>> propertyExpression,
            string controlPropertyName)
            where TControl : Control
            where TViewModel : INotifyPropertyChanged
        {
            if (control == null || viewModel == null || propertyExpression == null) return;

            var propertyName = PropertyNameHelper.GetName(propertyExpression);

            control.DataBindings.Clear();
            control.DataBindings.Add(controlPropertyName, viewModel, propertyName, false, DataSourceUpdateMode.OnPropertyChanged);
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
        /// Bind Label.Text to a ViewModel property (read-only display)
        /// </summary>
        public static void Bind<TViewModel>(this Label label, TViewModel viewModel, Expression<Func<TViewModel, object>> propertyExpression)
            where TViewModel : INotifyPropertyChanged
        {
            label.BindProperty(viewModel, propertyExpression, nameof(label.Text));
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

        public static void BindRadioGroup<TViewModel, TValue>(this IEnumerable<KeyValuePair<RadioButton, TValue>> radioPairs, TViewModel viewModel, Expression<Func<TViewModel, TValue>> propertyExpression) where TViewModel : INotifyPropertyChanged
        {
            if (radioPairs == null || viewModel == null || propertyExpression == null)
                return;

            var propertyName = PropertyNameHelper.GetName(propertyExpression);
            var propInfo = viewModel.GetType().GetProperty(propertyName);
            if (propInfo == null) throw new ArgumentException($"Property {propertyName} not found.");

            foreach (var kv in radioPairs)
            {
                var radio = kv.Key;
                var value = kv.Value;

                radio.Checked = Equals(propInfo.GetValue(viewModel, null), value);

                radio.CheckedChanged -= RadioButton_CheckedChanged;
                radio.CheckedChanged += RadioButton_CheckedChanged;

                void RadioButton_CheckedChanged(object sender, EventArgs e)
                {
                    if (radio.Checked)
                    {
                        var current = propInfo.GetValue(viewModel, null);
                        if (!Equals(current, value))
                            propInfo.SetValue(viewModel, value, null);
                    }
                }
            }

            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyName)
                {
                    var currentValue = propInfo.GetValue(viewModel, null);
                    foreach (var kv in radioPairs)
                    {
                        kv.Key.Checked = Equals(currentValue, kv.Value);
                    }
                }
            };
        }
    }
}
