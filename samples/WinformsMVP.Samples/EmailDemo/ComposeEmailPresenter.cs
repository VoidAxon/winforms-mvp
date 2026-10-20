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
        // Style 2 ("a new object each time"): the editable fields are read from the view into a new
        // EmailEditState whenever they are needed, and compared against the tracker's snapshot with
        // IsChangedWith. Only the three fields the user edits are tracked; the EmailMessage built
        // for sending also carries a date, sender and folder, which are not edits.
        private ChangeTracker<EmailEditState> _changeTracker;
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
                canExecute: () => HasUnsavedChanges);

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

            // Bind to View
            View.Mode = _mode;
            View.To = email.To;
            View.Subject = email.Subject;
            View.Body = email.Body;

            // Snapshot what the user starts from.
            _changeTracker = TrackChanges(ReadEditState());
            View.IsDirty = false;
        }

        /// <summary>The fields the user edits, read from the view.</summary>
        private sealed class EmailEditState
        {
            public string To { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
        }

        private EmailEditState ReadEditState() => new EmailEditState
        {
            To = View.To,
            Subject = View.Subject,
            Body = View.Body
        };

        private bool HasUnsavedChanges => _changeTracker != null && _changeTracker.IsChangedWith(ReadEditState());

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
        ///     those handlers accept the current content first, so there are no unsaved changes here
        ///     and this handler allows the close.</description></item>
        ///   <item><description>User clicked X with unsaved changes: prompt Yes/No/Cancel. On Yes the
        ///     draft save is awaited (the window stays open meanwhile); if it fails the close is
        ///     blocked so the user does not lose the text.</description></item>
        /// </list>
        /// </remarks>
        protected override async Task<bool> CanCloseAsync(CloseReason reason)
        {
            if (reason != CloseReason.Normal) return true;
            if (!HasUnsavedChanges) return true;

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
        /// Called when email data (To, Subject, Body) changes: refreshes the dirty indicator and
        /// re-evaluates Send / SaveDraft (Send also depends on the fields being filled in).
        /// </summary>
        private void OnEmailDataChanged(object sender, EventArgs e)
        {
            View.IsDirty = HasUnsavedChanges;
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
                var sent = ReadEditState();
                bool success = await _repository.SendEmailAsync(CreateEmailMessage(sent));

                if (success)
                {
                    // What was sent is now the accepted content, so the close below is not
                    // questioned by CanCloseAsync (unless the user edited while it was sending).
                    _changeTracker.AcceptChanges(sent);
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
        /// close prompt. Returns true only when everything on screen is saved: false (after
        /// telling the user) when the save failed, and false when the content changed while the
        /// save ran.
        /// </summary>
        private async Task<bool> SaveDraftAsync()
        {
            View.IsSaving = true;
            View.EnableInput = false;

            try
            {
                // Save a snapshot of the fields; edits made meanwhile are compared against it
                // afterwards and stay unsaved.
                var submitted = ReadEditState();
                await _repository.SaveDraftAsync(CreateEmailMessage(submitted));

                _changeTracker.AcceptChanges(submitted);
                View.IsDirty = HasUnsavedChanges;
                Dispatcher.RaiseCanExecuteChanged();
                return !View.IsDirty;
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
            if (HasUnsavedChanges)
            {
                if (!Messages.ConfirmYesNo("Discard all changes?", "Confirm Discard"))
                {
                    return;
                }
                // Finalize the dirty state BEFORE RequestClose so that the Pull-direction
                // CanClose handler sees no unsaved changes and does not ask again. This ends the
                // tracking of the discarded edits; it does not mean they were saved.
                _changeTracker.AcceptChanges(ReadEditState());
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

        private static EmailMessage CreateEmailMessage(EmailEditState content)
        {
            return new EmailMessage
            {
                From = "me@mycompany.com",  // Current user
                To = content.To,
                Subject = content.Subject,
                Body = content.Body,
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
