using Kibinago.Common;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Samples.LegacyPopupDemo
{
    /// <summary>
    /// A migrated (MVP) screen that still opens a legacy, non-MVP popup. The popup is opened by a
    /// semantic view method: the presenter decides when and handles the result; the Form decides how
    /// (which legacy form, how its values are passed, disposal). No interface or service is needed —
    /// for a popup shared by many screens, a small service (wiki: migration guide, Step 7) fits better.
    /// </summary>
    public interface ILegacyPopupDemoView : IWindowView
    {
        /// <summary>Opens the legacy editor on <paramref name="current"/> (null = new) and returns what it produced.</summary>
        InteractionResult<CustomerData> EditCustomer(CustomerData current);

        void ShowCustomer(CustomerData customer);

        void ShowStatus(string message);
    }

    public sealed class CustomerData
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public int Age { get; set; }

        public override string ToString() => $"{Name} ({Email}, {Age} years old)";
    }

    public static class LegacyPopupDemoActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("LegacyPopupDemo");

        public static readonly ViewAction EditCustomer = Factory.Create(nameof(EditCustomer));
    }
}
