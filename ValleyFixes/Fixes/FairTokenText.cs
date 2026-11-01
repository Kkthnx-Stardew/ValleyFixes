using System.Text.RegularExpressions;

namespace ValleyFixes.Fixes
{
    /// <summary>The pure text rule behind VF-007, kept free of game types so tests can compile it.</summary>
    public static class FairTokenText
    {
        /// <summary>The Lewis speech for third place in the Grange display. The game awards 250 for that place (Event.cs).</summary>
        public const string Key = "Event.cs.1729";

        private const string Wrong = "200";
        private const string Right = "250";

        // a whole number only, so 1200 or 2000 would never match
        private static readonly Regex WrongNumber = new(@"(?<!\d)200(?!\d)", RegexOptions.CultureInvariant);

        /// <summary>Swaps the one wrong number. Returns false and leaves the text alone unless it holds exactly one 200 and no 250.</summary>
        public static bool TryFix(string? text, out string fixedText)
        {
            fixedText = text ?? "";
            if (string.IsNullOrEmpty(text) || text.Contains(Right))
                return false;

            if (WrongNumber.Matches(text).Count != 1)
                return false;

            fixedText = WrongNumber.Replace(text, Right);
            return true;
        }
    }
}
