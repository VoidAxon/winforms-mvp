using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using WinformsMVP.Common.Validation.Attributes;
using WinformsMVP.Common.Validation.Core;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    /// <summary>
    /// The screen's order of errors comes from the model checked last: its properties in declaration
    /// order, moved by [ValidationOrder] — no field list in the presenter.
    /// </summary>
    public class ValidationModelOrderTests
    {
        // Declared in screen order; alphabetical order would be Body, Date, Staff.
        private sealed class Visit
        {
            [Required(ErrorMessage = "Date is required.")]
            public string Date { get; set; }

            [Required(ErrorMessage = "Staff is required.")]
            public string Staff { get; set; }

            [Required(ErrorMessage = "Body is required.")]
            public string Body { get; set; }
        }

        private sealed class VisitDateLast
        {
            [ValidationOrder(1)]
            [Required(ErrorMessage = "Date is required.")]
            public string Date { get; set; }

            [Required(ErrorMessage = "Staff is required.")]
            public string Staff { get; set; }

            [Required(ErrorMessage = "Body is required.")]
            public string Body { get; set; }
        }

        // No annotations: every rule is the presenter's (SetError).
        private sealed class PlainInput
        {
            public string Date { get; set; }
            public string Body { get; set; }
        }

        private class BaseInput
        {
            [Required(ErrorMessage = "Name is required.")]
            public string Name { get; set; }
        }

        private sealed class DerivedInput : BaseInput
        {
            [Required(ErrorMessage = "Code is required.")]
            public string Code { get; set; }
        }

        // Declared Zed before Alpha: alphabetical order would put Alpha first.
        private sealed class DerivedOutOfAlphabet : BaseInput
        {
            [Required(ErrorMessage = "Zed is required.")]
            public string Zed { get; set; }

            [Required(ErrorMessage = "Alpha is required.")]
            public string Alpha { get; set; }
        }

        private static IEnumerable<string> Fields(IReadOnlyList<(string Field, string Message)> errors) => errors.Select(e => e.Field);

        [Fact]
        public void ErrorsInOrder_FollowsTheModelsDeclarationOrder()
        {
            var state = new ValidationState();
            Assert.False(state.Check(new Visit()));
            Assert.Equal(new[] { "Date", "Staff", "Body" }, Fields(state.ErrorsInOrder()));
        }

        [Fact]
        public void ValidationOrder_MovesAPropertyAfterTheUnnumberedOnes()
        {
            var state = new ValidationState();
            state.Check(new VisitDateLast());
            Assert.Equal(new[] { "Staff", "Body", "Date" }, Fields(state.ErrorsInOrder()));
        }

        [Fact]
        public void PresenterRules_FollowTheModelOrder_UnknownFieldsByName_FormLevelLast()
        {
            var state = new ValidationState();
            state.Check(new PlainInput());   // no annotations: only remembers the model
            state.SetError(ValidationState.FormLevel, "Overlaps another visit.");
            state.SetError("Zeta", "z");
            state.SetError(nameof(PlainInput.Body), "b");
            state.SetError(nameof(PlainInput.Date), "d");

            Assert.Equal(new[] { "Date", "Body", "Zeta", ValidationState.FormLevel }, Fields(state.ErrorsInOrder()));
        }

        [Fact]
        public void BaseClassProperties_ComeFirst()
        {
            var state = new ValidationState();
            state.Check(new DerivedInput());
            Assert.Equal(new[] { "Name", "Code" }, Fields(state.ErrorsInOrder()));
        }

        [Fact]
        public void ADerivedModel_PassedAsItsBase_IsOrderedByItsRuntimeType()
        {
            var state = new ValidationState();
            BaseInput input = new DerivedOutOfAlphabet();
            state.Check(input);   // T = BaseInput; the derived properties are validated too
            Assert.Equal(new[] { "Name", "Zed", "Alpha" }, Fields(state.ErrorsInOrder()));
        }

        [Fact]
        public void FirstError_IsTheFirstInTheModelsOrder()
        {
            var state = new ValidationState();
            state.Check(new Visit { Date = "2026-10-22" });
            Assert.Equal(("Staff", "Staff is required."), state.FirstError);
        }

        [Fact]
        public void FirstError_WithoutErrors_Throws()
        {
            var state = new ValidationState();
            Assert.True(state.Check(new Visit { Date = "d", Staff = "s", Body = "b" }));
            Assert.Throws<InvalidOperationException>(() => state.FirstError);
        }

        [Fact]
        public void AnExplicitOrder_StillComesFirst_TheRestFollowTheModel()
        {
            var state = new ValidationState();
            state.Check(new Visit());
            Assert.Equal(new[] { "Body", "Date", "Staff" }, Fields(state.ErrorsInOrder(nameof(Visit.Body))));
        }

        [Fact]
        public void BeforeAnyModelIsChecked_FieldsAreOrderedByName()
        {
            var state = new ValidationState();
            state.SetError("Staff", "s");
            state.SetError("Body", "b");
            Assert.Equal(new[] { "Body", "Staff" }, Fields(state.ErrorsInOrder()));
        }
    }
}
