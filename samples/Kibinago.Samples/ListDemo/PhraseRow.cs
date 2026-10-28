using System.Collections.Generic;
using System.Linq;

namespace Kibinago.Samples.ListDemo
{
    /// <summary>A stock phrase with its display order (1, 2, 3 …), as stored.</summary>
    public sealed class PhraseRow
    {
        public int Id { get; set; }
        public int DisplayOrder { get; set; }
        public string Text { get; set; }
    }

    /// <summary>Stands in for the database: keeps the display orders and records each save.</summary>
    public sealed class PhraseRepository
    {
        private readonly Dictionary<int, PhraseRow> _rows = new[]
        {
            "お大事にしてください。", "次回は 2 週間後です。", "お薬は食後に飲んでください。",
            "検査結果は次回お伝えします。", "痛みが続くときは受診してください。", "水分を多めにとってください。",
        }.Select((text, i) => new PhraseRow { Id = i + 1, DisplayOrder = i + 1, Text = text }).ToDictionary(r => r.Id);

        public IReadOnlyList<PhraseRow> List() =>
            _rows.Values.OrderBy(r => r.DisplayOrder)
                .Select(r => new PhraseRow { Id = r.Id, DisplayOrder = r.DisplayOrder, Text = r.Text }).ToList();

        /// <summary>Saves the changed orders in one go (one transaction in a real repository).</summary>
        public void SaveOrders(IEnumerable<(int Id, int DisplayOrder)> orders)
        {
            foreach (var (id, order) in orders)
                _rows[id].DisplayOrder = order;
        }
    }
}
