using System;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.ExecutionRequestDemo
{
    /// <summary>
    /// View interface for ExecutionRequest demonstration
    ///
    /// ⚠️ Important: Follow the three MVP iron rules
    /// 1. View interface must not contain UI types (no DialogResult, Type, Form, etc.)
    /// 2. Interface method parameters and return values must be pure data types
    /// 3. Dependency direction: Presenter → View (View does not depend on Presenter)
    ///
    /// ExecutionRequest is a learning tool for developers unfamiliar with MVP
    /// Key: Parameters and return values must be business data types, not UI types
    /// </summary>
    public interface IExecutionRequestDemoView : IWindowView
    {
        // ==================================================
        // ExecutionRequest event definitions (follows the three iron rules)
        // ==================================================

        /// <summary>
        /// Scenario 1: Request to edit customer information
        /// ✅ Parameter: CustomerData (data to edit, null = new)
        /// ✅ Return: CustomerData (edit result, null = cancelled)
        /// </summary>
        event EventHandler<ExecutionRequestEventArgs<CustomerData, CustomerData>>
            EditCustomerRequested;

        /// <summary>
        /// Scenario 2: Request to save data
        /// ✅ Parameter: CustomerData (business data)
        /// ✅ Return: bool (whether successful)
        /// </summary>
        event EventHandler<ExecutionRequestEventArgs<CustomerData, bool>>
            SaveDataRequested;

        // ==================================================
        // Display methods
        // ==================================================

        /// <summary>
        /// Display customer information
        /// </summary>
        void ShowCustomerInfo(CustomerData data);

        /// <summary>
        /// Display selected file (Scenario 3: Example using IDialogProvider)
        /// </summary>
        void ShowSelectedFile(string filePath);

        /// <summary>
        /// Update status
        /// </summary>
        void UpdateStatus(string message, bool isSuccess);
    }

    /// <summary>
    /// Customer data transfer object
    /// </summary>
    public class CustomerData
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public int Age { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Email}, {Age} years old)";
        }
    }
}
