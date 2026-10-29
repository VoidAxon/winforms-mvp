using System;
using System.Collections.Generic;
using Kibinago.Views;

namespace Kibinago.Samples.Shell
{
    /// <summary>
    /// The sample's main window: a sidebar of categories and, on the right, the tabs of the selected
    /// category (説明, one per demo, コード).
    /// </summary>
    public interface IShellView : IWindowView
    {
        /// <summary>Fills the sidebar.</summary>
        void ShowCategories(IReadOnlyList<SampleCategory> categories);

        /// <summary>Selects a category in the sidebar (raises <see cref="CategorySelected"/>).</summary>
        void SelectCategory(string categoryId);

        /// <summary>Replaces the tabs with the category's: 説明, an empty tab per demo, コード.</summary>
        void ShowCategory(SampleCategory category);

        /// <summary>
        /// Creates the view of an embedded demo inside its tab and returns it, for its presenter.
        /// </summary>
        IView CreateDemoView(string demoId);

        /// <summary>Disposes and removes the view <see cref="CreateDemoView"/> created for a demo.</summary>
        void RemoveDemoView(string demoId);

        /// <summary>Disposes and removes every view created by <see cref="CreateDemoView"/>.</summary>
        void RemoveDemoViews();

        /// <summary>Shows the source files of a demo (or of the category) in the コード tab.</summary>
        void ShowSource(IReadOnlyList<string> sourceFiles);

        /// <summary>The user picked a category in the sidebar (its id).</summary>
        event EventHandler<string> CategorySelected;

        /// <summary>The user opened a demo's tab (its id).</summary>
        event EventHandler<string> DemoSelected;
    }
}
