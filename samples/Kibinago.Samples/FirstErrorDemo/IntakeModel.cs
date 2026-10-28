using System;
using System.ComponentModel.DataAnnotations;

namespace Kibinago.Samples.FirstErrorDemo
{
    /// <summary>
    /// The intake form's input. The properties are declared in the screen's order: that order is the
    /// order of the errors (FirstError, ErrorsInOrder) — the presenter keeps no list of fields.
    /// </summary>
    public sealed class IntakeModel
    {
        [Required(ErrorMessage = "氏名を入力してください。")]
        public string Name { get; set; }

        [Required(ErrorMessage = "フリガナを入力してください。")]
        [RegularExpression(@"^[ァ-ヶー　 ]+$", ErrorMessage = "フリガナはカタカナで入力してください。")]
        public string Kana { get; set; }

        [Required(ErrorMessage = "生年月日を入力してください。")]
        public DateTime? BirthDate { get; set; }

        [RegularExpression(@"^[0-9-]*$", ErrorMessage = "電話番号は数字とハイフンで入力してください。")]
        public string Phone { get; set; }

        [EmailAddress(ErrorMessage = "メールアドレスの形式が正しくありません。")]
        public string Email { get; set; }
    }
}
