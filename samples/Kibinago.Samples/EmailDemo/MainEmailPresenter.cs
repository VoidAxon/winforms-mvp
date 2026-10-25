using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Samples.EmailDemo.Models;
using Kibinago.Samples.EmailDemo.Services;

namespace Kibinago.Samples.EmailDemo
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
    /// <remarks>
    /// Repository-backed actions are <c>async Task</c> handlers: the dispatcher disables a running
    /// action and ignores repeated clicks, and logs unexpected failures. The initial inbox load
    /// runs in <see cref="OnInitializeAsync"/>, so the window shows immediately.
    /// </remarks>
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
            Dispatcher.Register(EmailActions.Compose, OnComposeAsync);
            Dispatcher.Register(EmailActions.Reply, OnReplyAsync,
                canExecute: () => View.HasSelection);
            Dispatcher.Register(EmailActions.Forward, OnForwardAsync,
                canExecute: () => View.HasSelection);
            Dispatcher.Register(EmailActions.Delete, OnDeleteAsync,
                canExecute: () => View.HasSelection);
            Dispatcher.Register(EmailActions.Refresh, OnRefreshAsync);

            // Email status operations
            Dispatcher.Register(EmailActions.MarkAsRead, OnMarkAsReadAsync,
                canExecute: () => View.HasSelection && View.SelectedEmail != null && !View.SelectedEmail.IsRead);
            Dispatcher.Register(EmailActions.MarkAsUnread, OnMarkAsUnreadAsync,
                canExecute: () => View.HasSelection && View.SelectedEmail != null && View.SelectedEmail.IsRead);
            Dispatcher.Register(EmailActions.ToggleStar, OnToggleStarAsync,
                canExecute: () => View.HasSelection);

            // View operations
            Dispatcher.Register(EmailActions.OpenEmail, OnOpenEmail,
                canExecute: () => View.HasSelection);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            View.CurrentFolder = EmailFolder.Inbox;
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            // Initial load of inbox — runs after OnInitialize without blocking the window;
            // the token is cancelled if the window closes first.
            return LoadEmailsAsync(_currentFolder, cancellationToken);
        }

        #region Event Handlers

        private void OnEmailSelectionChanged(object sender, EventArgs e)
        {
            // Update CanExecute status
            Dispatcher.RaiseCanExecuteChanged();

            // Show email preview
            if (View.HasSelection && View.SelectedEmail != null)
            {
                View.ShowEmailPreview(View.SelectedEmail);

                // Automatically mark as read
                if (!View.SelectedEmail.IsRead)
                {
                    // Fire-and-forget from an event handler: MarkEmailAsReadAsync handles its own errors.
                    _ = MarkEmailAsReadAsync(View.SelectedEmail.Id, true);
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
            // Fire-and-forget from an event handler: LoadEmailsAsync handles its own errors.
            _ = LoadEmailsAsync(folder, Lifetime);
        }

        #endregion

        #region Action Handlers

        private async Task OnComposeAsync()
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
                await LoadEmailsAsync(_currentFolder, Lifetime);
            }
        }

        private async Task OnReplyAsync()
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
                await LoadEmailsAsync(_currentFolder, Lifetime);
            }
        }

        private async Task OnForwardAsync()
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
                await LoadEmailsAsync(_currentFolder, Lifetime);
            }
        }

        private async Task OnDeleteAsync()
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
                    await LoadEmailsAsync(_currentFolder, Lifetime);
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

        private Task OnRefreshAsync(CancellationToken cancellationToken)
        {
            return LoadEmailsAsync(_currentFolder, cancellationToken);
        }

        private async Task OnMarkAsReadAsync()
        {
            var email = View.SelectedEmail;
            if (email == null) return;

            await MarkEmailAsReadAsync(email.Id, true);
            Dispatcher.RaiseCanExecuteChanged();
        }

        private async Task OnMarkAsUnreadAsync()
        {
            var email = View.SelectedEmail;
            if (email == null) return;

            await MarkEmailAsReadAsync(email.Id, false);
            Dispatcher.RaiseCanExecuteChanged();
        }

        private async Task OnToggleStarAsync()
        {
            var email = View.SelectedEmail;
            if (email == null) return;

            View.IsLoading = true;

            try
            {
                await _repository.ToggleStarAsync(email.Id);
                await LoadEmailsAsync(_currentFolder, Lifetime);
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

        /// <summary>
        /// Loads a folder into the view. Never faults: errors are shown to the user, and a load
        /// cancelled because the window closed simply stops without touching the view.
        /// </summary>
        private async Task LoadEmailsAsync(EmailFolder folder, CancellationToken cancellationToken)
        {
            View.IsLoading = true;
            View.StatusMessage = "Loading emails...";

            try
            {
                var emails = await _repository.GetEmailsByFolderAsync(folder);
                if (cancellationToken.IsCancellationRequested) return;   // window closed meanwhile

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

        private async Task MarkEmailAsReadAsync(int emailId, bool isRead)
        {
            try
            {
                await _repository.MarkAsReadAsync(emailId, isRead);
                await LoadEmailsAsync(_currentFolder, Lifetime);
            }
            catch (Exception ex)
            {
                Messages.ShowError($"Error updating email status: {ex.Message}", "Error");
            }
        }

        #endregion
    }
}
