using System;
using System.Linq;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.EmailDemo.Models;
using WinformsMVP.Samples.EmailDemo.Services;

namespace WinformsMVP.Samples.EmailDemo
{
    /// <summary>
    /// EmailAction definitions
    /// </summary>
    public static class EmailActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("Email");

        // Email operations
        public static readonly ViewAction Compose = Factory.Create("Compose");
        public static readonly ViewAction Reply = Factory.Create("Reply");
        public static readonly ViewAction Forward = Factory.Create("Forward");
        public static readonly ViewAction Delete = Factory.Create("Delete");
        public static readonly ViewAction Refresh = Factory.Create("Refresh");

        // Email status
        public static readonly ViewAction MarkAsRead = Factory.Create("MarkAsRead");
        public static readonly ViewAction MarkAsUnread = Factory.Create("MarkAsUnread");
        public static readonly ViewAction ToggleStar = Factory.Create("ToggleStar");

        // View
        public static readonly ViewAction OpenEmail = Factory.Create("OpenEmail");
    }

    /// <summary>
    /// Main email view Presenter
    /// Demonstrates complete usage of all MVP framework features
    /// </summary>
    public class MainEmailPresenter : WindowPresenterBase<IMainEmailView>
    {
        private readonly IEmailRepository _repository;
        private EmailFolder _currentFolder = EmailFolder.Inbox;

        public MainEmailPresenter(IEmailRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        protected override void OnViewAttached()
        {
            // Subscribe to View events
            View.EmailSelectionChanged += OnEmailSelectionChanged;
            View.FolderChanged += OnFolderChanged;
        }

        protected override void RegisterViewActions()
        {
            // Email operations
            _dispatcher.Register(EmailActions.Compose, OnCompose);
            _dispatcher.Register(EmailActions.Reply, OnReply,
                canExecute: () => View.HasSelection);
            _dispatcher.Register(EmailActions.Forward, OnForward,
                canExecute: () => View.HasSelection);
            _dispatcher.Register(EmailActions.Delete, OnDelete,
                canExecute: () => View.HasSelection);
            _dispatcher.Register(EmailActions.Refresh, OnRefresh);

            // Email status operations
            _dispatcher.Register(EmailActions.MarkAsRead, OnMarkAsRead,
                canExecute: () => View.HasSelection && View.SelectedEmail != null && !View.SelectedEmail.IsRead);
            _dispatcher.Register(EmailActions.MarkAsUnread, OnMarkAsUnread,
                canExecute: () => View.HasSelection && View.SelectedEmail != null && View.SelectedEmail.IsRead);
            _dispatcher.Register(EmailActions.ToggleStar, OnToggleStar,
                canExecute: () => View.HasSelection);

            // View operations
            _dispatcher.Register(EmailActions.OpenEmail, OnOpenEmail,
                canExecute: () => View.HasSelection);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            // Initial load of inbox
            View.CurrentFolder = EmailFolder.Inbox;
            LoadEmailsAsync(_currentFolder);
        }

        #region Event Handlers

        private void OnEmailSelectionChanged(object sender, EventArgs e)
        {
            // Update CanExecute status
            _dispatcher.RaiseCanExecuteChanged();

            // Show email preview
            if (View.HasSelection && View.SelectedEmail != null)
            {
                View.ShowEmailPreview(View.SelectedEmail);

                // Automatically mark as read
                if (!View.SelectedEmail.IsRead)
                {
                    _ = MarkEmailAsReadAsync(View.SelectedEmail.Id, true);  // Fire-and-forget
                }
            }
            else
            {
                View.ClearEmailPreview();
            }
        }

        private void OnFolderChanged(object sender, EmailFolder folder)
        {
            _currentFolder = folder;
            View.CurrentFolder = folder;
            LoadEmailsAsync(folder);
        }

        #endregion

        #region Action Handlers

        private void OnCompose()
        {
            var composePresenter = new ComposeEmailPresenter(_repository);
            var result = Navigator.ShowWindowAsModal<ComposeEmailPresenter, ComposeEmailParameters, bool>(
                composePresenter,
                new ComposeEmailParameters
                {
                    Mode = ComposeMode.New
                });

            if (result.IsOk && result.Value)
            {
                // Email sent, refresh list
                Messages.ShowInfo("Email sent successfully!", "Success");
                LoadEmailsAsync(_currentFolder);
            }
        }

        private void OnReply()
        {
            var originalEmail = View.SelectedEmail;
            if (originalEmail == null) return;

            var composePresenter = new ComposeEmailPresenter(_repository);
            var result = Navigator.ShowWindowAsModal<ComposeEmailPresenter, ComposeEmailParameters, bool>(
                composePresenter,
                new ComposeEmailParameters
                {
                    Mode = ComposeMode.Reply,
                    OriginalEmail = originalEmail
                });

            if (result.IsOk && result.Value)
            {
                Messages.ShowInfo("Reply sent successfully!", "Success");
                LoadEmailsAsync(_currentFolder);
            }
        }

        private void OnForward()
        {
            var originalEmail = View.SelectedEmail;
            if (originalEmail == null) return;

            var composePresenter = new ComposeEmailPresenter(_repository);
            var result = Navigator.ShowWindowAsModal<ComposeEmailPresenter, ComposeEmailParameters, bool>(
                composePresenter,
                new ComposeEmailParameters
                {
                    Mode = ComposeMode.Forward,
                    OriginalEmail = originalEmail
                });

            if (result.IsOk && result.Value)
            {
                Messages.ShowInfo("Email forwarded successfully!", "Success");
                LoadEmailsAsync(_currentFolder);
            }
        }

        private async void OnDelete()
        {
            var email = View.SelectedEmail;
            if (email == null) return;

            var confirmMessage = _currentFolder == EmailFolder.Trash
                ? "Permanently delete this email? This cannot be undone."
                : "Move this email to Trash?";

            if (!Messages.ConfirmYesNo(confirmMessage, "Delete Email"))
                return;

            View.IsLoading = true;

            try
            {
                bool success;
                if (_currentFolder == EmailFolder.Trash)
                {
                    success = await _repository.PermanentlyDeleteEmailAsync(email.Id);
                }
                else
                {
                    success = await _repository.DeleteEmailAsync(email.Id);
                }

                if (success)
                {
                    Messages.ShowInfo("Email deleted.", "Success");
                    LoadEmailsAsync(_currentFolder);
                }
                else
                {
                    Messages.ShowError("Failed to delete email.", "Error");
                }
            }
            catch (Exception ex)
            {
                Messages.ShowError($"Error deleting email: {ex.Message}", "Error");
            }
            finally
            {
                View.IsLoading = false;
            }
        }

        private void OnRefresh()
        {
            LoadEmailsAsync(_currentFolder);
        }

        private async void OnMarkAsRead()
        {
            var email = View.SelectedEmail;
            if (email == null) return;

            await MarkEmailAsReadAsync(email.Id, true);
            _dispatcher.RaiseCanExecuteChanged();
        }

        private async void OnMarkAsUnread()
        {
            var email = View.SelectedEmail;
            if (email == null) return;

            await MarkEmailAsReadAsync(email.Id, false);
            _dispatcher.RaiseCanExecuteChanged();
        }

        private async void OnToggleStar()
        {
            var email = View.SelectedEmail;
            if (email == null) return;

            View.IsLoading = true;

            try
            {
                await _repository.ToggleStarAsync(email.Id);
                LoadEmailsAsync(_currentFolder);
            }
            catch (Exception ex)
            {
                Messages.ShowError($"Error toggling star: {ex.Message}", "Error");
            }
            finally
            {
                View.IsLoading = false;
            }
        }

        private void OnOpenEmail()
        {
            var email = View.SelectedEmail;
            if (email == null) return;

            // TODO: Open detail window
            Messages.ShowInfo("Email detail window - Coming soon!", "Info");
        }

        #endregion

        #region Helper Methods

        private async void LoadEmailsAsync(EmailFolder folder)
        {
            View.IsLoading = true;
            View.StatusMessage = "Loading emails...";

            try
            {
                var emails = await _repository.GetEmailsByFolderAsync(folder);
                var emailList = emails.ToList();

                View.Emails = emailList;
                View.RefreshEmailList();

                // Update unread count
                var unreadCount = emailList.Count(e => !e.IsRead);
                View.UnreadCount = unreadCount;

                View.StatusMessage = $"{emailList.Count} emails ({unreadCount} unread)";
            }
            catch (Exception ex)
            {
                Messages.ShowError($"Error loading emails: {ex.Message}", "Error");
                View.StatusMessage = "Error loading emails";
            }
            finally
            {
                View.IsLoading = false;
            }
        }

        private async System.Threading.Tasks.Task MarkEmailAsReadAsync(int emailId, bool isRead)
        {
            try
            {
                await _repository.MarkAsReadAsync(emailId, isRead);
                LoadEmailsAsync(_currentFolder);
            }
            catch (Exception ex)
            {
                Messages.ShowError($"Error updating email status: {ex.Message}", "Error");
            }
        }

        #endregion
    }
}
