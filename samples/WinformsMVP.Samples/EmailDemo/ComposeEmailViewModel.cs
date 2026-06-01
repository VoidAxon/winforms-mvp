using WinformsMVP.Core.Models;

namespace WinformsMVP.Samples.EmailDemo
{
    /// <summary>
    /// ViewModel for compose email form
    /// Demonstrates Supervising Controller pattern with data binding
    /// </summary>
    public class ComposeEmailViewModel : BindableBase
    {
        private string _to;
        private string _subject;
        private string _body;
        private ComposeMode _mode;
        private bool _isDirty;

        public string To
        {
            get => _to;
            set => SetProperty(ref _to, value);
        }

        public string Subject
        {
            get => _subject;
            set => SetProperty(ref _subject, value);
        }

        public string Body
        {
            get => _body;
            set => SetProperty(ref _body, value);
        }

        public ComposeMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        public bool IsDirty
        {
            get => _isDirty;
            set => SetProperty(ref _isDirty, value);
        }
    }
}
