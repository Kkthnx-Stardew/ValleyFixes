namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-058: the Mines floors whose coal cart has been emptied this year, as the host knows them. The game keeps this per
    /// machine and never sends it, so a farmhand's pick is invisible to everyone else (and to the farmhand after leaving the floor).
    /// </summary>
    internal sealed class CoalCartLedger
    {
        private readonly HashSet<int> _levels = new();

        public int Year { get; private set; }

        public bool Contains(int level) => _levels.Contains(level);

        public int[] Levels => _levels.OrderBy(l => l).ToArray();

        /// <summary>Returns true when the floor was not already marked.</summary>
        public bool Mark(int level) => _levels.Add(level);

        public void Replace(int year, IEnumerable<int> levels)
        {
            Year = year;
            _levels.Clear();
            foreach (int level in levels)
                _levels.Add(level);
        }

        /// <summary>
        /// Mirrors <c>MineShaft.yearUpdate</c>: at the turn of the year the game drops the saved changes of every floor that is not
        /// a multiple of 5 or is below the Mines (above 120), so those carts fill again. Floors at multiples of 5 keep theirs.
        /// Returns true when the ledger changed.
        /// </summary>
        public bool RollOverTo(int year)
        {
            if (year == Year)
                return false;

            Year = year;
            _levels.RemoveWhere(level => level > 120 || level % 5 != 0);
            return true;
        }
    }
}
