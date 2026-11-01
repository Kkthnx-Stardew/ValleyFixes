namespace ValleyFixes.Fixes
{
    /// <summary>
    /// The decision behind the George wheelchair fix, with no game dependencies so it is unit tested.
    /// Frames are pixel arrays of one region of one sprite frame.
    /// </summary>
    internal static class SpriteFrames
    {
        /// <summary>
        /// True when a frame of the base sheet is a still copy of the first frame while the same frame of the donor sheet moves,
        /// and the donor agrees with the base on the frames that are meant to match. Anything else means the sheets were changed
        /// by another mod or by the game, so nothing is copied.
        /// </summary>
        public static bool ShouldCopyFromDonor(uint[] baseFirst, uint[] baseFrame, uint[] baseThird, uint[] donorFirst, uint[] donorFrame, uint[] donorThird)
        {
            return Same(baseFirst, baseFrame)
                && Same(baseFirst, baseThird)
                && Same(baseFirst, donorFirst)
                && Same(baseThird, donorThird)
                && !Same(donorFirst, donorFrame);
        }

        public static bool Same(uint[] a, uint[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
    }
}
