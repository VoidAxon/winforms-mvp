using System.Linq;
using Kibinago.Presenters;

namespace Kibinago.Samples.CompositionDemo
{
    /// <summary>
    /// A row's presenter: edits the ContactRow the parent gave it (TParam) and checks its phone number.
    /// It knows nothing of the other rows or of the parent.
    /// </summary>
    public class ContactRowPresenter : ControlPresenterBase<IContactRowView, ContactRow>
    {
        private ContactRow _row;

        protected override void OnInitialize(ContactRow row)
        {
            _row = row;
            View.ContactName = row.Name;
            View.Relation = row.Relation;
            View.Phone = row.Phone;
            SubscribeViewEvent(h => View.Edited += h, h => View.Edited -= h, OnEdited);
        }

        private void OnEdited()
        {
            _row.Name = View.ContactName;
            _row.Relation = View.Relation;
            _row.Phone = View.Phone;
            View.ShowPhoneError(_row.Phone.Length == 0 || _row.Phone.All(c => char.IsDigit(c) || c == '-')
                ? null
                : "電話番号は数字とハイフンで入力してください");
        }
    }
}
