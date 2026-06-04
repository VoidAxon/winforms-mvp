using System;
using System.Collections.Generic;
using System.ComponentModel;
using WinformsMVP.Common.Helpers;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Tracks changes to an object of type T and allows accepting or rejecting changes.
    /// T must be a reference type (class).
    /// </summary>
    /// <typeparam name="T">The type to track. Must be a reference type.</typeparam>
    /// <remarks>
    /// <para>
    /// Snapshotting (clone) resolution order: if T implements <see cref="ICloneable"/> its
    /// Clone() is used; otherwise the global <see cref="ChangeTrackerDefaults.Cloner"/> hook is used
    /// (default = built-in reflection deep copy in <see cref="ObjectCloner"/>).
    /// </para>
    /// <para>
    /// Comparison resolution order: an explicit comparer argument wins; otherwise if T has value
    /// equality (IEquatable&lt;T&gt;/IComparable&lt;T&gt;/Equals override) it is used; otherwise the
    /// global <see cref="ChangeTrackerDefaults.Comparer"/> hook is used (default = reflection deep compare).
    /// </para>
    /// <para>
    /// To plug in a third-party deep-copy library, set the hook once at startup, e.g.
    /// <c>ChangeTrackerDefaults.Cloner = o => o.DeepClone();</c>
    /// </para>
    /// <para>
    /// Implementing <see cref="ICloneable"/> with a deep copy is still recommended as the fast path,
    /// but is no longer required.
    /// </para>
    /// <para>
    /// The cloning strategy is resolved from the runtime type of the initial value at construction time;
    /// subsequent updates (UpdateCurrentValue, AcceptChanges with a new value) must be compatible —
    /// i.e., if the initial value implements ICloneable, later values must also be assignable to ICloneable.
    /// For most uses (concrete <c>T</c>), this is automatic.
    /// </para>
    /// </remarks>
    public class ChangeTracker<T> : IChangeTracking, IRevertibleChangeTracking where T : class
    {
        // Cached once per closed generic type by the CLR static initializer; avoids re-computing
        // BuildComparerFunc() on every constructor call when no explicit comparer is supplied.
        private static readonly Func<T, T, bool> DefaultComparer = BuildComparerFunc();

        private T _originalValue;
        private T _currentValue;
        private readonly Func<T, T> _clone;
        private readonly Func<T, T, bool> _comparer;
        private bool? _cachedIsChanged;
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
                lock (_lock)
                {
                    if (!ReferenceEquals(_currentValue, value))
                    {
                        var wasChanged = IsChanged;
                        _currentValue = value;
                        InvalidateIsChanged();

                        // Fire event only if IsChanged state changed
                        if (wasChanged != IsChanged)
                        {
                            OnIsChangedChanged();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a ChangeTracker with the specified initial value.
        /// </summary>
        /// <param name="initialValue">The initial value to start tracking.</param>
        /// <param name="comparer">Custom comparison function for values. If null, the comparer is resolved
        /// from T: value equality (IEquatable&lt;T&gt;/IComparable&lt;T&gt;/Equals override) uses EqualityHelper,
        /// otherwise the global ChangeTrackerDefaults.Comparer deep-compare hook is used.</param>
        /// <exception cref="ArgumentNullException">When initialValue is null.</exception>
        /// <remarks>
        /// The clone and comparer strategies are resolved once at construction time based on
        /// the runtime type of <paramref name="initialValue"/>. See the class-level remarks for
        /// the full resolution order.
        /// </remarks>
        public ChangeTracker(T initialValue, Func<T, T, bool> comparer = null)
        {
            if (initialValue == null)
                throw new ArgumentNullException(nameof(initialValue));

            // Resolve clone and comparer strategies before taking the first snapshots.
            _clone = BuildCloneFunc(initialValue);
            _comparer = comparer ?? DefaultComparer;
            _currentValue = _clone(initialValue);
            _originalValue = _clone(initialValue);
        }

        /// <summary>
        /// Initializes a ChangeTracker with the specified initial value and IEqualityComparer.
        /// </summary>
        /// <param name="initialValue">The initial value to start tracking.</param>
        /// <param name="comparer">The IEqualityComparer to use for value comparison.</param>
        public ChangeTracker(T initialValue, IEqualityComparer<T> comparer)
            : this(initialValue, comparer != null ? (Func<T, T, bool>)comparer.Equals : null)
        {
        }

        // ----------------------------------------------------------------------------------
        // IChangeTracking / IRevertibleChangeTracking interface implementation
        // ----------------------------------------------------------------------------------

        /// <summary>
        /// Gets a value indicating whether the current value differs from the original value.
        /// This property uses caching for performance.
        /// </summary>
        public bool IsChanged
        {
            get
            {
                lock (_lock)
                {
                    if (!_cachedIsChanged.HasValue)
                    {
                        _cachedIsChanged = !_comparer(_originalValue, _currentValue);
                    }
                    return _cachedIsChanged.Value;
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
            lock (_lock)
            {
                var wasChanged = IsChanged;
                _originalValue = _clone(_currentValue);
                InvalidateIsChanged();

                if (wasChanged)
                {
                    OnIsChangedChanged();
                }
            }
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
            lock (_lock)
            {
                var wasChanged = IsChanged;
                // Use _clone to prevent reference identity
                _currentValue = _clone(_originalValue);
                InvalidateIsChanged();

                if (wasChanged)
                {
                    OnIsChangedChanged();
                }
            }
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

            lock (_lock)
            {
                var wasChanged = IsChanged;
                _originalValue = _clone(newValue);
                _currentValue = _clone(newValue);
                InvalidateIsChanged();

                if (wasChanged)
                {
                    OnIsChangedChanged();
                }
            }
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

            CurrentValue = newValue;
        }

        // ----------------------------------------------------------------------------------
        // Private helper methods
        // ----------------------------------------------------------------------------------

        private void InvalidateIsChanged()
        {
            _cachedIsChanged = null;
        }

        private void OnIsChangedChanged()
        {
            IsChangedChanged?.Invoke(this, EventArgs.Empty);
        }

        // Clone resolution: if the sample value implements ICloneable, use Clone(); otherwise fall back to the global hook.
        private static Func<T, T> BuildCloneFunc(T sample)
        {
            if (sample is ICloneable)
                return v => (T)((ICloneable)v).Clone();
            return v => (T)ChangeTrackerDefaults.Cloner(v);
        }

        // Comparer resolution: if T has value equality (IEquatable/IComparable/Equals override), use EqualityHelper;
        // otherwise fall back to the global deep-compare hook.
        private static Func<T, T, bool> BuildComparerFunc()
        {
            if (HasValueEquality(typeof(T)))
                return EqualityHelper.Equals;
            return (a, b) => ChangeTrackerDefaults.Comparer(a, b);
        }

        // Returns true if T has meaningful equality semantics: IEquatable<T>, IComparable<T>,
        // or an Equals(object) override (not inherited from object/ValueType).
        private static bool HasValueEquality(Type t)
        {
            if (typeof(IEquatable<T>).IsAssignableFrom(t)) return true;
            if (typeof(IComparable<T>).IsAssignableFrom(t)) return true;
            var m = t.GetMethod("Equals", new[] { typeof(object) });
            return m != null && m.DeclaringType != typeof(object);
        }
    }
}
