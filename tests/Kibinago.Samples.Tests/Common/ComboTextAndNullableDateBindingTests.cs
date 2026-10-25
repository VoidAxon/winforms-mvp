using System;
using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.Common.Extensions;
using Xunit;

namespace Kibinago.Samples.Tests.Common
{
    /// <summary>
    /// ComboBox.BindText (free text of an editable combo) and the nullable-date DateTimePicker.Bind
    /// (checkbox = has a value).
    /// </summary>
    public class ComboTextAndNullableDateBindingTests
    {
        private sealed class Visit : INotifyPropertyChanged
        {
            private string _place;
            private DateTime? _due;
            private DateTime _date = new DateTime(2026, 1, 1);

            public event PropertyChangedEventHandler PropertyChanged;
            public int HandlerCount => PropertyChanged?.GetInvocationList().Length ?? 0;
            public int DueWrites;

            public string Place { get => _place; set { _place = value; Raise(nameof(Place)); } }
            public DateTime? Due { get => _due; set { _due = value; DueWrites++; Raise(nameof(Due)); } }
            public DateTime Date { get => _date; set { _date = value; Raise(nameof(Date)); } }
            public DateTime? ReadOnlyDue => _due;

            /// <summary>Changes the value and raises PropertyChanged with an empty name ("everything changed").</summary>
            public void ReplaceAll(DateTime? due)
            {
                _due = due;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            }

            private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>Simulates the user ticking the checkbox / picking a date: the native control raises ValueChanged.</summary>
        private sealed class UserPicker : DateTimePicker
        {
            public UserPicker() { ShowCheckBox = true; }

            public void UserSets(bool isChecked, DateTime? value = null)
            {
                if (value.HasValue) Value = value.Value;
                Checked = isChecked;
                OnValueChanged(EventArgs.Empty);
            }

            public void Recreate() => RecreateHandle();
        }

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

        private static readonly DateTime D1 = new DateTime(2026, 10, 1);
        private static readonly DateTime D2 = new DateTime(2026, 12, 24);

        // ---- ComboBox.BindText ------------------------------------------------------------------

        [Fact]
        public void ComboText_FreeTextFlowsBothWays()
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            combo.Items.AddRange(new object[] { "東京", "大阪" });
            using (Host(combo))
            {
                var visit = new Visit { Place = "大阪" };
                combo.BindText(visit, m => m.Place);
                Assert.Equal("大阪", combo.Text);

                combo.Text = "札幌（一覧にない）";
                Assert.Equal("札幌（一覧にない）", visit.Place);

                visit.Place = "東京";
                Assert.Equal("東京", combo.Text);
            }
        }

        [Fact]
        public void ComboText_ReturnsATextBinding_WrittenOnEveryChange()
        {
            using (var combo = new ComboBox())
            {
                var binding = combo.BindText(new Visit(), m => m.Place);

                Assert.Same(binding, combo.DataBindings[nameof(ComboBox.Text)]);
                Assert.Equal(DataSourceUpdateMode.OnPropertyChanged, binding.DataSourceUpdateMode);
                Assert.True(binding.FormattingEnabled);
            }
        }

        [Fact]
        public void ComboText_BindingAgain_Replaces()
        {
            using (var combo = new ComboBox())
            {
                combo.BindText(new Visit(), m => m.Place);
                var second = combo.BindText(new Visit(), m => m.Place);

                Assert.Equal(1, combo.DataBindings.Count);
                Assert.Same(second, combo.DataBindings[0]);
            }
        }

        // ---- DateTimePicker nullable date -------------------------------------------------------

        [Fact]
        public void NullableDate_InitialNull_UnchecksAndKeepsTheDisplayedDate()
        {
            var picker = new UserPicker { Value = D1 };
            using (Host(picker))
            {
                var visit = new Visit();
                picker.Bind(visit, m => m.Due);

                Assert.False(picker.Checked);
                Assert.Equal(D1, picker.Value);
                Assert.Null(visit.Due);
                Assert.Equal(0, visit.DueWrites);
            }
        }

        [Fact]
        public void NullableDate_InitialDate_SetsValueAndChecks()
        {
            var picker = new UserPicker();
            using (Host(picker))
            {
                var visit = new Visit { Due = D1 };
                visit.DueWrites = 0;
                picker.Bind(visit, m => m.Due);

                Assert.True(picker.Checked);
                Assert.Equal(D1, picker.Value);
                Assert.Equal(0, visit.DueWrites);
            }
        }

        [Fact]
        public void NullableDate_ModelChanges_ReachThePicker_WithoutWritingBack()
        {
            var picker = new UserPicker();
            using (Host(picker))
            {
                var visit = new Visit();
                picker.Bind(visit, m => m.Due);

                visit.Due = D1;
                Assert.True(picker.Checked);
                Assert.Equal(D1, picker.Value);

                visit.Due = null;
                Assert.False(picker.Checked);
                Assert.Equal(D1, picker.Value);

                visit.Due = D2;
                Assert.True(picker.Checked);
                Assert.Equal(D2, picker.Value);

                Assert.Equal(3, visit.DueWrites);   // only the three model assignments above
            }
        }

        [Fact]
        public void NullableDate_EmptyPropertyName_Resyncs()
        {
            var picker = new UserPicker();
            using (Host(picker))
            {
                var visit = new Visit();
                picker.Bind(visit, m => m.Due);

                visit.ReplaceAll(D2);

                Assert.True(picker.Checked);
                Assert.Equal(D2, picker.Value);
            }
        }

        [Fact]
        public void NullableDate_UserEdits_ReachTheModel()
        {
            var picker = new UserPicker();
            using (Host(picker))
            {
                var visit = new Visit { Due = D1 };
                picker.Bind(visit, m => m.Due);

                picker.UserSets(isChecked: false);
                Assert.Null(visit.Due);

                picker.UserSets(isChecked: true);
                Assert.Equal(D1, visit.Due);

                picker.UserSets(isChecked: true, value: D2);
                Assert.Equal(D2, visit.Due);
            }
        }

        [Fact]
        public void NullableDate_WithoutShowCheckBox_Throws_WithoutAttaching()
        {
            var picker = new DateTimePicker();
            using (Host(picker))
            {
                var visit = new Visit();
                var ex = Assert.Throws<InvalidOperationException>(() => picker.Bind(visit, m => m.Due));

                Assert.Contains(nameof(DateTimePicker.ShowCheckBox), ex.Message);
                Assert.Equal(0, visit.HandlerCount);
            }
        }

        [Fact]
        public void NullableDate_OutOfRange_IsRejected_WithoutChangingThePicker()
        {
            var picker = new UserPicker { MinDate = new DateTime(2000, 1, 1), Value = D1 };
            using (Host(picker))
            {
                var early = new Visit { Due = new DateTime(1999, 12, 31) };
                Assert.Throws<ArgumentOutOfRangeException>(() => picker.Bind(early, m => m.Due));
                Assert.Equal(0, early.HandlerCount);
                Assert.Equal(D1, picker.Value);

                var visit = new Visit { Due = D2 };
                picker.Bind(visit, m => m.Due);
                Assert.Throws<ArgumentOutOfRangeException>(() => visit.Due = new DateTime(1999, 12, 31));
                Assert.Equal(D2, picker.Value);   // never silently clamped
                Assert.True(picker.Checked);
            }
        }

        [Fact]
        public void NullableDate_ReadOnlyOrPathOrNull_AreRejected()
        {
            using (var picker = new UserPicker())
            {
                var visit = new Visit();
                Assert.Throws<ArgumentException>(() => picker.Bind(visit, m => m.ReadOnlyDue));
                Assert.Throws<ArgumentNullException>(() => picker.Bind<Visit>(null, m => m.Due));
                Assert.Throws<ArgumentNullException>(() => ((DateTimePicker)null).Bind(visit, m => m.Due));
                Assert.Equal(0, visit.HandlerCount);
            }
        }

        [Fact]
        public void NullableDate_BindingAgain_ReplacesThePreviousBinding()
        {
            var picker = new UserPicker();
            using (Host(picker))
            {
                var first = new Visit();
                var second = new Visit { Due = D2 };
                picker.Bind(first, m => m.Due);
                picker.Bind(second, m => m.Due);

                Assert.Equal(0, first.HandlerCount);
                Assert.Equal(1, second.HandlerCount);

                first.Due = D1;   // the old model no longer drives the picker
                Assert.Equal(D2, picker.Value);
            }
        }

        [Fact]
        public void NullableDate_ReplacesNativeValueAndCheckedBindings_KeepsOthers()
        {
            var picker = new UserPicker();
            using (Host(picker))
            {
                var visit = new Visit();
                picker.Bind(visit, m => m.Date);   // native Value binding
                picker.DataBindings.Add(nameof(DateTimePicker.Enabled), new EnabledSource(), nameof(EnabledSource.On));

                picker.Bind(visit, m => m.Due);

                Assert.Null(picker.DataBindings[nameof(DateTimePicker.Value)]);
                Assert.NotNull(picker.DataBindings[nameof(DateTimePicker.Enabled)]);
            }
        }

        private sealed class EnabledSource { public bool On { get; set; } = true; }

        [Fact]
        public void PlainDateBind_ReplacesANullableDateBinding()
        {
            var picker = new UserPicker();
            using (Host(picker))
            {
                var visit = new Visit();
                picker.Bind(visit, m => m.Due);

                picker.Bind(visit, m => m.Date);   // DateTime property: the existing overload

                Assert.NotNull(picker.DataBindings[nameof(DateTimePicker.Value)]);
                visit.Due = D2;                    // the nullable binding no longer drives the picker
                Assert.Equal(visit.Date, picker.Value);
            }
        }

        [Fact]
        public void NullableDate_DisposeHandle_Unbinds_AndDisposingThePickerReleasesTheModel()
        {
            var picker = new UserPicker();
            var visit = new Visit();
            var form = Host(picker);
            var handle = picker.Bind(visit, m => m.Due);
            handle.Dispose();
            handle.Dispose();
            Assert.Equal(0, visit.HandlerCount);

            picker.Bind(visit, m => m.Due);
            Assert.Equal(1, visit.HandlerCount);
            form.Dispose();
            Assert.Equal(0, visit.HandlerCount);
            visit.Due = D1;   // no longer touches the disposed picker
        }

        [Fact]
        public void NullableDate_SurvivesHandleRecreation()
        {
            var picker = new UserPicker { Value = D1 };
            using (Host(picker))
            {
                var visit = new Visit();
                picker.Bind(visit, m => m.Due);

                picker.Recreate();

                Assert.False(picker.Checked);
                Assert.Null(visit.Due);

                visit.Due = D2;
                Assert.True(picker.Checked);
                Assert.Equal(D2, picker.Value);
            }
        }
    }
}
