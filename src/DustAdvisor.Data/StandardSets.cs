using System.Collections.Generic;

namespace DustAdvisor.Data
{
    /// Year of the Scarab (2026). Maintain by hand on rotation.
    /// Verified 2026-05-13 against https://api.hearthstonejson.com/v1/latest/enUS/cards.collectible.json
    ///
    /// Set codes in the HearthstoneJSON API do not always match marketing names:
    ///   CORE             - Core Set 2026 (always Standard, refreshed each year)
    ///   EMERALD_DREAM    - Awakening of the Old Gods / Emerald Dream (Year of the Pegasus, Apr 2025) — EDR_ card prefix
    ///   THE_LOST_CITY    - The Lost City (Year of the Scarab, ~Aug 2025) — TLC_ / DINO_ card prefixes
    ///   TIME_TRAVEL      - Time Travelers (Year of the Scarab, ~Nov 2025) — TIME_ / END_ card prefixes; includes mini-set
    ///
    /// Removed from previous list (now Wild or wrong codes):
    ///   ISLAND_VACATION      - Perils in Paradise (Year of the Pegasus, rotated Apr 2026)
    ///   WHIZBANGS_WORKSHOP   - Whizbang's Workshop (Year of the Pegasus, rotated Apr 2026)
    ///   GREAT_DARK_BEYOND    - Wrong code (0 cards); real code is SPACE (GDB_+SC_ prefix), also Wild
    ///   HEROES_OF_STARCRAFT  - Wrong code (0 cards); StarCraft mini-set is part of SPACE set, also Wild
    ///   BADLANDS             - Wrong code (0 cards); real code is WILD_WEST (Showdown in the Badlands, 2023)
    public static class StandardSets
    {
        public static readonly HashSet<string> Codes = new HashSet<string>
        {
            "CORE",          // Core Set 2026
            "EMERALD_DREAM", // Emerald Dream expansion (Year of the Pegasus, Apr 2025)
            "THE_LOST_CITY", // The Lost City expansion + mini-set (Year of the Scarab, ~Aug 2025)
            "TIME_TRAVEL",   // Time Travelers expansion + mini-set (Year of the Scarab, ~Nov 2025)
        };

        /// Sets currently in Standard but rotating out at the next year-flip.
        /// Empty for most of the year; the user fills this when the rotation is imminent
        /// (typically Jan-Mar each year). RotationImminent strategy uses this list.
        public static readonly HashSet<string> RotatingNextYear = new HashSet<string>
        {
        };
    }
}
