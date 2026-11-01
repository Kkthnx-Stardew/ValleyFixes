namespace ValleyFixes.Fixes
{
    /// <summary>The pure script rule behind VF-001, kept free of game types so tests can compile it.</summary>
    public static class MovieScripts
    {
        // The three reactions seen with a stray name in the installed 1.6.15 data. The message command reads only its first
        // argument, so "/message Sam "text"" shows "Sam" and drops the real text.
        private static readonly (string Npc, string Tag)[] Known =
        {
            ("Sam", "summer_movie_1"),
            ("Sam", "dislike"),
            ("Demetrius", "spring_movie_1")
        };

        /// <summary>Removes the stray name from a known reaction. Returns false and leaves the script alone for any other reaction or shape.</summary>
        public static bool TryFix(string npc, string tag, string? script, out string fixedScript)
        {
            fixedScript = script ?? "";
            if (string.IsNullOrEmpty(script) || !IsKnown(npc, tag))
                return false;

            string bad = $"/message {npc} \"";
            int at = script.IndexOf(bad, StringComparison.Ordinal);
            if (at < 0 || script.IndexOf(bad, at + 1, StringComparison.Ordinal) >= 0)
                return false;

            fixedScript = script.Remove(at, bad.Length).Insert(at, "/message \"");
            return true;
        }

        private static bool IsKnown(string npc, string tag)
        {
            foreach (var (knownNpc, knownTag) in Known)
            {
                if (knownNpc == npc && knownTag == tag)
                    return true;
            }
            return false;
        }
    }
}
