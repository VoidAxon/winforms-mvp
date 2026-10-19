using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Tracks changes to an object of type T and allows accepting or rejecting changes.
    /// T must be a reference type (class).
    /// </summary>
    /// <typeparam name="T">The type to track. Must be a reference type.</typeparam>
    /// <remarks>
    /// <para>
    /// Snapshots and comparison come from a <see cref="ChangeTrackingPolicy{T}"/> fixed at
    /// construction: the built-in engines (reflection deep copy and
    /// <see cref="ObjectComparer.ContentEquals(object, object)"/>), a comparer you pass, or an
    /// explicit policy. Nothing global is read afterwards.
    /// </para>
    /// <para>
    /// T's own Equals/IEquatable is deliberately not consulted: entity types commonly define
    /// equality as "same Id", which would report an edited entity as unchanged and skip the save
    /// prompt. Pass a comparer to opt in. <see cref="ICloneable"/> is not consulted either — .NET
    /// allows Clone() to be shallow; a model with a verified deep Clone() opts in with a policy.
    /// </para>
    /// <para>
    /// Inside a presenter, prefer <c>TrackChanges(value)</c>: it uses the application-wide
    /// engines and per-type policies configured at startup (<c>mvp.ChangeTracking</c>). A tracker
    /// created with <c>new</c> only ever uses what its constructor was given.
    /// </para>
    /// </remarks>
    public class ChangeTracker<T> : IChangeTracking, IRevertibleChangeTracking where T : class
    {
        private T _originalValue;
        private T _currentValue;
        private readonly Func<T, T> _clone;
        private readonly Func<T, T, bool> _comparer;
        private readonly object _lock = new object();

        /// <summary>
        /// Occurs when the IsChanged state changes.
        /// </summary>
        public event EventHandler IsChangedChanged;

        /// <summary>
        /// The working snapshot currently held by the tracker.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Exposed mainly for **ad-hoc inspection during development** (e.g. peeking at the
        /// post-change value in the debugger). It is intentionally hidden from IntelliSense
        /// (<see cref="EditorBrowsableAttribute"/>) to discourage using it as your data source.
        /// </para>
        /// <para>
        /// ChangeTracker is a change-<i>detection</i> tool, not the owner of your data. Treat the
        /// View (or your model) as the source of truth: pass the current value into
        /// <see cref="IsChangedWith"/> to test "changed?", and reset the baseline with
        /// <see cref="AcceptChanges(T)"/> after a successful save.
        /// </para>
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public T CurrentValue
        {
            get
            {
                lock (_lock)
                {
                    return _currentValue;
                }
            }
            private set
            {
                Mutate(() => _currentValue = value);
            }
        }

        /// <summary>
        /// Tracks <paramref name="initialValue"/> with the built-in engines: reflection deep copy for
        /// snapshots and content comparison (<see cref="ObjectComparer.ContentEquals(object, object)"/>).
        /// </summary>
        /// <exception cref="ArgumentNullException">When initialValue is null.</exception>
        /// <remarks>
        /// Application-wide engines and per-type policies registered at startup are not consulted
        /// here; a presenter's <c>TrackChanges(value)</c> uses them.
        /// </remarks>
        public ChangeTracker(T initialValue)
            : this(initialValue, ChangeTrackingPolicy<T>.BuiltIn)
        {
        }

        /// <summary>
        /// Tracks <paramref name="initialValue"/> comparing with <paramref name="comparer"/>;
        /// snapshots use the built-in deep copy.
        /// </summary>
        /// <exception cref="ArgumentNullException">When initialValue or comparer is null.</exception>
        public ChangeTracker(T initialValue, Func<T, T, bool> comparer)
            : this(initialValue, new ChangeTrackingPolicy<T>(
                ChangeTrackingPolicy<T>.BuiltIn.Clone,
                comparer ?? throw new ArgumentNullException(nameof(comparer))))
        {
        }

        /// <summary>
        /// Tracks <paramref name="initialValue"/> comparing with <paramref name="comparer"/>;
        /// snapshots use the built-in deep copy.
        /// </summary>
        /// <exception cref="ArgumentNullException">When initialValue or comparer is null.</exception>
        public ChangeTracker(T initialValue, IEqualityComparer<T> comparer)
            : this(initialValue, (comparer ?? throw new ArgumentNullException(nameof(comparer))).Equals)
        {
        }

        /// <summary>
        /// Tracks <paramref name="initialValue"/> with an explicit snapshot/compare
        /// <paramref name="policy"/>. The policy is fixed for the tracker's lifetime.
        /// </summary>
        /// <exception cref="ArgumentNullException">When initialValue or policy is null.</exception>
        public ChangeTracker(T initialValue, ChangeTrackingPolicy<T> policy)
        {
            if (initialValue == null)
                throw new ArgumentNullException(nameof(initialValue));
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            _clone = policy.Clone;
            _comparer = policy.ContentEquals;
            _currentValue = _clone(initialValue);
            _originalValue = _clone(initialValue);
        }

        // ----------------------------------------------------------------------------------
        // IChangeTracking / IRevertibleChangeTracking interface implementation
        // ----------------------------------------------------------------------------------

        /// <summary>
        /// Gets a value indicating whether the current value differs from the original value.
        /// Computed on every read (not cached), so in-place edits made through
        /// <see cref="CurrentValue"/> are reflected immediately.
        /// </summary>
        public bool IsChanged
        {
            get
            {
                // Not cached: CurrentValue is a live reference, so in-place edits made through it
                // (or after UpdateCurrentValue) must be seen on the next read.
                lock (_lock)
                {
                    return !_comparer(_originalValue, _currentValue);
                }
            }
        }

        /// <summary>
        /// Commits the current value as the new baseline.
        /// </summary>
        /// <remarks>
        /// Internally creates a deep copy of the current value to use as the new baseline,
        /// using the clone strategy resolved at construction time.
        /// </remarks>
        public void AcceptChanges()
        {
            Mutate(() => _originalValue = _clone(_currentValue));
        }

        /// <summary>
        /// Reverts the current value to the last accepted baseline.
        /// </summary>
        /// <remarks>
        /// Internally creates a deep copy of the original baseline value and assigns it to CurrentValue,
        /// using the clone strategy resolved at construction time.
        /// This protects the baseline and prevents reference sharing between CurrentValue and the stored original.
        /// </remarks>
        public void RejectChanges()
        {
            // Use _clone to prevent reference identity
            Mutate(() => _currentValue = _clone(_originalValue));
        }

        // ----------------------------------------------------------------------------------
        // Helper methods (for Passive View / Supervising Controller patterns)
        // ----------------------------------------------------------------------------------

        /// <summary>
        /// Checks whether the value from the view differs from the original baseline.
        /// Useful for Passive View / Supervising Controller patterns.
        /// </summary>
        /// <param name="currentValueFromView">The current value retrieved from the view.</param>
        /// <returns>True if the view value differs from the original value, otherwise false.</returns>
        public bool IsChangedWith(T currentValueFromView)
        {
            lock (_lock)
            {
                return !_comparer(_originalValue, currentValueFromView);
            }
        }

        /// <summary>
        /// Commits a new value as the baseline and updates the current value.
        /// </summary>
        /// <param name="newValue">The value to set as the new baseline and current value.</param>
        /// <exception cref="ArgumentNullException">When newValue is null.</exception>
        public void AcceptChanges(T newValue)
        {
            if (newValue == null)
                throw new ArgumentNullException(nameof(newValue));

            Mutate(() =>
            {
                _originalValue = _clone(newValue);
                _currentValue = _clone(newValue);
            });
        }

        /// <summary>
        /// Gets a copy of the original baseline value.
        /// </summary>
        /// <returns>A deep copy of the original value.</returns>
        public T GetOriginalValue()
        {
            lock (_lock)
            {
                return _clone(_originalValue);
            }
        }

        /// <summary>
        /// Updates the currently tracked value.
        /// </summary>
        /// <param name="newValue">The new current value.</param>
        /// <exception cref="ArgumentNullException">When newValue is null.</exception>
        /// <remarks>
        /// This method updates the CurrentValue property and
        /// fires the IsChangedChanged event if the IsChanged state changes.
        /// </remarks>
        public void UpdateCurrentValue(T newValue)
        {
            if (newValue == null)
                throw new ArgumentNullException(nameof(newValue));

            Mutate(() => _currentValue = newValue);
        }

        // ----------------------------------------------------------------------------------
        // Private helper methods
        // ----------------------------------------------------------------------------------

        // Applies a state change under the lock and raises IsChangedChanged AFTER releasing it, so a
        // subscriber that marshals to another thread (Control.Invoke) cannot deadlock with a caller
        // waiting on the tracker.
        //
        // The "before" state is compared against the last state observed by a mutation, not
        // re-computed: an in-place edit made through CurrentValue between two mutations is still
        // reported by the next one.
        private bool _lastReportedIsChanged;

        private void Mutate(Action change)
        {
            bool raise;
            lock (_lock)
            {
                change();
                var isChanged = !_comparer(_originalValue, _currentValue);
                raise = isChanged != _lastReportedIsChanged;
                _lastReportedIsChanged = isChanged;
            }
            if (raise) IsChangedChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
