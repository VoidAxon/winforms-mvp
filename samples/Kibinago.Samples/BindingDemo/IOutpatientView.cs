using System.Collections.Generic;
using Kibinago.Views;

namespace Kibinago.Samples.BindingDemo
{
    public interface IOutpatientView : IActionableView
    {
        /// <summary>Binds the controls to the model (two-way), with the departments for the combo.</summary>
        void Bind(OutpatientInput input, IReadOnlyList<DepartmentItem> departments);

        /// <summary>For the demo: the model's values as the presenter sees them.</summary>
        void ShowModel(string text);
    }
}
