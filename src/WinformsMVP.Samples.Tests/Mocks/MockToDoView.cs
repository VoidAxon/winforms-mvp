using System;
using System.Collections.Generic;
using WinformsMVP.Common.Events;
using WinformsMVP.Samples.ToDoDemo;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IToDoView for testing.
    /// Records all method calls and simulates view behavior.
    /// </summary>
    public class MockToDoView : IToDoView
    {
        // Internal state
        private readonly List<string> _tasks = new List<string>();
        private int _selectedIndex = -1;
        private bool _hasPendingChanges = false;

        // Record method calls
        public List<string> MethodCalls { get; } = new List<string>();
        public List<string> StatusMessages { get; } = new List<string>();

        // IWindowView members
        public bool IsDisposed { get; private set; } = false;
        public IntPtr Handle => IntPtr.Zero;

        public void Activate()
        {
            MethodCalls.Add("Activate()");
        }

        // IWindowView.Closing — tests can call RaiseClosing(args) to simulate the framework
        // triggering a close (e.g. user clicking X).
        public event EventHandler<WindowClosingEventArgs> Closing;
        public void OnClosing(WindowClosingEventArgs args) => Closing?.Invoke(this, args);
        public void RaiseClosing(WindowClosingEventArgs args) => OnClosing(args);

        // IToDoView implementation
        public string TaskText { get; set; }

        public bool HasSelectedTask => _selectedIndex >= 0 && _selectedIndex < _tasks.Count;

        public bool HasPendingChanges => _hasPendingChanges;

        public void AddTaskToList(string task)
        {
            MethodCalls.Add($"AddTaskToList({task})");
            _tasks.Add(task);
            _hasPendingChanges = true;
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveSelectedTask()
        {
            MethodCalls.Add("RemoveSelectedTask()");
            if (HasSelectedTask)
            {
                _tasks.RemoveAt(_selectedIndex);
                _selectedIndex = -1;
                _hasPendingChanges = true;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void CompleteSelectedTask()
        {
            MethodCalls.Add("CompleteSelectedTask()");
            if (HasSelectedTask)
            {
                _tasks[_selectedIndex] = "[Done] " + _tasks[_selectedIndex];
                _hasPendingChanges = true;
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ClearPendingChanges()
        {
            MethodCalls.Add("ClearPendingChanges()");
            _hasPendingChanges = false;
        }

        public void UpdateStatus(string message)
        {
            MethodCalls.Add($"UpdateStatus({message})");
            StatusMessages.Add(message);
        }

        public IViewActionBinder ActionBinder { get; } = NullViewActionBinder.Instance;

        // Events
        public event EventHandler SelectionChanged;
        public event EventHandler DataChanged;

        // Test helper methods
        public void SelectTask(int index)
        {
            _selectedIndex = index;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearSelection()
        {
            _selectedIndex = -1;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public int TaskCount => _tasks.Count;

        public string GetTask(int index) => _tasks[index];

        public void Clear()
        {
            _tasks.Clear();
            _selectedIndex = -1;
            _hasPendingChanges = false;
            MethodCalls.Clear();
            StatusMessages.Clear();
        }
    }
}
