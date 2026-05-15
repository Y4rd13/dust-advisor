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
    }
}
