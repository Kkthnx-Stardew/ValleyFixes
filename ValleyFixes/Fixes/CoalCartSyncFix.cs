using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-058: a farmhand who empties a Mines coal cart keeps no record the host or anyone else can see. The cart stays full for
    /// everyone else, and for the farmhand too once they leave the floor and come back, so the same cart can be emptied again
    /// and again. The game keeps its list of emptied carts (<c>MineShaft.permanentMineChanges</c>) per machine and never sends it.
    /// Here the host holds the list. Every pick is reported to the host, the host sends the list to everyone, and each machine
    /// draws a listed cart as empty and refuses to empty it a second time. Needs the mod on every player's machine. Does nothing
    /// in single player.
    /// </summary>
    internal sealed class CoalCartSyncFix
    {
        private const int FullCartTile = 194;
        private const int EmptyCartTile = 195;
        private const string TakenType = "CartTaken";
        private const string StateType = "CartState";
        private const string SaveKey = "coal-carts";

        private static CoalCartSyncFix? _instance;

        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;
        private readonly string _modId;
        private readonly CoalCartLedger _ledger = new();
        private readonly HashSet<int> _logged = new();

        public CoalCartSyncFix(IMonitor monitor, IModHelper helper, string modId)
        {
            _monitor = monitor;
            _helper = helper;
            _modId = modId;
        }

        public void Register(Harmony harmony)
        {
            _instance = this;

            var update = AccessTools.Method(typeof(MineShaft), nameof(MineShaft.updateMineLevelData), new[] { typeof(int), typeof(int) });
            var action = AccessTools.Method(typeof(MineShaft), nameof(MineShaft.checkAction));
            if (update is null || action is null)
            {
                _monitor.Log("Coal cart sync is off: MineShaft.updateMineLevelData or MineShaft.checkAction was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            harmony.Patch(update, postfix: new HarmonyMethod(typeof(CoalCartSyncFix), nameof(AfterUpdateMineLevelData)));
            harmony.Patch(action, prefix: new HarmonyMethod(typeof(CoalCartSyncFix), nameof(BeforeCheckAction)));

            var events = _helper.Events;
            events.GameLoop.SaveLoaded += OnSaveLoaded;
            events.GameLoop.Saving += OnSaving;
            events.GameLoop.DayStarted += OnDayStarted;
            events.GameLoop.ReturnedToTitle += (_, _) => _ledger.Replace(0, Array.Empty<int>());
            events.Multiplayer.PeerConnected += OnPeerConnected;
            events.Multiplayer.ModMessageReceived += OnMessage;
            events.Player.Warped += OnWarped;
        }

        // ---- the ledger ------------------------------------------------------

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            _ledger.Replace(Game1.year, Array.Empty<int>());
            if (!Context.IsMainPlayer)
                return;

            try
            {
                var saved = _helper.Data.ReadSaveData<LedgerSave>(SaveKey);
                if (saved is not null)
                {
                    _ledger.Replace(saved.Year, saved.Levels ?? Array.Empty<int>());
                    _ledger.RollOverTo(Game1.year);
                }

                // carts the host emptied before this mod saw them are already in the game's own list
                foreach (var pair in MineShaft.permanentMineChanges)
                {
                    if (pair.Value.coalCartsLeft <= 0)
                        _ledger.Mark(pair.Key);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not read the saved coal cart list, so it starts from the game's own list. {ex.Message}", LogLevel.Warn);
            }
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            if (!Context.IsMainPlayer)
                return;

            try
            {
                _helper.Data.WriteSaveData(SaveKey, new LedgerSave { Year = _ledger.Year, Levels = _ledger.Levels });
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not save the coal cart list. {ex.Message}", LogLevel.Warn);
            }
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!Context.IsMainPlayer || !Context.IsMultiplayer)
                return;

            if (_ledger.RollOverTo(Game1.year))
                Broadcast();
        }

        private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
        {
            if (Context.IsMainPlayer && _ledger.Levels.Length > 0)
                SendState(new[] { e.Peer.PlayerID });
        }

        // ---- messages ---------------------------------------------------------

        private void Broadcast() => SendState(null);

        private void SendState(long[]? playerIds)
        {
            _helper.Multiplayer.SendMessage(new CartMessage { Year = _ledger.Year, Levels = _ledger.Levels }, StateType, new[] { _modId }, playerIds);
        }

        private void OnMessage(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != _modId)
                return;

            try
            {
                var message = e.ReadAs<CartMessage>();
                if (e.Type == TakenType && Context.IsMainPlayer)
                {
                    foreach (int level in message.Levels)
                    {
                        if (_ledger.Mark(level))
                            _monitor.Log($"Player {e.FromPlayerID} emptied the coal cart on Mines floor {level}. It is now empty for everyone.", LogLevel.Info);
                    }
                    Broadcast();
                }
                else if (e.Type == StateType && !Context.IsMainPlayer)
                {
                    _ledger.Replace(message.Year, message.Levels);
                    if (Game1.currentLocation is MineShaft shaft)
                        EmptyCartsOn(shaft);
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not read a coal cart message. {ex.Message}", LogLevel.Warn);
            }
        }

        // ---- the game's side ----------------------------------------------------

        private static void AfterUpdateMineLevelData(MineShaft __instance, int feature, int amount)
        {
            if (feature != 2 || amount >= 0 || !Context.IsMultiplayer)
                return;

            _instance?.OnCartEmptied(__instance.mineLevel);
        }

        private void OnCartEmptied(int level)
        {
            try
            {
                _ledger.Mark(level);
                if (Context.IsMainPlayer)
                {
                    _monitor.Log($"The host emptied the coal cart on Mines floor {level}.", LogLevel.Info);
                    Broadcast();
                }
                else
                {
                    _helper.Multiplayer.SendMessage(new CartMessage { Year = _ledger.Year, Levels = new[] { level } }, TakenType, new[] { _modId }, new[] { Game1.MasterPlayer.UniqueMultiplayerID });
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not report an emptied coal cart. {ex.Message}", LogLevel.Warn);
            }
        }

        // a cart the list says is empty gives nothing, wherever the player's own copy of the floor still shows it full
        private static bool BeforeCheckAction(MineShaft __instance, xTile.Dimensions.Location tileLocation, Farmer who, ref bool __result)
        {
            try
            {
                var self = _instance;
                if (self is null || !Context.IsMultiplayer || !who.IsLocalPlayer)
                    return true;
                if (__instance.getTileIndexAt(tileLocation, "Buildings", "mine") != FullCartTile || !self.IsEmpty(__instance.mineLevel))
                    return true;

                self.EmptyCartsOn(__instance);
                __result = true;
                return false;
            }
            catch (Exception ex)
            {
                _instance?._monitor.Log($"Coal cart check failed, so the game's own rule applies. {ex.Message}", LogLevel.Warn);
                return true;
            }
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (e.IsLocalPlayer && Context.IsMultiplayer && e.NewLocation is MineShaft shaft)
                EmptyCartsOn(shaft);
        }

        private bool IsEmpty(int level)
        {
            return _ledger.Contains(level) || (MineShaft.permanentMineChanges.TryGetValue(level, out var info) && info.coalCartsLeft <= 0);
        }

        /// <summary>Draws every full cart on the floor as empty, the way the game does when it builds a floor that was already emptied.</summary>
        private void EmptyCartsOn(MineShaft shaft)
        {
            if (!IsEmpty(shaft.mineLevel) || shaft.map?.GetLayer("Buildings") is not { } layer)
                return;

            int changed = 0;
            for (int x = 0; x < layer.LayerWidth; x++)
            {
                for (int y = 0; y < layer.LayerHeight; y++)
                {
                    if ((layer.Tiles[x, y]?.TileIndex ?? -1) != FullCartTile)
                        continue;
                    shaft.setMapTile(x, y, EmptyCartTile, "Buildings", "mine");
                    shaft.setMapTile(x, y - 1, 179, "Front", "mine");
                    changed++;
                }
            }

            if (changed > 0 && _logged.Add(shaft.mineLevel))
                _monitor.Log($"The coal cart on Mines floor {shaft.mineLevel} was already emptied, so it is shown empty.", LogLevel.Info);
        }

        internal sealed class CartMessage
        {
            public int Year { get; set; }
            public int[] Levels { get; set; } = Array.Empty<int>();
        }

        internal sealed class LedgerSave
        {
            public int Year { get; set; }
            public int[]? Levels { get; set; }
        }
    }
}
