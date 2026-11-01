using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Bundles;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-030: the remixed Chef's bundle gives 1 Pink Cake where the classic bundle gives 3.
    /// Bundles are decided when a world is created, so this only reaches new remixed worlds.
    /// </summary>
    internal sealed class ChefBundleRewardFix
    {
        private const string AssetName = "Data/RandomBundles";

        private readonly IMonitor _monitor;

        public ChefBundleRewardFix(IMonitor monitor)
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
                foreach (RandomBundleData? area in asset.GetData<List<RandomBundleData>>())
                {
                    if (area is null)
                        continue;

                    foreach (BundleData? bundle in Bundles(area))
                    {
                        if (bundle is not null && ChefBundleReward.TryFix(area.AreaName, bundle.Name, bundle.Reward, out string fixedReward))
                        {
                            bundle.Reward = fixedReward;
                            changed++;
                        }
                    }
                }

                _monitor.Log($"Chef's bundle fix changed {changed} of 1 expected reward.", changed == 1 ? LogLevel.Trace : LogLevel.Debug);
            }, AssetEditPriority.Late);
        }

        private static IEnumerable<BundleData?> Bundles(RandomBundleData area)
        {
            if (area.Bundles is not null)
            {
                foreach (BundleData? bundle in area.Bundles)
                    yield return bundle;
            }

            if (area.BundleSets is not null)
            {
                foreach (BundleSetData? set in area.BundleSets)
                {
                    if (set?.Bundles is null)
                        continue;
                    foreach (BundleData? bundle in set.Bundles)
                        yield return bundle;
                }
            }
        }
    }
}
