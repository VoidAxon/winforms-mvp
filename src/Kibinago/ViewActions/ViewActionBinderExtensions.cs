using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;

namespace Kibinago.ViewActions
{
    /// <summary>
    /// Extension methods for ViewActionBinder to simplify bulk binding scenarios.
    /// </summary>
    public static class ViewActionBinderExtensions
    {
        /// <summary>
        /// Adds multiple action bindings at once using tuples.
        /// Useful for binding many RadioButtons or other controls concisely.
        /// </summary>
        /// <example>
        /// <code>
        /// _binder.AddRange(
        ///     (ThemeActions.Light, _lightRadio),
        ///     (ThemeActions.Dark, _darkRadio),
        ///     (ThemeActions.Auto, _autoRadio)
        /// );
        /// </code>
        /// </example>
        public static ViewActionBinder AddRange(
            this ViewActionBinder binder,
            params (ViewAction action, Component component)[] bindings)
        {
            if (binder == null)
                throw new ArgumentNullException(nameof(binder));

            foreach (var (action, component) in bindings)
            {
                binder.Add(action, component);
            }

            return binder;
        }

        /// <summary>
        /// Adds multiple action bindings from a dictionary.
        /// </summary>
        /// <example>
        /// <code>
        /// var mapping = new Dictionary&lt;ViewAction, RadioButton&gt;
        /// {
        ///     [ThemeActions.Light] = _lightRadio,
        ///     [ThemeActions.Dark] = _darkRadio,
        ///     [ThemeActions.Auto] = _autoRadio
        /// };
        /// _binder.AddRange(mapping);
        /// </code>
        /// </example>
        public static ViewActionBinder AddRange(
            this ViewActionBinder binder,
            IDictionary<ViewAction, Component> mappings)
        {
            if (binder == null)
                throw new ArgumentNullException(nameof(binder));
            if (mappings == null)
                throw new ArgumentNullException(nameof(mappings));

            foreach (var kvp in mappings)
            {
                binder.Add(kvp.Key, kvp.Value);
            }

            return binder;
        }

    }
}
