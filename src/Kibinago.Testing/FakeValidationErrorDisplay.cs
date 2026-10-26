using System;
using System.Collections.Generic;
using Kibinago.Validation;

namespace Kibinago.Testing
{
    /// <summary>
    /// <see cref="IValidationErrorDisplay"/> fake for a test view that implements <c>IValidatableView</c>:
    /// records the last errors the presenter's <c>Validation</c> pushed. It keeps every error as received;
    /// which ones a real display would draw (visited fields only until <see cref="RevealAll"/>) is the
    /// display's concern, not the presenter's, so tests assert on the errors themselves.
    /// </summary>
    /// <example>
    /// <code>
    /// public FakeValidationErrorDisplay Errors { get; } = new FakeValidationErrorDisplay();
    /// public IValidationErrorDisplay ValidationErrors =&gt; Errors;   // IValidatableView
    ///
    /// view.Binder.Dispatch(Actions.Save);
    /// Assert.True(view.Errors.RevealAll);
    /// Assert.True(view.Errors.HasError(nameof(Customer.Name)));
    /// </code>
    /// </example>
    public class FakeValidationErrorDisplay : IValidationErrorDisplay
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> None =
            new Dictionary<string, IReadOnlyList<string>>();

        /// <summary>The current errors by field (<c>ValidationState.FormLevel</c> for form-level errors); empty until the first push.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; private set; } = None;

        /// <summary>Whether the last push asked to show every error (after a submit attempt) rather than visited fields only.</summary>
        public bool RevealAll { get; private set; }

        /// <summary>How many times errors were pushed.</summary>
        public int ShowCount { get; private set; }

        /// <summary>How many times the display was told to forget visited fields (<c>Validation.Reset()</c>).</summary>
        public int ResetCount { get; private set; }

        /// <summary>The current error messages of <paramref name="field"/>; empty when it has none.</summary>
        /// <param name="field">The field name (the model property name).</param>
        public IReadOnlyList<string> ErrorsFor(string field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            return Errors.TryGetValue(field, out var messages) ? messages : Array.Empty<string>();
        }

        /// <summary>Whether <paramref name="field"/> currently has an error.</summary>
        /// <param name="field">The field name (the model property name).</param>
        public bool HasError(string field) => ErrorsFor(field).Count > 0;

        /// <inheritdoc />
        public void ShowErrors(IReadOnlyDictionary<string, IReadOnlyList<string>> errors, bool revealAll)
        {
            Errors = errors ?? None;
            RevealAll = revealAll;
            ShowCount++;
        }

        /// <inheritdoc />
        public void Reset() => ResetCount++;
    }
}
