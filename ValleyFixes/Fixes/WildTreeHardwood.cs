namespace ValleyFixes.Fixes
{
    /// <summary>The pure id rule behind VF-006, kept free of game types so tests can compile it.</summary>
    public static class WildTreeHardwood
    {
        // Tree.tickUpdate tests the data entry's ItemId against the unqualified id "709" to decide that a drop is hardwood.
        // The game data stores the qualified id, so the test never matches and the Lumberjack bonus is skipped.
        public const string DataId = "(O)709";
        public const string ComparedId = "709";

        /// <summary>Returns true and the unqualified id for a hardwood drop of a standing tree. Stump entries are never read by that test and are left alone.</summary>
        public static bool TryFix(string? itemId, bool forStump, out string fixedId)
        {
            fixedId = itemId ?? "";
            if (forStump || itemId != DataId)
                return false;

            fixedId = ComparedId;
            return true;
        }
    }
}
