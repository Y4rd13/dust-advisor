using System;
using DustAdvisor.Algorithm.Domain;

namespace DustAdvisor.Algorithm
{
    public static class Constants
    {
        public static int DisenchantRegular(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return 5;
                case Rarity.Rare: return 20;
                case Rarity.Epic: return 100;
                case Rarity.Legendary: return 400;
                default: throw new ArgumentOutOfRangeException(nameof(r), r, "Free/unknown rarity has no DE value");
            }
        }

        public static int DisenchantGolden(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return 50;
                case Rarity.Rare: return 100;
                case Rarity.Epic: return 400;
                case Rarity.Legendary: return 1600;
                default: throw new ArgumentOutOfRangeException(nameof(r), r, "Free/unknown rarity has no DE value");
            }
        }

        public static int CraftCost(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return 40;
                case Rarity.Rare: return 100;
                case Rarity.Epic: return 400;
                case Rarity.Legendary: return 1600;
                default: throw new ArgumentOutOfRangeException(nameof(r), r, "Free/unknown rarity has no craft cost");
            }
        }

        public static int PlaysetSize(Rarity r)
        {
            return r == Rarity.Legendary ? 1 : 2;
        }
    }
}
