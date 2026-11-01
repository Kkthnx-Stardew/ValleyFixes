namespace ValleyFixes.Fixes
{
    /// <summary>The pure bundle rule behind VF-030, kept free of game types so tests can compile it.</summary>
    public static class ChefBundleReward
    {
        // The classic Chef's bundle gives 3 Pink Cake. The remixed one gives 1, the only remixed reward that differs from its classic bundle.
        public const string Area = "Bulletin Board";
        public const string Bundle = "Chef's";
        public const string Wrong = "1 Pink Cake";
        public const string Right = "3 Pink Cake";

        /// <summary>Returns true and the corrected reward only for the Chef's bundle in the Bulletin Board with the known wrong reward.</summary>
        public static bool TryFix(string? area, string? bundle, string? reward, out string fixedReward)
        {
            fixedReward = reward ?? "";
            if (area != Area || bundle != Bundle || reward != Wrong)
                return false;

            fixedReward = Right;
            return true;
        }
    }
}
