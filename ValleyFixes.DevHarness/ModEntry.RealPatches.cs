using StardewValley;

namespace ValleyFixes.DevHarness
{
    /// <summary>Loads a copy of a real save with the real mod list and writes the map of every method ValleyFixes patches and who else patches it.</summary>
    public partial class ModEntry
    {
        private IEnumerable<int> RealPatchesMain()
        {
            var into = new Dictionary<string, object?>();
            _results["real_patches"] = into;
            foreach (int w in WaitForWorld("real_patches_error")) yield return w;
            yield return 120;
            into["world"] = WorldFacts();
            into["patch_map"] = PatchMap.Build();
        }
    }
}
