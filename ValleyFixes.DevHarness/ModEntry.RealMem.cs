using StardewValley;
using xTile.Layers;
using xTile.Tiles;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Memory scenario on a copy of a real save with the real mod list. Counts what is on every loaded map's tiles, then removes the
    /// default "@Rotation" and "@Flip" properties and measures the managed heap after a full collection before and after.
    /// Run with -Only realmem and the save copy named in VFH_LOAD. Optionally pauses for a heap dump (-HeapDumps).
    /// </summary>
    public partial class ModEntry
    {
        private static long HeapNow()
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            return GC.GetTotalMemory(true);
        }

        private IEnumerable<int> RealMemMain()
        {
            var into = new Dictionary<string, object?>();
            _results["realmem"] = into;
            foreach (int w in WaitForWorld("realmem_error")) yield return w;
            into["world"] = WorldFacts();
            yield return 300;

            into["memoryAtStart"] = MemoryLight();
            long heap0 = HeapNow();
            into["heapAfterLoadMB"] = Math.Round(heap0 / 1048576.0, 1);
            foreach (int w in Hold("loaded")) yield return w;

            int maps = 0, layers = 0, tiles = 0, withProps = 0, propEntries = 0, rotation = 0, flip = 0, rotationZero = 0, flipZero = 0;
            var keyCounts = new Dictionary<string, int>();
            ulong hash = 14695981039346656037UL;
            void Mix(string text) { foreach (char c in text) { hash ^= c; hash *= 1099511628211UL; } hash ^= 0xFF; hash *= 1099511628211UL; }
            var seenMaps = new HashSet<xTile.Map>();
            Utility.ForEachLocation(loc =>
            {
                var map = loc.map;
                if (map is null || !seenMaps.Add(map))
                    return true;
                maps++;
                foreach (Layer layer in map.Layers)
                {
                    layers++;
                    for (int x = 0; x < layer.LayerWidth; x++)
                    {
                        for (int y = 0; y < layer.LayerHeight; y++)
                        {
                            Tile? tile = layer.Tiles[x, y];
                            if (tile is null)
                                continue;
                            tiles++;
                            // what the TMX library would answer for this tile, so a missing property and a stored 0 hash the same
                            int rot = tile.Properties.TryGetValue("@Rotation", out var rv) ? int.Parse(rv.ToString()) : 0;
                            int flp = tile.Properties.TryGetValue("@Flip", out var fv) ? int.Parse(fv.ToString()) : 0;
                            Mix($"{map.Id}|{layer.Id}|{x}|{y}|{tile.TileIndex}|{rot}|{flp}");
                            if (tile.Properties.Count == 0)
                                continue;
                            withProps++;
                            foreach (var kv in tile.Properties)
                            {
                                propEntries++;
                                if (kv.Key != "@Rotation" && kv.Key != "@Flip") Mix(kv.Key + "=" + kv.Value);
                                keyCounts[kv.Key] = keyCounts.GetValueOrDefault(kv.Key) + 1;
                                if (kv.Key == "@Rotation") { rotation++; if (kv.Value.ToString() == "0") rotationZero++; }
                                if (kv.Key == "@Flip") { flip++; if (kv.Value.ToString() == "0") flipZero++; }
                            }
                        }
                    }
                }
                return true;
            }, includeInteriors: true, includeGenerated: true);

            into["tileScan"] = new
            {
                tileHash = hash.ToString("x16"),
                maps, layers, tiles, tilesWithProperties = withProps, propertyEntries = propEntries,
                rotationEntries = rotation, rotationZero, flipEntries = flip, flipZero,
                topKeys = keyCounts.OrderByDescending(k => k.Value).Take(8).Select(k => new { k.Key, k.Value }).ToList()
            };
            yield return 5;

            if (Environment.GetEnvironmentVariable("VFH_NOSTRIP") == "1")
            {
                into["memoryAtEnd"] = MemoryLight();
                yield return 60;
                yield break;
            }

            // strip only the entries whose value is the default 0, the same set a loader would never have added
            int stripped = 0;
            foreach (var map in seenMaps)
            {
                foreach (Layer layer in map.Layers)
                {
                    for (int x = 0; x < layer.LayerWidth; x++)
                    {
                        for (int y = 0; y < layer.LayerHeight; y++)
                        {
                            Tile? tile = layer.Tiles[x, y];
                            if (tile is null || tile.Properties.Count == 0)
                                continue;
                            foreach (string key in new[] { "@Rotation", "@Flip" })
                            {
                                if (tile.Properties.TryGetValue(key, out var value) && value.ToString() == "0")
                                {
                                    tile.Properties.Remove(key);
                                    stripped++;
                                }
                            }
                        }
                    }
                }
            }
            yield return 5;
            long heap1 = HeapNow();
            into["stripped"] = stripped;
            into["heapAfterStripMB"] = Math.Round(heap1 / 1048576.0, 1);
            into["freedMB"] = Math.Round((heap0 - heap1) / 1048576.0, 1);
            into["memoryAtEnd"] = MemoryLight();
            foreach (int w in Hold("stripped")) yield return w;
            yield return 60;
        }
    }
}
