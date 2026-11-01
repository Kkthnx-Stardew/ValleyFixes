namespace ValleyFixes.Fixes
{
    /// <summary>The pure pick rule behind VF-022, kept free of game types so tests can compile it.</summary>
    public static class SeasonalPick
    {
        // Sardine (131) cannot be caught in summer, and the 1.4 and 1.6.4 patch notes removed it from the summer quest pools.
        // The Trash Bear list was missed. Nothing else calls the helper that builds that list.
        public const string Sardine = "131";

        public static bool NeedsReroll(bool isSummer, string? item)
        {
            return isSummer && item == Sardine;
        }
    }
}
