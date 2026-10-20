using System;
using System.Threading;
using System.Windows.Input;

namespace WinformsMVP.MVP.ViewActions
{
    /// <summary>
    /// The <see cref="ICommand"/> view of one action on a <see cref="ViewActionBinder"/>
    /// (<see cref="ViewActionBinder.CommandFor(ViewAction)"/>). Execute triggers the action exactly
    /// like a bound control does; CanExecute follows the bound dispatcher's canExecute.
    /// </summary>
    internal sealed class ViewActionCommand : ICommand
    {
        private readonly ViewActionBinder _binder;
        private readonly ViewAction _key;
        private readonly Func<object, object> _payload;     // CommandParameter -> dispatch payload
        private readonly SynchronizationContext _context;   // UI thread the command was created on

        internal ViewActionCommand(ViewActionBinder binder, ViewAction key, Func<object, object> payload)
        {
            _binder = binder;
            _key = key;
            _payload = payload;
            _context = SynchronizationContext.Current;
        }

        public event EventHandler CanExecuteChanged;

        /// <summary>True once the binder is bound to a dispatcher and the action can run now.</summary>
        public bool CanExecute(object parameter) => _binder.CanTrigger(_key);

        public void Execute(object parameter) => _binder.Trigger(_key, _payload(parameter));

        // Consumers such as WinForms' Command binding update controls in this event, so it is raised
        // on the thread the command was created on (the form's UI thread).
        internal void RaiseCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            if (handler == null) return;
            if (_context == null || _context == SynchronizationContext.Current)
                handler(this, EventArgs.Empty);
            else
                _context.Post(_ => CanExecuteChanged?.Invoke(this, EventArgs.Empty), null);
        }
    }
}
