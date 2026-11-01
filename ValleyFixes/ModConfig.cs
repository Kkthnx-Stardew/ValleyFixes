namespace ValleyFixes
{
    /// <summary>One switch per fix, so any of them can be turned off on its own.</summary>
    public sealed class ModConfig
    {
        /// <summary>VF-007: the Fair third place speech says 200 star tokens but the game awards 250.</summary>
        public bool FixFairThirdPlaceText { get; set; } = true;

        /// <summary>VF-001: three movie reactions hand the NPC name to the message command as the text.</summary>
        public bool FixMovieReactionScripts { get; set; } = true;

        /// <summary>VF-006: mahogany trees never give Lumberjack players the extra hardwood the tree code intends. Turning this on raises their mahogany hardwood by about 28% in testing, so it is off by default.</summary>
        public bool FixLumberjackMahoganyBonus { get; set; } = false;

        /// <summary>VF-012: the new farm and new farmhand menus accept a name made only of spaces. That can leave a farm that new farmhands cannot join. This only blocks the OK button for such names.</summary>
        public bool FixBlankNames { get; set; } = true;

        /// <summary>VF-030: the remixed Chef's bundle gives 1 Pink Cake where the classic one gives 3. Bundles are fixed when a world is created, so this only affects new remixed worlds.</summary>
        public bool FixChefBundleReward { get; set; } = true;

        /// <summary>VF-022: Trash Bear can ask for a Sardine in summer, when it cannot be caught. The request is changed to another summer item.</summary>
        public bool FixTrashBearSardine { get; set; } = true;

        /// <summary>VF-036: in Portuguese, single male farmers show the female "single" text on the social page.</summary>
        public bool FixPortugueseSingleStatus { get; set; } = true;

        /// <summary>VF-043: in single player, removed objects stay in memory in a network queue nobody reads. This clears the queues every morning. Memory only, no visible change.</summary>
        public bool TrimLocationChangeBacklog { get; set; } = true;

        /// <summary>VF-044: items a quest removes overnight (Qi Beans, Calico Eggs) still survive as ground drops made from an item id. Off by default because it removes items the player could otherwise pick up and keep.</summary>
        public bool FixQuestItemGroundDrops { get; set; } = false;

        /// <summary>VF-045: closing the About screen on the title screen turns the music back on while the mute button still shows muted. This mutes it again.</summary>
        public bool FixTitleMusicMute { get; set; } = true;

        /// <summary>VF-046: each morning the host logs special order participants who are no longer in this world. These keep an order from ever being removed. Log only unless the repair setting is also on.</summary>
        public bool CheckStaleOrderParticipants { get; set; } = true;

        /// <summary>VF-046: also remove those stale ids (only those, only when they belong to no player in this world). Off by default because it edits shared multiplayer state and was only tested with a made-up id.</summary>
        public bool RepairStaleOrderParticipants { get; set; } = false;

        /// <summary>VF-047: George's wheelchair wheels do not turn when he walks up. The missing wheel frames are copied from his winter sprite sheet when the sheets are unmodified.</summary>
        public bool FixGeorgeWheelchair { get; set; } = true;

        /// <summary>VF-004: writes a short log entry when something cuts the eating animation short. Changes nothing in the game.</summary>
        public bool LogEatInterrupts { get; set; } = true;
    }
}
