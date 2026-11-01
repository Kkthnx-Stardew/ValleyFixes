namespace ValleyFixes.Fixes
{
    /// <summary>The pure rule behind VF-012, kept free of game types so tests can compile it.</summary>
    public static class NameValidation
    {
        /// <summary>
        /// Adds a blank-text check on top of the game's own answer. The game checks the typed text by length, so a single space passes,
        /// then trims the player name to nothing and saves a farm name that reads back empty. This can only turn a "yes" into a "no".
        /// </summary>
        public static bool Allows(bool gameAllows, bool checkFarmName, string? playerName, string? farmName)
        {
            if (!gameAllows)
                return false;
            if (string.IsNullOrWhiteSpace(playerName))
                return false;
            return !checkFarmName || !string.IsNullOrWhiteSpace(farmName);
        }
    }
}
