namespace DustAdvisor.Algorithm.Domain
{
    public sealed class DustItem
    {
        public string CardId { get; }
        public string CardName { get; }
        public Rarity Rarity { get; }
        public int RegularToDust { get; }
        public int GoldenToDust { get; }
        public int DustGained { get; }
        public bool InRefundWindow { get; }
        public bool IsStandardLegal { get; }
        public int InDeckCount { get; }
        public string Class { get; }
        public string MetaTier { get; }

        public DustItem(string cardId, string cardName, Rarity rarity,
                        int regularToDust, int goldenToDust, int dustGained,
                        bool inRefundWindow, bool isStandardLegal, int inDeckCount = 0, string @class = "NEUTRAL", string metaTier = "?")
        {
            CardId = cardId;
            CardName = cardName;
            Rarity = rarity;
            RegularToDust = regularToDust;
            GoldenToDust = goldenToDust;
            DustGained = dustGained;
            InRefundWindow = inRefundWindow;
            IsStandardLegal = isStandardLegal;
            InDeckCount = inDeckCount;
            Class = @class ?? "NEUTRAL";
            MetaTier = string.IsNullOrEmpty(metaTier) ? "?" : metaTier;
        }
    }
}
