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

        /// <summary>VF-055: with separate wallets, gold sent to or earned for a farmhand who is not in the game is lost at the next save and reload. The host copies the shared wallet into the saved copy before each save. Host only, does nothing without separate wallets.</summary>
        public bool FixOfflineFarmhandWallets { get; set; } = true;

        /// <summary>VF-058: in multiplayer, a coal cart a farmhand empties stays full for everyone else, and for that farmhand after they leave the floor and return, so it can be emptied again and again. The host keeps the list of emptied carts and shares it. Needs the mod on every player's machine. Does nothing in single player.</summary>
        public bool SyncCoalCarts { get; set; } = true;

        /// <summary>VF-063: joining a host by IP address or on a local network starts with one UDP packet. If it or the host's answer is lost, the game never sends it again and the joiner waits 45 seconds for a timeout. This sends the request again up to three times, three seconds apart, until the host answers. It also keeps a typed port on each attempt. The farmhand's side only, and it changes nothing once the host has answered. Skipped if another mod patches the same game methods.</summary>
        public bool RetryLostJoinRequests { get; set; } = true;

        /// <summary>VF-062: the game runs the background update of every location every frame, even locations nobody is in and that hold nothing. In a big mod list that is hundreds of locations. A location that has had nobody in it for 60 frames in a row, holds no characters, animals, buildings or temporary sprites and has no event is skipped. Host or single player only. No visible change. Skipped if another mod patches the same game method.</summary>
        public bool SkipEmptyLocationUpdates { get; set; } = true;

        /// <summary>VF-062: monsters in an outdoor location nobody has been in for 60 frames are not updated, so they stand still until a player arrives. Expansion mods can leave hundreds of monsters in locations nobody visits. Villagers and animals are never affected, and neither are the Farm, buildings, mines and the volcano. Host or single player only.</summary>
        public bool PauseIdleLocationMonsters { get; set; } = true;

        /// <summary>VF-061: SMAPI builds a new asset name object, several strings and two delegates every time any mod asks it to parse an asset name, and some mods do so for every sprite drawn (thousands of times a frame in a big mod list). The answers are kept and handed back, which removes most of the garbage the game has to collect. No visible change. Skipped if another mod already patches the same SMAPI method.</summary>
        public bool CacheAssetNames { get; set; } = true;

        /// <summary>VF-060: a farmhand keeps seeing festival map changes (the SquidFest booth and sign) after the festival is over, because the game's own check for taking the booth away looks at the wrong tilesheet name and the farmhand's rebuilt Beach has an empty list of applied changes. The booth tile is now looked up on the right tilesheet. Works on the farmhand's machine alone.</summary>
        public bool FixStaleMapOverrides { get; set; } = true;

        /// <summary>VF-059: a stardrop that something cuts short while you eat it gives nothing, and the stardrop is already used up. When this is on, the energy bonus is applied the moment the eating is cut short. Other food is not changed.</summary>
        public bool FixStardropInterrupt { get; set; } = true;

        /// <summary>VF-064: menu boxes (item tooltips, dialogue boxes) draw only three of the four corner pieces of their drop shadow, which leaves a small notch under the top left corner. The missing piece is drawn. Cosmetic only. Skipped if another mod patches the same method.</summary>
        public bool FixMenuBoxShadow { get; set; } = true;

        /// <summary>VF-065: how many ticks (1/60 s each) this machine delays what other players and monsters do, so movement stays smooth. The game uses 15, which looks about 250 ms late. In the test, 8 looked 141 ms late and 4 looked 72 ms late. On a link with 15 ms jitter and 1% loss, 4 froze up to 3.7% of frames and 15 froze up to 1.8%. Only this player is affected. 15 means no change. Allowed 2 to 15.</summary>
        public int MultiplayerInterpolationTicks { get; set; } = 15;

        /// <summary>VF-066: SMAPI writes a rotation and a flip property of 0 on every tile of every Tiled (TMX) mod map, which nothing needs. Not storing them freed 125 MB of the managed heap on a real save with 101 mods. A tile with no such property reads back as 0 rotation and no flip, so nothing looks different. Skipped if another mod patches the same SMAPI methods.</summary>
        public bool SkipDefaultTileProperties { get; set; } = true;

        /// <summary>VF-067: with separate wallets, an empty slot in a farmhand's part of a Mini-Shipping Bin makes the host throw a NullReferenceException when the day ends, so the new day never starts. The game's developers confirmed it and said 1.6.16 cleans every player's part. Empty slots are removed from every part while the day is ending. Skipped if another mod patches Chest.clearNulls.</summary>
        public bool FixMiniShippingBinCrash { get; set; } = true;

        /// <summary>VF-068: fruit that grew on a tree before it aged to the next quality keeps the old quality when picked. The game's developers called this an unintended change in 1.6 and said 1.6.16 fixes it. Off by default because it raises the quality of some fruit.</summary>
        public bool FixFruitTreeQuality { get; set; } = false;

        /// <summary>VF-069: once Pierre has been given his missing stock list, his shop lists wheat, corn and sunflower seeds twice, one at the normal price and one out of season at 1.5 times the price. The dearer copy is dropped. The game's developers list it as fixed in 1.6.16. Only those six shop entries are touched. Skipped if another mod patches ShopBuilder.GetShopStock.</summary>
        public bool FixPierreDuplicateSeeds { get; set; } = true;

        /// <summary>VF-070: an item delivery quest that asks for a cooked dish, for anyone but the Wizard, threw a NullReferenceException about a third of the time because one way of writing its text left an array null. Which days it happens on is fixed by the save and the day. The game's developers list it as fixed in 1.6.16. Skipped if another mod patches ItemDeliveryQuest.loadQuestInfo.</summary>
        public bool FixCookingDeliveryQuestCrash { get; set; } = true;

        /// <summary>VF-071: the wedding script asks the game for the farmer's spouse while the farmer is engaged, and the game only answers for a married farmer. Lewis's matrimonial speech comes out as a fragment and the spouse's position in the script is left unresolved. While the wedding event is built, the engaged spouse is given to the script. The game's developers list Lewis skipping his speech as fixed in 1.6.16. Skipped if another mod patches the same methods.</summary>
        public bool FixWeddingSpouseTokens { get; set; } = true;

        /// <summary>VF-004: writes a short log entry when something cuts the eating animation short. Changes nothing in the game.</summary>
        public bool LogEatInterrupts { get; set; } = true;
    }
}
