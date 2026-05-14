using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DustAdvisor.Algorithm.Domain;
using Newtonsoft.Json;

namespace DustAdvisor.Data
{
    public sealed class HearthstoneJsonClient : IHearthstoneJsonClient
    {
        private const string BaseUrl = "https://api.hearthstonejson.com/v1/latest";
        private readonly IHttpFetcher _fetcher;

        public HearthstoneJsonClient(IHttpFetcher fetcher)
        {
            _fetcher = fetcher;
        }

        public async Task<IReadOnlyList<CardMeta>> LoadCollectibleAsync(string locale, CancellationToken ct)
        {
            var url = $"{BaseUrl}/{locale}/cards.collectible.json";
            var json = await _fetcher.GetAsync(url, ct).ConfigureAwait(false);
            var dtos = JsonConvert.DeserializeObject<List<HearthstoneJsonCardDto>>(json) ?? new List<HearthstoneJsonCardDto>();
            var result = new List<CardMeta>(dtos.Count);
            foreach (var d in dtos)
            {
                if (!d.Collectible) continue;
                var rarity = ParseRarity(d.Rarity);
                if (rarity == null) continue;
                var set = new CardSet(d.Set ?? "UNKNOWN", StandardSets.Codes.Contains(d.Set ?? string.Empty));
                result.Add(new CardMeta(d.Id, d.DbfId, d.Name ?? d.Id, rarity.Value, set, isCollectible: true));
            }
            return result;
        }

        private static Rarity? ParseRarity(string s)
        {
            switch (s)
            {
                case "FREE": return Rarity.Free;
                case "COMMON": return Rarity.Common;
                case "RARE": return Rarity.Rare;
                case "EPIC": return Rarity.Epic;
                case "LEGENDARY": return Rarity.Legendary;
                default: return null;
            }
        }
    }
}
