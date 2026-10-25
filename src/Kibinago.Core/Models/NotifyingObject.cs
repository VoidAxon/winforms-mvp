using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kibinago.Models
{
    /// <summary>
    /// Base class for a model that raises <see cref="PropertyChanged"/> when a property changes — typically a
    /// screen's input model, which the form binds to (Supervising Controller). Write each property as
    /// <c>set => SetProperty(ref _field, value)</c>. Not thread-safe: change it on the UI thread.
    /// </summary>
    public class NotifyingObject : INotifyPropertyChanged
    {
        /// <summary>Raised after a property value changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Assigns <paramref name="value"/> to <paramref name="storage"/> and raises
        /// <see cref="PropertyChanged"/> when it differs (<see cref="EqualityComparer{T}.Default"/>).
        /// </summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="storage">The property's backing field.</param>
        /// <param name="value">The new value.</param>
        /// <param name="propertyName">The property name; filled in by the compiler when called from the setter.</param>
        /// <returns><c>true</c> when the value changed; <c>false</c> when it was equal and nothing was raised.</returns>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>Raises <see cref="PropertyChanged"/> for <paramref name="propertyName"/>.</summary>
        /// <param name="propertyName">The changed property; filled in by the compiler when called from the property.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
