using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    internal sealed class HearthstoneJsonCardDto
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("dbfId")] public int DbfId { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("rarity")] public string Rarity { get; set; }
        [JsonProperty("set")] public string Set { get; set; }
        [JsonProperty("collectible")] public bool Collectible { get; set; }
        [JsonProperty("howToEarn")] public string HowToEarn { get; set; }
        [JsonProperty("howToEarnGolden")] public string HowToEarnGolden { get; set; }
        [JsonProperty("cardClass")] public string CardClass { get; set; }

        // Fields below are used by PatchDiffDetector to find balance-patch changes.
        // Nullable because spells lack attack/health, weapons use durability, hero cards use armor.
        [JsonProperty("cost")] public int? Cost { get; set; }
        [JsonProperty("attack")] public int? Attack { get; set; }
        [JsonProperty("health")] public int? Health { get; set; }
        [JsonProperty("durability")] public int? Durability { get; set; }
        [JsonProperty("armor")] public int? Armor { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
    }
}
