using Kibinago.Views;

namespace Kibinago.Samples.MdiDemo
{
    /// <summary>An MDI parent: it places the documents itself, so they are prepared, not shown, by Navigator.</summary>
    public interface IMdiHostView : IWindowView
    {
        /// <summary>Places a prepared document window as an MDI child and shows it.</summary>
        void ShowDocument(IWindowView document);

        void ShowStatus(string text);
    }
}
