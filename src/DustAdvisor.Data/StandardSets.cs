using System.Collections.Generic;

namespace DustAdvisor.Data
{
    /// Year of the Scarab (2026). Maintain by hand on rotation.
    public static class StandardSets
    {
        public static readonly HashSet<string> Codes = new HashSet<string>
        {
            "CORE",
            "ISLAND_VACATION",
            "EMERALD_DREAM",
            "GREAT_DARK_BEYOND",
            "HEROES_OF_STARCRAFT",
            "BADLANDS",
            "WHIZBANGS_WORKSHOP",
        };
    }
}
