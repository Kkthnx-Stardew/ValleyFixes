namespace ValleyFixes.Fixes
{
    /// <summary>The ids on a special order that belong to nobody in this world. No game dependencies, so it is unit tested.</summary>
    internal static class StaleParticipants
    {
        public static List<long> Find(IEnumerable<long> participantIds, ISet<long> knownPlayerIds)
        {
            var stale = new List<long>();
            foreach (long id in participantIds)
            {
                if (!knownPlayerIds.Contains(id))
                    stale.Add(id);
            }
            return stale;
        }
    }
}
