using System;
using System.Collections.Generic;

namespace Kibinago.Samples.ChangeTrackingDemo
{
    /// <summary>The record edited on the screen. Allergies is a list: the tracker compares it by content.</summary>
    public sealed class PatientProfile
    {
        public string Name { get; set; }
        public string BloodType { get; set; }
        public List<string> Allergies { get; set; } = new List<string>();
        public string Memo { get; set; }

        /// <summary>Set on every edit; not a change the user should be asked to save (ignored by the tracker).</summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Stands in for the database.</summary>
    public sealed class PatientProfileRepository
    {
        private PatientProfile _stored = new PatientProfile
        {
            Name = "佐藤 一郎",
            BloodType = "A",
            Allergies = new List<string> { "ペニシリン" },
            Memo = "",
            UpdatedAt = DateTime.Now,
        };

        public PatientProfile Load() => Copy(_stored);

        public void Save(PatientProfile profile) => _stored = Copy(profile);

        private static PatientProfile Copy(PatientProfile p) => new PatientProfile
        {
            Name = p.Name, BloodType = p.BloodType, Allergies = new List<string>(p.Allergies), Memo = p.Memo, UpdatedAt = p.UpdatedAt,
        };
    }
}
