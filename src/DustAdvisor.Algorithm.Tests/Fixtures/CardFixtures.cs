using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Algorithm.Tests.Fixtures
{
    internal static class CardFixtures
    {
        private static readonly CardSet WildExpansion = new CardSet("OG", isStandardLegal: false);
        private static readonly CardSet StandardExpansion = new CardSet("BADLANDS", isStandardLegal: true);
        private static readonly CardSet Core = new CardSet("CORE", isStandardLegal: true);

        public static CardMeta CommonWild(string id, int dbfId, string name = "Common Card")
            => new CardMeta(id, dbfId, name, Rarity.Common, WildExpansion, isCollectible: true);

        public static CardMeta RareStandard(string id, int dbfId, string name = "Rare Card")
            => new CardMeta(id, dbfId, name, Rarity.Rare, StandardExpansion, isCollectible: true);

        public static CardMeta LegendaryWild(string id, int dbfId, string name = "Legendary Card")
            => new CardMeta(id, dbfId, name, Rarity.Legendary, WildExpansion, isCollectible: true);

        public static CardMeta CoreCard(string id, int dbfId, string name = "Core Card")
            => new CardMeta(id, dbfId, name, Rarity.Common, Core, isCollectible: true);
    }
}
