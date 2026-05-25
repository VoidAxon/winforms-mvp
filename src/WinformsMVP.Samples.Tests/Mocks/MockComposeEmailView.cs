using System;
using System.Collections.Generic;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.EmailDemo;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IComposeEmailView for testing.
    /// Records all method calls and simulates view behavior.
    /// </summary>
    public class MockComposeEmailView : IComposeEmailView
    {
        // Internal state
        private string _to = string.Empty;
        private string _subject = string.Empty;
        private string _body = string.Empty;
        private ComposeMode _mode = ComposeMode.New;
        private bool _isSaving = false;
        private bool _isDirty = false;
        private bool _enableInput = true;

        // Record method calls
        public List<string> MethodCalls { get; } = new List<string>();

        // IWindowView members
        public bool IsDisposed { get; private set; } = false;
        public IntPtr Handle => IntPtr.Zero;

        public void Activate()
        {
            MethodCalls.Add("Activate()");
        }

        // IWindowView.Closing — tests can call RaiseClosing(args) to simulate the framework
        // triggering a close (e.g. user clicking X). Inspect args.Cancel after to verify
        // whether the Presenter blocked the close.
        public event EventHandler<WindowClosingEventArgs> Closing;
        public void OnClosing(WindowClosingEventArgs args) => Closing?.Invoke(this, args);
        public void RaiseClosing(WindowClosingEventArgs args) => OnClosing(args);

        // IComposeEmailView implementation
        public string To
        {
            get => _to;
            set
            {
                MethodCalls.Add($"To = {value}");
                _to = value;
            }
        }

        public string Subject
        {
            get => _subject;
            set
            {
                MethodCalls.Add($"Subject = {value}");
                _subject = value;
            }
        }

        public string Body
        {
            get => _body;
            set
            {
                MethodCalls.Add($"Body = {value}");
                _body = value;
            }
        }

        public ComposeMode Mode
        {
            get => _mode;
            set
            {
                MethodCalls.Add($"Mode = {value}");
                _mode = value;
            }
        }

        public bool IsSaving
        {
            set
            {
                MethodCalls.Add($"IsSaving = {value}");
                _isSaving = value;
            }
        }

        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                MethodCalls.Add($"IsDirty = {value}");
                _isDirty = value;
            }
        }

        public bool EnableInput
        {
            set
            {
                MethodCalls.Add($"EnableInput = {value}");
                _enableInput = value;
            }
        }

        public IViewActionBinder ActionBinder { get; } = NullViewActionBinder.Instance;

        // Events
        public event EventHandler EmailDataChanged;

        // Test helper methods
        /// <summary>
        /// Simulate user changing email data (To, Subject, or Body)
        /// </summary>
        public void SimulateEmailDataChange()
        {
            _isDirty = true;
            EmailDataChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Get current IsSaving value
        /// </summary>
        public bool GetIsSaving() => _isSaving;

        /// <summary>
        /// Get current EnableInput value
        /// </summary>
        public bool GetEnableInput() => _enableInput;

        /// <summary>
        /// Clear all state and method calls
        /// </summary>
        public void Clear()
        {
            _to = string.Empty;
            _subject = string.Empty;
            _body = string.Empty;
            _mode = ComposeMode.New;
            _isSaving = false;
            _isDirty = false;
            _enableInput = true;
            MethodCalls.Clear();
        }
    }
}
