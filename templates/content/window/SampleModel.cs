using System.ComponentModel.DataAnnotations;

namespace MvpNamespace
{
    /// <summary>The data the window edits, with its validation rules.</summary>
    public sealed class SampleModel
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100, ErrorMessage = "Title must be at most 100 characters.")]
        public string Title { get; set; }
    }
}
