using System;
using System.ComponentModel;
using System.Text;
using WinformsMVP.Core.Models;

namespace WinformsMVP.Samples.MVPComparisonDemo.SupervisingController
{
    /// <summary>
    /// User model for Supervising Controller pattern.
    ///
    /// In Supervising Controller:
    /// - Model inherits from BindableBase (implements INotifyPropertyChanged)
    /// - Model contains business data and simple validation
    /// - View can bind directly to the model
    /// - Presenter focuses on complex logic and coordination
    ///
    /// This reduces boilerplate code for simple property synchronization.
    /// </summary>
    public class UserModel : BindableBase, ICloneable
    {
        private string _name;
        private string _email;
        private int _age;
        private bool _isActive;
        private string _validationErrors;

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    // Automatically validate when property changes
                    ValidateAndNotify();
                }
            }
        }

        public string Email
        {
            get => _email;
            set
            {
                if (SetProperty(ref _email, value))
                {
                    ValidateAndNotify();
                }
            }
        }

        public int Age
        {
            get => _age;
            set
            {
                if (SetProperty(ref _age, value))
                {
                    ValidateAndNotify();
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public string ValidationErrors
        {
            get => _validationErrors;
            private set => SetProperty(ref _validationErrors, value);
        }

        /// <summary>
        /// In Supervising Controller, Model can contain simple validation logic
        /// </summary>
        public bool IsValid => string.IsNullOrEmpty(ValidationErrors);

        private void ValidateAndNotify()
        {
            var errors = new StringBuilder();

            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.AppendLine("• Name is required");
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                errors.AppendLine("• Email is required");
            }
            else if (!Email.Contains("@"))
            {
                errors.AppendLine("• Email must contain @");
            }

            if (Age < 18 || Age > 120)
            {
                errors.AppendLine("• Age must be between 18 and 120");
            }

            ValidationErrors = errors.ToString().Trim();
        }

        /// <summary>
        /// Creates a deep copy of this UserModel instance.
        /// </summary>
        /// <returns>A new UserModel instance with all properties independently copied.</returns>
        /// <remarks>
        /// <para>
        /// This method implements a <strong>deep copy</strong>.
        /// All property values are copied to the new instance and do not share references with the original object.
        /// </para>
        /// <para>
        /// <strong>Note:</strong> If this model has nested objects (e.g., Address, List, etc.),
        /// they must also be deeply copied:
        /// <code>
        /// Address = this.Address?.Clone() as Address,
        /// Tags = this.Tags != null ? new List&lt;string&gt;(this.Tags) : null
        /// </code>
        /// </para>
        /// <para>
        /// Do not use MemberwiseClone(). It creates a shallow copy and will cause
        /// issues when used with ChangeTracker&lt;T&gt;.
        /// </para>
        /// </remarks>
        public object Clone()
        {
            return new UserModel
            {
                Name = this.Name,
                Email = this.Email,
                Age = this.Age,
                IsActive = this.IsActive
                // ValidationErrors is automatically recalculated, so no need to copy
            };
        }

        /// <summary>
        /// Restore values from another model
        /// </summary>
        public void CopyFrom(UserModel other)
        {
            Name = other.Name;
            Email = other.Email;
            Age = other.Age;
            IsActive = other.IsActive;
        }
    }
}
