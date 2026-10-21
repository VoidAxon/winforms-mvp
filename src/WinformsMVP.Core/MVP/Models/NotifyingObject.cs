using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinformsMVP.MVP.Models
{
    /// <summary>
    /// Base class for a model that raises <see cref="PropertyChanged"/> when a property changes — typically a
    /// screen's input model, which the form binds to (Supervising Controller). Write each property as
    /// <c>set => SetProperty(ref _field, value)</c>. Not thread-safe: change it on the UI thread.
    /// </summary>
    public class NotifyingObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
