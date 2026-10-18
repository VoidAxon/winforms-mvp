using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.EmailDemo.Models;
using WinformsMVP.Samples.EmailDemo.Services;

namespace WinformsMVP.Samples.EmailDemo
{
    /// <summary>
    /// ComposeEmailActions definition
    /// </summary>
    public static class ComposeEmailActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("ComposeEmail");

        public static readonly ViewAction Send = Factory.Create("Send");
        public static readonly ViewAction SaveDraft = Factory.Create("SaveDraft");
        public static readonly ViewAction Discard = Factory.Create("Discard");
    }

    /// <summary>
    /// Compose email Presenter.
    /// </summary>
    /// <remarks>
    /// Demonstrates the canonical two-direction close pattern:
    /// <list type="bullet">
    ///   <item><description><b>Push</b> (Presenter initiates close): <c>OnSendAsync</c> and <c>OnDiscard</c>
    ///     call <c>RequestClose(...)</c> after finalizing dirty state.</description></item>
    ///   <item><description><b>Pull</b> (external close — user clicks X): <see cref="CanCloseAsync"/> prompts
    ///     the user to save / discard / cancel when there are unsaved changes, and awaits the save.</description></item>
    /// </list>
    /// The single-source-of-truth principle: dirty data check lives only in <see cref="CanCloseAsync"/>.
    /// Push-direction handlers (<c>OnSendAsync</c>, <c>OnDiscard</c>) finalize the
    /// <see cref="ChangeTracker{T}"/> state before requesting close, so the
    /// <see cref="CanCloseAsync"/> handler observes <c>IsChanged == false</c> and lets the close
    /// proceed without prompting.
    /// <para>
    /// Send and SaveDraft are <c>async Task</c> handlers: while one runs the dispatcher disables
    /// it and ignores repeated clicks, so no busy flag is needed for that.
    /// </para>
    /// </remarks>
    public class ComposeEmailPresenter :
        WindowPresenterBase<IComposeEmailView, ComposeEmailParameters>
    {
        private readonly IEmailRepository _repository;
        private ChangeTracker<EmailMessage> _changeTracker;
        private ComposeMode _mode;
        private EmailMessage _originalEmail;

        public ComposeEmailPresenter(IEmailRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        protected override void OnViewAttached()
        {
            // Subscribe to semantic email data change event
            View.EmailDataChanged += OnEmailDataChanged;
        }

        protected override void RegisterViewActions()
        {
            // Send operation
            Dispatcher.Register(ComposeEmailActions.Send, OnSendAsync,
                canExecute: () => !string.IsNullOrWhiteSpace(View.To) &&
                                  !string.IsNullOrWhiteSpace(View.Subject));

            // Save draft operation
            Dispatcher.Register(ComposeEmailActions.SaveDraft, OnSaveDraftAsync,
                canExecute: () => _changeTracker?.IsChanged == true);

            // Discard operation
            Dispatcher.Register(ComposeEmailActions.Discard, OnDiscard);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize(ComposeEmailParameters parameters)
        {
            _mode = parameters.Mode;
            _originalEmail = parameters.OriginalEmail;

            // Initialize email content based on mode
            var email = CreateEmailFromMode();

            // Use ChangeTracker to track changes (for save draft)
            _changeTracker = new ChangeTracker<EmailMessage>(email);

            // Bind to View
            View.Mode = _mode;
            View.To = email.To;
            View.Subject = email.Subject;
            View.Body = email.Body;

            // Subscribe to ChangeTracker change events
            _changeTracker.IsChangedChanged += (s, e) =>
            {
                View.IsDirty = _changeTracker.IsChanged;
                Dispatcher.RaiseCanExecuteChanged();
            };
        }

        /// <summary>
        /// Pull-direction close handler. Runs whenever the underlying Form is about to close
        /// (user clicked X, system shutdown, parent closing, or because Push-direction
        /// <c>RequestClose</c> triggered the close).
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///   <item><description>Only normal user-driven closes are guarded; system shutdown /
        ///     task manager / parent-closing let the process exit without prompting.</description></item>
        ///   <item><description>Push-direction closes (after <c>OnSendAsync</c> / <c>OnDiscard</c>):
        ///     <c>IsChanged</c> will be <c>false</c> here, so this handler allows the close.</description></item>
        ///   <item><description>User clicked X with unsaved changes: prompt Yes/No/Cancel. On Yes the
        ///     draft save is awaited (the window stays open meanwhile); if it fails the close is
        ///     blocked so the user does not lose the text.</description></item>
        /// </list>
        /// </remarks>
        protected override async Task<bool> CanCloseAsync(CloseReason reason)
        {
            if (reason != CloseReason.Normal) return true;
            if (_changeTracker == null || !_changeTracker.IsChanged) return true;

            var result = Messages.ConfirmYesNoCancel(
                "You have unsaved changes. Do you want to save as draft?",
                "Unsaved Changes");

            if (result == ConfirmResult.Cancel)
                return false;

            if (result == ConfirmResult.Yes)
            {
                return await SaveDraftAsync();
            }
            // No: fall through; the close proceeds.
            return true;
        }

        #region Event Handlers

        /// <summary>
        /// Called when email data (To, Subject, Body) changes.
        /// Updates the ChangeTracker and triggers CanExecute re-evaluation for Send/SaveDraft buttons.
        /// </summary>
        private void OnEmailDataChanged(object sender, EventArgs e)
        {
            // Update ChangeTracker with current email values
            var currentEmail = new EmailMessage
            {
                To = View.To,
                Subject = View.Subject,
                Body = View.Body
            };

            _changeTracker.UpdateCurrentValue(currentEmail);

            // Trigger CanExecute update for Send and SaveDraft actions
            Dispatcher.RaiseCanExecuteChanged();
        }

        #endregion

        #region Action Handlers

        private async Task OnSendAsync()
        {
            // Validate input
            if (!ValidateInput(out string errorMessage))
            {
                Messages.ShowWarning(errorMessage, "Validation Failed");
                return;
            }

            View.IsSaving = true;
            View.EnableInput = false;

            try
            {
                var email = CreateEmailMessage();
                bool success = await _repository.SendEmailAsync(email);

                if (success)
                {
                    _changeTracker.AcceptChanges();  // Accept changes, mark as unmodified
                    RequestClose(true, InteractionStatus.Ok);  // Return true indicating sent
                }
                else
                {
                    Messages.ShowError("Failed to send email.", "Error");
                }
            }
            catch (Exception ex)
            {
                // A failed send is an expected error: tell the user (the window stays open).
                Messages.ShowError($"Error sending email: {ex.Message}", "Error");
            }
            finally
            {
                View.IsSaving = false;
                View.EnableInput = true;
            }
        }

        private async Task OnSaveDraftAsync()
        {
            if (await SaveDraftAsync())
            {
                Messages.ShowInfo("Draft saved.", "Success");
            }
        }

        /// <summary>
        /// Saves the current content as a draft. Shared by the SaveDraft action and the
        /// close prompt. Returns false (after telling the user) when the save failed.
        /// </summary>
        private async Task<bool> SaveDraftAsync()
        {
            // Editing is locked while the save runs, so the snapshot being saved is what the user
            // sees; otherwise edits made during the wait would be marked clean and lost on close.
            View.IsSaving = true;
            View.EnableInput = false;

            try
            {
                var email = CreateEmailMessage();
                await _repository.SaveDraftAsync(email);

                // Accept only if nothing changed after the snapshot was taken.
                if (email.To != View.To || email.Subject != View.Subject || email.Body != View.Body)
                    return false;
                _changeTracker.AcceptChanges();
                return true;
            }
            catch (Exception ex)
            {
                Messages.ShowError($"Error saving draft: {ex.Message}", "Error");
                return false;
            }
            finally
            {
                View.IsSaving = false;
                View.EnableInput = true;
            }
        }

        private void OnDiscard()
        {
            if (_changeTracker.IsChanged)
            {
                if (!Messages.ConfirmYesNo("Discard all changes?", "Confirm Discard"))
                {
                    return;
                }
                // Finalize dirty state BEFORE RequestClose so that the Pull-direction
                // CanClose handler sees IsChanged == false and does not re-prompt.
                _changeTracker.RejectChanges();
            }

            RequestClose(InteractionStatus.Cancel);  // Return false indicating not sent — use no-result overload (null not inferrable)
        }

        #endregion

        #region Helper Methods

        private EmailMessage CreateEmailFromMode()
        {
            switch (_mode)
            {
                case ComposeMode.Reply:
                    if (_originalEmail == null)
                        throw new InvalidOperationException("Original email required for Reply mode");

                    return new EmailMessage
                    {
                        To = _originalEmail.From,
                        Subject = "Re: " + _originalEmail.Subject,
                        Body = $"\n\n--- Original Message ---\nFrom: {_originalEmail.From}\nDate: {_originalEmail.Date}\n\n{_originalEmail.Body}"
                    };

                case ComposeMode.Forward:
                    if (_originalEmail == null)
                        throw new InvalidOperationException("Original email required for Forward mode");

                    return new EmailMessage
                    {
                        To = "",
                        Subject = "Fwd: " + _originalEmail.Subject,
                        Body = $"\n\n--- Forwarded Message ---\nFrom: {_originalEmail.From}\nDate: {_originalEmail.Date}\nSubject: {_originalEmail.Subject}\n\n{_originalEmail.Body}"
                    };

                case ComposeMode.New:
                default:
                    return new EmailMessage
                    {
                        To = "",
                        Subject = "",
                        Body = ""
                    };
            }
        }

        private EmailMessage CreateEmailMessage()
        {
            return new EmailMessage
            {
                From = "me@mycompany.com",  // Current user
                To = View.To,
                Subject = View.Subject,
                Body = View.Body,
                Date = DateTime.Now,
                IsRead = true,
                IsStarred = false,
                Folder = EmailFolder.Sent  // Sent emails go to Sent folder
            };
        }

        private bool ValidateInput(out string errorMessage)
        {
            // Validate recipient
            if (string.IsNullOrWhiteSpace(View.To))
            {
                errorMessage = "Recipient (To) is required.";
                return false;
            }

            // Validate email format
            if (!IsValidEmail(View.To))
            {
                errorMessage = "Invalid email address format.";
                return false;
            }

            // Validate subject
            if (string.IsNullOrWhiteSpace(View.Subject))
            {
                errorMessage = "Subject is required.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Simple email format validation
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return regex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
