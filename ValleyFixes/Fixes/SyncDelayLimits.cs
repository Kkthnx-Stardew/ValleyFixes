namespace ValleyFixes.Fixes
{
    /// <summary>The allowed range for the multiplayer delay setting.</summary>
    public static class SyncDelayLimits
    {
        public const int VanillaTicks = 15;
        public const int MinTicks = 2;

        /// <summary>The value to use for a setting. The game's own 15 when the setting is out of range.</summary>
        public static int Clamp(int ticks) => ticks < MinTicks || ticks > VanillaTicks ? VanillaTicks : ticks;
    }
}
