using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Validation;
using Xunit;

namespace Kibinago.Samples.Tests.Common
{
    /// <summary>
    /// The failed-submit recipe: ValidationState.ErrorsInOrder (the screen's order) and
    /// ControlErrorDisplay.TryFocusError (put the cursor on the field).
    /// </summary>
    public class ValidationErrorOrderAndFocusTests
    {
        private sealed class Visit
        {
            [Required(ErrorMessage = "Date is required.")]
            public string Date { get; set; }

            [Required(ErrorMessage = "Staff is required.")]
            [MinLength(3, ErrorMessage = "Staff is too short.")]
            public string Staff { get; set; } = "";

            [Required(ErrorMessage = "Body is required.")]
            public string Body { get; set; }
        }

        private static IEnumerable<string> Fields(IReadOnlyList<(string Field, string Message)> errors) => errors.Select(e => e.Field);

        // ---- ErrorsInOrder ----------------------------------------------------------------------

        [Fact]
        public void ErrorsInOrder_FollowsTheGivenOrder()
        {
            var state = new ValidationState();
            state.Validate(new Visit());

            var errors = state.ErrorsInOrder(nameof(Visit.Body), nameof(Visit.Date), nameof(Visit.Staff));

            Assert.Equal(new[] { nameof(Visit.Body), nameof(Visit.Date), nameof(Visit.Staff) }, Fields(errors));
            Assert.Equal("Body is required.", errors[0].Message);
        }

        [Fact]
        public void ErrorsInOrder_KeepsEveryMessageOfAField_ModelRulesFirst()
        {
            var state = new ValidationState();
            state.Validate(new Visit { Date = "x", Body = "x", Staff = "ab" });
            state.SetError(nameof(Visit.Staff), "Staff has left.");

            var errors = state.ErrorsInOrder(nameof(Visit.Staff));

            Assert.Equal(new[] { "Staff is too short.", "Staff has left." }, errors.Select(e => e.Message));
            Assert.All(errors, e => Assert.Equal(nameof(Visit.Staff), e.Field));
        }

        [Fact]
        public void ErrorsInOrder_AppendsUnlistedFieldsByName_AndFormLevelLast()
        {
            var state = new ValidationState();
            state.SetError(ValidationState.FormLevel, "Overlaps another visit.");
            state.SetError("Zeta", "z");
            state.SetError("Alpha", "a");
            state.SetError(nameof(Visit.Body), "b");

            var errors = state.ErrorsInOrder(nameof(Visit.Body));

            Assert.Equal(new[] { nameof(Visit.Body), "Alpha", "Zeta", ValidationState.FormLevel }, Fields(errors));
        }

        [Fact]
        public void ErrorsInOrder_FormLevelCanBePlacedExplicitly()
        {
            var state = new ValidationState();
            state.SetError(ValidationState.FormLevel, "Overlaps another visit.");
            state.SetError(nameof(Visit.Body), "b");

            var errors = state.ErrorsInOrder(ValidationState.FormLevel, nameof(Visit.Body));

            Assert.Equal(new[] { ValidationState.FormLevel, nameof(Visit.Body) }, Fields(errors));
        }

        [Fact]
        public void ErrorsInOrder_IgnoresDuplicatesAndFieldsWithoutErrors()
        {
            var state = new ValidationState();
            state.SetError(nameof(Visit.Body), "b");

            var errors = state.ErrorsInOrder(nameof(Visit.Date), nameof(Visit.Body), nameof(Visit.Body), "Unknown");

            Assert.Equal(new[] { (nameof(Visit.Body), "b") }, errors);
        }

        [Fact]
        public void ErrorsInOrder_NoErrors_IsEmpty_AndNoOrderStillReturnsAll()
        {
            var state = new ValidationState();
            Assert.Empty(state.ErrorsInOrder(nameof(Visit.Body)));

            state.SetError(nameof(Visit.Body), "b");
            Assert.Single(state.ErrorsInOrder());
        }

        [Fact]
        public void ErrorsInOrder_IsASnapshot()
        {
            var state = new ValidationState();
            state.SetError(nameof(Visit.Body), "b");
            var errors = state.ErrorsInOrder(nameof(Visit.Body));

            state.ClearError(nameof(Visit.Body));

            Assert.Single(errors);
        }

        [Fact]
        public void ErrorsInOrder_NullOrderOrEntry_Throws()
        {
            var state = new ValidationState();
            Assert.Throws<ArgumentNullException>(() => state.ErrorsInOrder(null));
            Assert.Throws<ArgumentNullException>(() => state.ErrorsInOrder(nameof(Visit.Body), null));
        }

        // ---- TryFocusError ----------------------------------------------------------------------

        private static Form Host(params Control[] controls)
        {
            var form = new Form
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-32000, -32000),
            };
            form.Controls.AddRange(controls);
            form.Show();
            return form;
        }

        [Fact]
        public void TryFocusError_FocusesTheMappedControl()
        {
            var txtDate = new TextBox();
            var txtBody = new TextBox();
            using (var provider = new ErrorProvider())
            using (Host(txtDate, txtBody))
            {
                var display = new ErrorProviderDisplay(provider)
                    .Map(nameof(Visit.Date), txtDate)
                    .Map(nameof(Visit.Body), txtBody);
                txtDate.Focus();

                Assert.True(display.TryFocusError(nameof(Visit.Body)));
                Assert.True(txtBody.Focused);
            }
        }

        [Fact]
        public void TryFocusError_UnmappedOrFormLevel_ReturnsFalse_AndLeavesTheFocus()
        {
            var txtDate = new TextBox();
            var btnSave = new Button();
            using (var provider = new ErrorProvider())
            using (Host(txtDate, btnSave))
            {
                var display = new ErrorProviderDisplay(provider)
                    .Map(nameof(Visit.Date), txtDate)
                    .Map(ValidationState.FormLevel, btnSave);
                txtDate.Focus();

                Assert.False(display.TryFocusError(nameof(Visit.Body)));
                Assert.False(display.TryFocusError(ValidationState.FormLevel));
                Assert.True(txtDate.Focused);
            }
        }

        [Fact]
        public void TryFocusError_SelectsTheTabPageThatHoldsTheControl()
        {
            var tabs = new TabControl();
            var first = new TabPage("基本");
            var second = new TabPage("本文");
            var txtDate = new TextBox();
            var txtBody = new TextBox();
            first.Controls.Add(txtDate);
            second.Controls.Add(txtBody);
            tabs.TabPages.AddRange(new[] { first, second });
            using (var provider = new ErrorProvider())
            using (Host(tabs))
            {
                var display = new ErrorProviderDisplay(provider)
                    .Map(nameof(Visit.Date), txtDate)
                    .Map(nameof(Visit.Body), txtBody);
                Assert.Same(first, tabs.SelectedTab);

                Assert.True(display.TryFocusError(nameof(Visit.Body)));

                Assert.Same(second, tabs.SelectedTab);
                Assert.True(txtBody.Focused);
            }
        }

        [Fact]
        public void TryFocusError_DisabledControl_ReturnsFalse()
        {
            var txtBody = new TextBox { Enabled = false };
            using (var provider = new ErrorProvider())
            using (Host(txtBody))
            {
                var display = new ErrorProviderDisplay(provider).Map(nameof(Visit.Body), txtBody);

                Assert.False(display.TryFocusError(nameof(Visit.Body)));
            }
        }

        [Fact]
        public void TryFocusError_NullField_Throws()
        {
            using (var provider = new ErrorProvider())
            {
                var display = new ErrorProviderDisplay(provider);
                Assert.Throws<ArgumentNullException>(() => display.TryFocusError(null));
            }
        }
    }
}
