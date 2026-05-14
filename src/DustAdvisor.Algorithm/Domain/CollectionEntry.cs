namespace DustAdvisor.Algorithm.Domain
{
    public sealed class CollectionEntry
    {
        public string CardId { get; }
        public int Regular { get; }
        public int Golden { get; }
        public int Signature { get; }
        public int Diamond { get; }

        public CollectionEntry(string cardId, int regular = 0, int golden = 0, int signature = 0, int diamond = 0)
        {
            CardId = cardId;
            Regular = regular;
            Golden = golden;
            Signature = signature;
            Diamond = diamond;
        }
    }
}
