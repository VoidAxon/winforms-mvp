using System;
using System.Collections.Generic;
using Kibinago.Common;

namespace Kibinago.Samples.ListDemo
{
    /// <summary>
    /// A row of the patient list. NotifyingObject, so an edit in the grid is reported to the list
    /// (KeepSorted moves the row to its place).
    /// </summary>
    public sealed class PatientRow : NotifyingObject
    {
        private string _ward;
        private int _no;
        private string _name;
        private DateTime _admittedOn;
        private string _status;

        public string Ward { get => _ward; set => SetProperty(ref _ward, value); }
        public int No { get => _no; set => SetProperty(ref _no, value); }
        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public DateTime AdmittedOn { get => _admittedOn; set => SetProperty(ref _admittedOn, value); }
        public string Status { get => _status; set => SetProperty(ref _status, value); }

        /// <summary>Sample data: three wards, a few patients each.</summary>
        public static IReadOnlyList<PatientRow> Sample()
        {
            var today = DateTime.Today;
            PatientRow Row(string ward, int no, string name, int daysAgo, string status) =>
                new PatientRow { Ward = ward, No = no, Name = name, AdmittedOn = today.AddDays(-daysAgo), Status = status };
            return new[]
            {
                Row("3 東", 12, "佐藤 一郎", 3, "入院中"),
                Row("5 西", 3, "鈴木 花子", 10, "退院予定"),
                Row("3 東", 2, "高橋 健", 1, "入院中"),
                Row("4 南", 7, "田中 美咲", 5, "入院中"),
                Row("5 西", 11, "伊藤 誠", 2, "入院中"),
                Row("3 東", 5, "渡辺 陽子", 14, "退院予定"),
                Row("4 南", 1, "山本 大輔", 7, "入院中"),
                Row("5 西", 8, "中村 さくら", 1, "入院中"),
                Row("4 南", 9, "小林 翔", 3, "退院予定"),
                Row("3 東", 8, "加藤 真由美", 6, "入院中"),
            };
        }
    }
}
