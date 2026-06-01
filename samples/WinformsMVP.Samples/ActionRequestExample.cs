using System;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples
{
    /// <summary>
    /// Example: Using ActionRequestEventArgs to solve the event explosion problem in complex forms
    ///
    /// Problem Scenario:
    /// In complex forms, there may be dozens of buttons/operations (Save, Cancel, Delete, Export, Print, Refresh, etc.)
    /// If you define a separate event for each operation, the View interface becomes very bloated.
    ///
    /// Solution:
    /// Use a unified ActionRequest event, distinguishing different operations through ViewAction.
    /// </summary>
    public static class ComplexDataGridActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("ComplexDataGrid");

        // Define ActionKeys for all operations
        public static readonly ViewAction Add = Factory.Create("Add");
        public static readonly ViewAction Edit = Factory.Create("Edit");
        public static readonly ViewAction Delete = Factory.Create("Delete");
        public static readonly ViewAction Refresh = Factory.Create("Refresh");
        public static readonly ViewAction Export = Factory.Create("Export");
        public static readonly ViewAction Import = Factory.Create("Import");
        public static readonly ViewAction Print = Factory.Create("Print");
        public static readonly ViewAction Filter = Factory.Create("Filter");
        public static readonly ViewAction Sort = Factory.Create("Sort");
        public static readonly ViewAction Search = Factory.Create("Search");
    }

    #region Traditional Approach (Event Explosion) vs ActionRequest Approach Comparison

    /// <summary>
    /// ❌ Traditional Approach - Event Explosion Problem
    /// Each operation requires a separate event definition, leading to bloated interfaces
    /// </summary>
    public interface IDataGridView_Traditional : IWindowView
    {
        // 😱 Requires defining numerous events
        event EventHandler AddRequested;
        event EventHandler EditRequested;
        event EventHandler DeleteRequested;
        event EventHandler RefreshRequested;
        event EventHandler ExportRequested;
        event EventHandler ImportRequested;
        event EventHandler PrintRequested;
        event EventHandler FilterRequested;
        event EventHandler SortRequested;
        event EventHandler SearchRequested;
        // ... possibly more operations
    }

    /// <summary>
    /// ✅ ActionRequest Approach - Concise and Elegant
    /// Only requires a single unified event, distinguishing operations through ActionKey
    /// </summary>
    public interface IDataGridView : IWindowView
    {
        // ✅ Only requires one event!
        event EventHandler<ActionRequestEventArgs> ActionRequested;

        void UpdateStatus(string message);
    }

    #endregion

    #region Presenter Implementation - Using ActionRequest

    /// <summary>
    /// Presenter uses ActionRequestEventArgs to handle all operations
    /// </summary>
    public class DataGridPresenter : WindowPresenterBase<IDataGridView>
    {
        protected override void OnViewAttached()
        {
            // ✅ Only need to subscribe to one event
            View.ActionRequested += OnViewActionTriggered;  // Use helper method provided by base class
        }

        protected override void RegisterViewActions()
        {
            // Register handlers for all operations
            _dispatcher.Register(ComplexDataGridActions.Add, OnAdd);
            _dispatcher.Register(ComplexDataGridActions.Edit, OnEdit);
            _dispatcher.Register(ComplexDataGridActions.Delete, OnDelete, canExecute: () => HasSelection());
            _dispatcher.Register(ComplexDataGridActions.Refresh, OnRefresh);
            _dispatcher.Register(ComplexDataGridActions.Export, OnExport);
            _dispatcher.Register(ComplexDataGridActions.Import, OnImport);
            _dispatcher.Register(ComplexDataGridActions.Print, OnPrint);
            _dispatcher.Register(ComplexDataGridActions.Filter, OnFilter);
            _dispatcher.Register(ComplexDataGridActions.Sort, OnSort);
            _dispatcher.Register(ComplexDataGridActions.Search, OnSearch);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            View.UpdateStatus("Ready");
        }

        #region Action Handlers

        private void OnAdd()
        {
            View.UpdateStatus("Adding new record...");
            // Implement add logic
        }

        private void OnEdit()
        {
            View.UpdateStatus("Editing record...");
            // Implement edit logic
        }

        private void OnDelete()
        {
            View.UpdateStatus("Deleting record...");
            // Implement delete logic
        }

        private void OnRefresh()
        {
            View.UpdateStatus("Refreshing data...");
            // Implement refresh logic
        }

        private void OnExport()
        {
            View.UpdateStatus("Exporting data...");
            // Implement export logic
        }

        private void OnImport()
        {
            View.UpdateStatus("Importing data...");
            // Implement import logic
        }

        private void OnPrint()
        {
            View.UpdateStatus("Printing...");
            // Implement print logic
        }

        private void OnFilter()
        {
            View.UpdateStatus("Filtering data...");
            // Implement filter logic
        }

        private void OnSort()
        {
            View.UpdateStatus("Sorting data...");
            // Implement sort logic
        }

        private void OnSearch()
        {
            View.UpdateStatus("Searching data...");
            // Implement search logic
        }

        private bool HasSelection()
        {
            // Check if there is a selected item
            return true;  // Example
        }

        #endregion

        protected override void Cleanup()
        {
            if (View != null)
            {
                View.ActionRequested -= OnViewActionTriggered;
            }
        }
    }

    #endregion

    #region ActionRequest Example with Parameters

    /// <summary>
    /// Example: ActionRequest with Parameters
    /// Used for operations that need to pass data (such as search keywords, filter conditions, etc.)
    /// </summary>
    public static class SearchActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("Search");

        public static readonly ViewAction SearchByKeyword = Factory.Create("SearchByKeyword");
        public static readonly ViewAction FilterByCategory = Factory.Create("FilterByCategory");
    }

    /// <summary>
    /// View interface - Supports ActionRequest with parameters
    /// </summary>
    public interface ISearchableDataGridView : IWindowView
    {
        // Operations without parameters
        event EventHandler<ActionRequestEventArgs> ActionRequested;

        // Operations with parameters (such as search keywords)
        event EventHandler<ActionRequestEventArgs<string>> SearchActionRequested;

        void UpdateStatus(string message);
    }

    /// <summary>
    /// Presenter - Handles ActionRequest with parameters
    /// </summary>
    public class SearchableDataGridPresenter : WindowPresenterBase<ISearchableDataGridView>
    {
        protected override void OnViewAttached()
        {
            View.ActionRequested += OnViewActionTriggered;
            View.SearchActionRequested += OnSearchActionTriggered;  // Event with parameters
        }

        protected override void RegisterViewActions()
        {
            // Register operations with parameters
            _dispatcher.Register<string>(
                SearchActions.SearchByKeyword,
                OnSearchByKeyword);

            _dispatcher.Register<string>(
                SearchActions.FilterByCategory,
                OnFilterByCategory);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            View.UpdateStatus("Ready");
        }

        // Handle SearchAction event with parameters
        private void OnSearchActionTriggered(object sender, ActionRequestEventArgs<string> e)
        {
            DispatchAction(e);  // Use base class DispatchAction method
        }

        private void OnSearchByKeyword(string keyword)
        {
            View.UpdateStatus($"Searching for keyword: {keyword}");
            // Implement search logic
        }

        private void OnFilterByCategory(string category)
        {
            View.UpdateStatus($"Filtering by category: {category}");
            // Implement filter logic
        }

        protected override void Cleanup()
        {
            if (View != null)
            {
                View.ActionRequested -= OnViewActionTriggered;
                View.SearchActionRequested -= OnSearchActionTriggered;
            }
        }
    }

    #endregion

    #region Summary and Comparison

    /*
     * Advantages of ActionRequestEventArgs:
     *
     * 1. ✅ Solves Event Explosion Problem
     *    - Traditional approach: 10 operations = 10 events
     *    - ActionRequest: 10 operations = 1 event
     *
     * 2. ✅ More Concise View Interface
     *    - No need to define separate events for each operation
     *    - Interface is easier to maintain and extend
     *
     * 3. ✅ Unified Event Handling Pattern
     *    - All operations are passed through ActionRequestEventArgs
     *    - Presenter uses unified DispatchAction method for handling
     *
     * 4. ✅ Supports Operations with Parameters
     *    - ActionRequestEventArgs<T> supports passing parameters
     *    - Type-safe, compile-time checking
     *
     * Usage Recommendations:
     *
     * - Simple forms (< 5 operations): Can use traditional separate events
     * - Complex forms (> 5 operations): Recommend using ActionRequestEventArgs
     * - Operations that need to pass parameters: Use ActionRequestEventArgs<T>
     */

    #endregion
}
