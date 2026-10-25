using System;
using System.ComponentModel;

namespace Kibinago.ViewActions
{
    internal class ActionBinding
    {
        public ViewAction ActionKey { get; }
        public Component[] Components { get; }

        /// <summary>
        /// Optional projection that reads a payload value off the triggering control at dispatch time.
        /// <c>null</c> for value-less bindings (the classic <c>Add(action, controls)</c> path).
        /// </summary>
        public Func<Component, object> ValueSelector { get; }

        public ActionBinding(ViewAction actionkey, Component[] components)
            : this(actionkey, components, null)
        {
        }

        public ActionBinding(ViewAction actionkey, Component[] components, Func<Component, object> valueSelector)
        {
            ActionKey = actionkey;
            Components = components;
            ValueSelector = valueSelector;
        }
    }
}
