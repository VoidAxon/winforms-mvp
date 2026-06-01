using System;
using System.Text.RegularExpressions;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
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
    ///   <item><description><b>Push</b> (Presenter initiates close): <c>OnSend</c> and <c>OnDiscard</c>
    ///     raise <see cref="IRequestClose{TResult}.CloseRequested"/> via the local
    ///     <c>RaiseClose</c> helper, after finalizing dirty state.</description></item>
    ///   <item><description><b>Pull</b> (external close — user clicks X): <c>OnViewClosing</c> subscribes
    ///     to <see cref="WinformsMVP.MVP.Views.IWindowView.Closing"/> and prompts the user
    ///     to save / discard / cancel when there are unsaved changes.</description></item>
    /// </list>
    /// The single-source-of-truth principle: dirty data check lives only in <c>OnViewClosing</c>.
    /// Push-direction handlers (<c>OnSend</c>, <c>OnDiscard</c>) finalize the
    /// <see cref="ChangeTracker{T}"/> state before requesting close, so the
    /// <c>OnViewClosing</c> handler observes <c>IsChanged == false</c> and lets the close
    /// proceed without prompting.
    /// </remarks>
    public class ComposeEmailPresenter :
        WindowPresenterBase<IComposeEmailView, ComposeEmailParameters>,
        IRequestClose<bool>
    {
        public event EventHandler<CloseRequestedEventArgs<bool>> CloseRequested;
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

            // Subscribe to window close — Pull direction. Handler decides whether to allow
            // the close (e.g. prompt on unsaved changes). Push-direction closes come through
            // here too, but by then dirty state has been finalized so the prompt is skipped.
            View.Closing += OnViewClosing;
        }

        protected override void RegisterViewActions()
        {
            // Send operation
            _dispatcher.Register(ComposeEmailActions.Send, OnSend,
                canExecute: () => !string.IsNullOrWhiteSpace(View.To) &&
                                  !string.IsNullOrWhiteSpace(View.Subject));

            // Save draft operation
            _dispatcher.Register(ComposeEmailActions.SaveDraft, OnSaveDraft,
                canExecute: () => _changeTracker?.IsChanged == true);

            // Discard operation
            _dispatcher.Register(ComposeEmailActions.Discard, OnDiscard);

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
                _dispatcher.RaiseCanExecuteChanged();
            };
        }

        /// <summary>
        /// Pull-direction close handler. Runs whenever the underlying Form is about to close
        /// (user clicked X, system shutdown, parent closing, or because Push-direction
        /// <c>RequestClose</c> triggered <c>Form.Close()</c>).
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///   <item><description>System shutdown / task manager: skip prompts, let the process exit.</description></item>
        ///   <item><description>Push-direction closes (after <c>OnSend</c> / <c>OnDiscard</c>):
        ///     <c>IsChanged</c> will be <c>false</c> here, so this handler is a no-op.</description></item>
        ///   <item><description>User clicked X with unsaved changes: prompt Yes/No/Cancel.</description></item>
        /// </list>
        /// </remarks>
        private void OnViewClosing(object sender, WindowClosingEventArgs args)
        {
            if (args.Reason != CloseReason.Normal) return;
            if (_changeTracker == null || !_changeTracker.IsChanged) return;

            var result = Messages.ConfirmYesNoCancel(
                "You have unsaved changes. Do you want to save as draft?",
                "Unsaved Changes");

            if (result == ConfirmResult.Cancel)
            {
                args.Cancel = true;
                return;
            }

            if (result == ConfirmResult.Yes)
            {
                OnSaveDraft();  // fire-and-forget like the original CanClose behavior
            }
            // No: fall through; args.Cancel remains false → window closes.
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
            _dispatcher.RaiseCanExecuteChanged();
        }

        #endregion

        #region Action Handlers

        private async void OnSend()
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
                    RaiseClose(true, InteractionStatus.Ok);  // Return true indicating sent
                }
                else
                {
                    Messages.ShowError("Failed to send email.", "Error");
                }
            }
            catch (Exception ex)
            {
                Messages.ShowError($"Error sending email: {ex.Message}", "Error");
            }
            finally
            {
                View.IsSaving = false;
                View.EnableInput = true;
            }
        }

        private async void OnSaveDraft()
        {
            View.IsSaving = true;

            try
            {
                var email = CreateEmailMessage();
                int draftId = await _repository.SaveDraftAsync(email);

                _changeTracker.AcceptChanges();  // Accept changes
                Messages.ShowInfo("Draft saved.", "Success");
            }
            catch (Exception ex)
            {
                Messages.ShowError($"Error saving draft: {ex.Message}", "Error");
            }
            finally
            {
                View.IsSaving = false;
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
                // OnViewClosing handler sees IsChanged == false and does not re-prompt.
                _changeTracker.RejectChanges();
            }

            RaiseClose(false, InteractionStatus.Cancel);  // Return false indicating not sent
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

        private void RaiseClose(bool result, InteractionStatus status)
            => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<bool>(result, status));
    }
}
