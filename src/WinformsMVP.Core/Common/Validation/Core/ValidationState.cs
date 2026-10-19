using System;
using System.Collections.Generic;
using System.Linq;

namespace WinformsMVP.Common.Validation.Core
{
    /// <summary>
    /// The current validation errors of a presenter, by field name. Errors come from the model's
    /// DataAnnotations (<see cref="Validate{T}"/>) and from presenter rules
    /// (<see cref="SetError"/>, e.g. cross-field or database checks). Every change is pushed to
    /// the view's <see cref="IValidationErrorDisplay"/>.
    /// </summary>
    /// <remarks>
    /// Typical flow: re-run <see cref="Validate{T}"/> whenever the user edits (errors appear next
    /// to fields the user has visited), and call <see cref="Check{T}"/> when the user submits (all
    /// errors appear). Keep the submit action enabled while there are errors — a disabled Save
    /// button cannot reveal the errors of fields the user never visited.
    /// </remarks>
    public sealed class ValidationState
    {
        /// <summary>Field name used for errors that do not belong to a single field.</summary>
        public const string FormLevel = "";

        private static readonly IReadOnlyList<string> NoErrors = new string[0];

        private Dictionary<string, List<string>> _modelErrors = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, List<string>> _customErrors = new Dictionary<string, List<string>>();
        private bool _revealAll;

        /// <summary>Raised after the errors or <see cref="RevealAll"/> changed.</summary>
        public event EventHandler ErrorsChanged;

        /// <summary>Raised by <see cref="Reset"/>, before <see cref="ErrorsChanged"/>, so displays forget visited fields.</summary>
        public event EventHandler WasReset;

        /// <summary>True when any rule fails, whether or not the error is displayed yet.</summary>
        public bool HasErrors => _modelErrors.Count > 0 || _customErrors.Count > 0;

        /// <summary>True after <see cref="Check{T}"/> / <see cref="ShowAll"/> until <see cref="Reset"/>.</summary>
        public bool RevealAll => _revealAll;

        /// <summary>All current errors, by field name (model rules first, then presenter rules).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors
        {
            get
            {
                var all = new Dictionary<string, IReadOnlyList<string>>();
                foreach (var name in _modelErrors.Keys.Concat(_customErrors.Keys).Distinct())
                    all[name] = GetErrors(name);
                return all;
            }
        }

        /// <summary>The errors of one field (empty when it is valid).</summary>
        public IReadOnlyList<string> GetErrors(string field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            _modelErrors.TryGetValue(field, out var model);
            _customErrors.TryGetValue(field, out var custom);
            if (model == null && custom == null) return NoErrors;
            return (model ?? Enumerable.Empty<string>()).Concat(custom ?? Enumerable.Empty<string>()).ToList();
        }

        /// <summary>
        /// Re-runs the model's DataAnnotations rules (including class-level attributes and
        /// <c>IValidatableObject</c>) and replaces the errors they produced. Errors set with
        /// <see cref="SetError"/> are kept.
        /// </summary>
        public void Validate<T>(T model) where T : class
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var errors = new Dictionary<string, List<string>>();
            foreach (var result in ModelValidator.For<T>().ValidateAll(model))
            {
                var members = result.MemberNames?.Where(m => !string.IsNullOrEmpty(m)).ToList();
                if (members == null || members.Count == 0) members = new List<string> { FormLevel };
                foreach (var member in members)
                    Add(errors, member, result.ErrorMessage);
            }

            if (SameErrors(_modelErrors, errors)) return;
            _modelErrors = errors;
            OnChanged();
        }

        /// <summary>
        /// Validates <paramref name="model"/>, reveals every error (a submit attempt) and returns
        /// true when there are none.
        /// </summary>
        public bool Check<T>(T model) where T : class
        {
            Validate(model);
            ShowAll();
            return !HasErrors;
        }

        /// <summary>Sets the presenter-rule error of <paramref name="field"/> (replacing a previous one).</summary>
        public void SetError(string field, string message)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (string.IsNullOrEmpty(message)) { ClearError(field); return; }
            if (_customErrors.TryGetValue(field, out var existing) && existing.Count == 1 && existing[0] == message)
                return;
            _customErrors[field] = new List<string> { message };
            OnChanged();
        }

        /// <summary>Clears the presenter-rule error of <paramref name="field"/>.</summary>
        public void ClearError(string field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (_customErrors.Remove(field)) OnChanged();
        }

        /// <summary>Displays every error, including fields the user has not visited.</summary>
        public void ShowAll()
        {
            if (_revealAll) return;
            _revealAll = true;
            OnChanged();
        }

        /// <summary>Clears all errors and goes back to showing only visited fields (e.g. after Reset / New).</summary>
        public void Reset()
        {
            // Always signalled, even when already clean: the display still has to forget visits.
            _modelErrors = new Dictionary<string, List<string>>();
            _customErrors.Clear();
            _revealAll = false;
            WasReset?.Invoke(this, EventArgs.Empty);
            OnChanged();
        }

        private void OnChanged() => ErrorsChanged?.Invoke(this, EventArgs.Empty);

        private static void Add(Dictionary<string, List<string>> errors, string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = new List<string>();
            list.Add(message);
        }

        private static bool SameErrors(Dictionary<string, List<string>> a, Dictionary<string, List<string>> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var pair in a)
                if (!b.TryGetValue(pair.Key, out var other) || !pair.Value.SequenceEqual(other)) return false;
            return true;
        }
    }
}
