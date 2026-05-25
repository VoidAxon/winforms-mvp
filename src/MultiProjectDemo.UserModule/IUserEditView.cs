using WinformsMVP.Core.Views;

namespace MultiProjectDemo.UserModule
{
    public interface IUserEditView : IWindowView
    {
        string UserName { get; set; }
        string Email { get; set; }
        void SetTitle(string title);
    }
}
