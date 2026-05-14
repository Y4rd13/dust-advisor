using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Ui
{
    public sealed class DustItemRow
    {
        public string CardId { get; }
        public string Name { get; }
        public string Rarity { get; }
        public int RegularToDust { get; }
        public int GoldenToDust { get; }
        public int DustGained { get; }
        public string Flag { get; }

        public DustItemRow(DustItem item)
        {
            CardId = item.CardId;
            Name = item.CardName;
            Rarity = item.Rarity.ToString();
            RegularToDust = item.RegularToDust;
            GoldenToDust = item.GoldenToDust;
            DustGained = item.DustGained;
            Flag = item.InRefundWindow ? "REFUND"
                 : item.IsStandardLegal ? "STANDARD"
                 : "WILD";
        }
    }
}
