using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.WildTrees;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-006: lets the Lumberjack bonus apply to the hardwood from mahogany trees, as the tree code intends.
    /// This raises hardwood yield for Lumberjack players, so it is off unless the player turns it on.
    /// </summary>
    internal sealed class LumberjackHardwoodFix
    {
        private const string AssetName = "Data/WildTrees";

        private readonly IMonitor _monitor;

        public LumberjackHardwoodFix(IMonitor monitor)
        {
            _monitor = monitor;
        }

        public void Register(IModEvents events)
        {
            events.Content.AssetRequested += OnAssetRequested;
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo(AssetName))
                return;

            e.Edit(asset =>
            {
                int changed = 0;
                foreach (WildTreeData? tree in asset.AsDictionary<string, WildTreeData>().Data.Values)
                {
                    if (tree?.ChopItems is null)
                        continue;

                    foreach (WildTreeChopItemData? chop in tree.ChopItems)
                    {
                        if (chop is not null && WildTreeHardwood.TryFix(chop.ItemId, chop.ForStump == true, out string fixedId))
                        {
                            chop.ItemId = fixedId;
                            changed++;
                        }
                    }
                }

                _monitor.Log($"Lumberjack hardwood fix changed {changed} of 2 expected tree entries.", changed == 2 ? LogLevel.Trace : LogLevel.Debug);
            }, AssetEditPriority.Late);
        }
    }
}
