using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Common.Validation.Core;

namespace Kibinago.Common.Validation
{
    /// <summary>
    /// Shows validation errors next to WinForms controls. Map each field name (the model property
    /// name, e.g. <c>nameof(Order.Amount)</c>) to the control that edits it. A field's errors are
    /// shown once the user has visited its control (focus left it) or after a submit attempt
    /// (<see cref="ValidationState.Check{T}"/>); <see cref="ValidationState.Reset"/> forgets the
    /// visits. Derive from this class to render errors differently (labels, colors);
    /// <see cref="ErrorProviderDisplay"/> uses an <see cref="ErrorProvider"/>.
    /// </summary>
    public abstract class ControlErrorDisplay : IValidationErrorDisplay
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> None =
            new Dictionary<string, IReadOnlyList<string>>();

        private readonly Dictionary<string, Control> _controls = new Dictionary<string, Control>();
        private readonly HashSet<string> _visited = new HashSet<string>();
        private IReadOnlyDictionary<string, IReadOnlyList<string>> _errors = None;
        private bool _revealAll;

        /// <summary>
        /// Shows the errors of <paramref name="field"/> on <paramref name="control"/>. Map
        /// <see cref="ValidationState.FormLevel"/> to a control (e.g. the Save button) to show errors
        /// that belong to no field, and errors of unmapped fields, after a submit attempt.
        /// </summary>
        public ControlErrorDisplay Map(string field, Control control)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (_controls.ContainsKey(field))
                throw new InvalidOperationException($"Field '{field}' is already mapped to a control.");

            _controls[field] = control;
            if (field != ValidationState.FormLevel)
            {
                control.Leave += (s, e) =>
                {
                    if (_visited.Add(field)) Render();
                };
            }
            return this;
        }

        /// <inheritdoc />
        public void ShowErrors(IReadOnlyDictionary<string, IReadOnlyList<string>> errors, bool revealAll)
        {
            _errors = errors ?? None;
            _revealAll = revealAll;
            Render();
        }

        /// <inheritdoc />
        public void Reset() => _visited.Clear();

        /// <summary>
        /// Moves the focus to the control mapped to <paramref name="field"/>, first selecting the
        /// <see cref="TabPage"/>s that hold it. Call it after the error message was dismissed, e.g.
        /// from a view method <c>FocusError(field)</c> that the presenter calls on a failed submit.
        /// </summary>
        /// <returns>
        /// False, without changing the focus, for <see cref="ValidationState.FormLevel"/>, an
        /// unmapped field or a disabled control; false also when the control cannot take the focus
        /// (e.g. hidden). Other containers (a collapsed panel, a custom tab control) are the form's
        /// to open before calling this.
        /// </returns>
        public bool TryFocusError(string field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (field == ValidationState.FormLevel || !_controls.TryGetValue(field, out var control) || !control.Enabled)
                return false;

            for (var child = control; child.Parent != null; child = child.Parent)
            {
                if (child is TabPage page && child.Parent is TabControl tabs && tabs.SelectedTab != page)
                    tabs.SelectedTab = page;
            }
            return control.CanFocus && control.Focus();
        }

        /// <summary>Shows <paramref name="message"/> on <paramref name="control"/>; an empty message clears it.</summary>
        protected abstract void SetControlError(Control control, string message);

        private void Render()
        {
            var messages = _controls.Values.Distinct().ToDictionary(c => c, c => new List<string>());
            _controls.TryGetValue(ValidationState.FormLevel, out var formLevel);

            foreach (var pair in _errors)
            {
                if (pair.Value == null || pair.Value.Count == 0) continue;

                if (pair.Key != ValidationState.FormLevel && _controls.TryGetValue(pair.Key, out var control))
                {
                    if (_revealAll || _visited.Contains(pair.Key))
                        messages[control].AddRange(pair.Value);
                }
                else if (_revealAll && formLevel != null)
                {
                    messages[formLevel].AddRange(pair.Value);
                }
            }

            foreach (var pair in messages)
                SetControlError(pair.Key, string.Join(Environment.NewLine, pair.Value));
        }
    }

    /// <summary>Shows validation errors with a WinForms <see cref="ErrorProvider"/> (icon + tooltip).</summary>
    public sealed class ErrorProviderDisplay : ControlErrorDisplay
    {
        private readonly ErrorProvider _provider;

        public ErrorProviderDisplay(ErrorProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc cref="ControlErrorDisplay.Map"/>
        /// <remarks>Returns this display's own type, so a field typed <c>ErrorProviderDisplay</c> can hold the chain.</remarks>
        public new ErrorProviderDisplay Map(string field, Control control)
        {
            base.Map(field, control);
            return this;
        }

        protected override void SetControlError(Control control, string message) => _provider.SetError(control, message);
    }
}
