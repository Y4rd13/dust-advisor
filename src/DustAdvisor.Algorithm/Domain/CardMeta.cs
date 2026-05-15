namespace DustAdvisor.Algorithm.Domain
{
    public sealed class CardMeta
    {
        public string CardId { get; }
        public int DbfId { get; }
        public string Name { get; }
        public Rarity Rarity { get; }
        public CardSet Set { get; }
        public bool IsCollectible { get; }
        public string Class { get; }

        public CardMeta(string cardId, int dbfId, string name, Rarity rarity, CardSet set, bool isCollectible, string @class = "NEUTRAL")
        {
            CardId = cardId;
            DbfId = dbfId;
            Name = name;
            Rarity = rarity;
            Set = set;
            IsCollectible = isCollectible;
            Class = @class ?? "NEUTRAL";
        }
    }
}
