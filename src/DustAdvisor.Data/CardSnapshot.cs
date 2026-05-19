namespace DustAdvisor.Data
{
    /// <summary>
    /// Minimal projection of a Hearthstone card's mutable balance-relevant fields.
    /// Decouples the JSON DTO (internal) from the patch-diff logic (public for testability).
    /// </summary>
    public sealed class CardSnapshot
    {
        public string Id { get; }
        public string Name { get; }
        public int? Cost { get; }
        public int? Attack { get; }
        public int? Health { get; }
        public int? Durability { get; }
        public int? Armor { get; }
        public string Text { get; }

        public CardSnapshot(string id, string name, int? cost, int? attack, int? health, int? durability, int? armor, string text)
        {
            Id = id;
            Name = name;
            Cost = cost;
            Attack = attack;
            Health = health;
            Durability = durability;
            Armor = armor;
            Text = text;
        }
    }
}
