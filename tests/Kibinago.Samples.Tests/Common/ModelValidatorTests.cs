using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Kibinago.Common.Validation.Core;
using Xunit;

namespace Kibinago.Samples.Tests.Common
{
    public class ModelValidatorTests
    {
        private class Person
        {
            [Required]
            [StringLength(10)]
            public string Name { get; set; }
        }

        [Fact]
        public void Result_KeepsTheMemberNamesItWasCreatedWith()
        {
            var names = new List<string> { "Name" };
            var result = new ModelValidationResult("Required.", names, 0);

            names[0] = "Email";

            Assert.Equal(new[] { "Name" }, result.MemberNames);
        }

        [Fact]
        public void Results_WithTheSameMessageOnDifferentFields_AreDifferentErrors()
        {
            var name = new ModelValidationResult("Required.", new[] { "Name" }, 0);
            var email = new ModelValidationResult("Required.", new[] { "Email" }, 0);

            Assert.Equal(2, new[] { name, email }.Distinct().Count());
        }

        [Fact]
        public void For_ReturnsTypedValidator()
        {
            IModelValidator<Person> validator = ModelValidator.For<Person>();
            Assert.NotNull(validator);
        }

        [Fact]
        public void ValidateAll_InvalidModel_ReturnsErrors()
        {
            var validator = ModelValidator.For<Person>();

            var errors = validator.ValidateAll(new Person { Name = "" });

            Assert.NotEmpty(errors);
            Assert.All(errors, e => Assert.IsType<ModelValidationResult>(e));
        }

        [Fact]
        public void ValidateAll_ValidModel_ReturnsNoErrors()
        {
            var validator = ModelValidator.For<Person>();

            var errors = validator.ValidateAll(new Person { Name = "Alice" });

            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateSequential_InvalidModel_ReturnsFirstError()
        {
            var validator = ModelValidator.For<Person>();

            var result = validator.ValidateSequential(new Person { Name = "" });

            Assert.False(result.IsValid);
            Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        }

        [Fact]
        public void ValidateSequential_ValidModel_ReturnsSuccess()
        {
            var validator = ModelValidator.For<Person>();

            var result = validator.ValidateSequential(new Person { Name = "Alice" });

            Assert.True(result.IsValid);
        }

        [Fact]
        public void IsValid_ReflectsModelState()
        {
            var validator = ModelValidator.For<Person>();

            Assert.True(validator.IsValid(new Person { Name = "Alice" }));
            Assert.False(validator.IsValid(new Person { Name = "" }));
        }
    }
}
