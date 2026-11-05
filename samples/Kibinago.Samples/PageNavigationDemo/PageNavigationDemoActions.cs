using Kibinago.ViewActions;

namespace Kibinago.Samples.PageNavigationDemo
{
    public static class PageNavigationDemoActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("PageNavigationDemo");

        /// <summary>A crumb was clicked; payload = its level (0 = the first page).</summary>
        public static readonly ViewAction<int> Breadcrumb = Factory.Create<int>(nameof(Breadcrumb));

        /// <summary>受注編集: 保存.</summary>
        public static readonly ViewAction SaveOrder = Factory.Create(nameof(SaveOrder));
    }
}
