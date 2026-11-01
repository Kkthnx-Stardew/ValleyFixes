namespace ValleyFixes.Fixes
{
    /// <summary>The pure text rule behind VF-036, kept free of game types so tests can compile it.</summary>
    public static class GenderedSingleStatus
    {
        public const string FemaleKey = "SocialPage_Relationship_Single_Female";
        public const string MaleKey = "SocialPage_Relationship_Single_Male";

        /// <summary>
        /// The social page, in a language that uses gendered translations, splits the female string on a slash and shows the first part to
        /// men and the last to women. The Portuguese string has no slash, so men get the female text and the male string is never read.
        /// Returns true and "male/female" only when the female string has no slash and the male string exists and differs.
        /// </summary>
        public static bool TryFix(string? female, string? male, out string combined)
        {
            combined = female ?? "";
            if (string.IsNullOrWhiteSpace(female) || string.IsNullOrWhiteSpace(male))
                return false;
            if (female.Contains('/') || male.Contains('/') || female == male)
                return false;

            combined = male + "/" + female;
            return true;
        }
    }
}
