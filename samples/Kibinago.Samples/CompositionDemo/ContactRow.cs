namespace Kibinago.Samples.CompositionDemo
{
    /// <summary>One emergency contact. The parent presenter owns the list; each row's presenter edits its own.</summary>
    public sealed class ContactRow
    {
        public string Name { get; set; } = "";
        public string Relation { get; set; } = "";
        public string Phone { get; set; } = "";
    }
}
