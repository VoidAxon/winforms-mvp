using Kibinago.Views;

namespace Kibinago.Samples.MdiDemo
{
    public interface IDocumentView : IWindowView
    {
        string Title { set; }
        void ShowStatus(string text);
    }
}
