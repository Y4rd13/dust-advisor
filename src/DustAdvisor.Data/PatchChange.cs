namespace DustAdvisor.Data
{
    public enum PatchChangeDirection
    {
        Neutral,
        Nerf,
        Buff,
    }

    public sealed class PatchChange
    {
        public string CardId { get; }
        public string CardName { get; }
        public string FieldName { get; }
        public string OldValue { get; }
        public string NewValue { get; }
        public PatchChangeDirection Direction { get; }

        public PatchChange(string cardId, string cardName, string fieldName, string oldValue, string newValue, PatchChangeDirection direction)
        {
            CardId = cardId;
            CardName = cardName;
            FieldName = fieldName;
            OldValue = oldValue;
            NewValue = newValue;
            Direction = direction;
        }
    }
}
