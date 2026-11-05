using HarmonyLib;
using StardewModdingAPI;
using xTile.Tiles;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-066: SMAPI loads mod maps made in the Tiled TMX format through its bundled TMXTile library, which writes an "@Rotation" and an
    /// "@Flip" property on every tile that has any property, even when the value is the default 0. A property holds a dictionary entry,
    /// a string and a value object. On a real save with 101 mods that was 2.7 million entries, 99% of them 0, and about 125 MB of the managed heap.
    /// The only code that reads them is the library's own <c>GetRotationValue</c> and <c>GetFlip</c>, which answer 0 when the property is missing.
    /// So a value of 0 is not stored. If the property already holds another value, it is removed, which reads back as 0 too.
    /// Skipped if another mod already patches the same library methods.
    /// </summary>
    internal static class TileDefaultPropertiesFix
    {
        private const string RotationKey = "@Rotation";
        private const string FlipKey = "@Flip";

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            var type = Type.GetType("TMXTile.TMXExtensions, TMXTile");
            var rotation = type is null ? null : AccessTools.Method(type, "SetRotationValue", new[] { typeof(Tile), typeof(int) });
            var flip = type is null ? null : AccessTools.Method(type, "SetFlip", new[] { typeof(Tile), typeof(int) });
            if (rotation is null || flip is null)
            {
                monitor.Log("Tile default property fix is off: SMAPI's TMXTile.SetRotationValue or SetFlip was not found, so SMAPI has changed.", LogLevel.Warn);
                return;
            }

            var others = HarmonyOwners.Foreign(rotation).Concat(HarmonyOwners.Foreign(flip)).Distinct().ToList();
            if (others.Count > 0)
            {
                monitor.Log($"Tile default property fix is off: {string.Join(", ", others)} already patches SMAPI's TMXTile, and two patches on one method could interfere.", LogLevel.Info);
                return;
            }

            harmony.Patch(rotation, prefix: new HarmonyMethod(typeof(TileDefaultPropertiesFix), nameof(BeforeSetRotation)));
            harmony.Patch(flip, prefix: new HarmonyMethod(typeof(TileDefaultPropertiesFix), nameof(BeforeSetFlip)));
        }

        private static bool BeforeSetRotation(Tile tile, int rotation) => Keep(tile, RotationKey, rotation);

        private static bool BeforeSetFlip(Tile tile, int flip) => Keep(tile, FlipKey, flip);

        /// <summary>Returns true when the library should store the value as it always did. A 0 is never stored and clears an older value.</summary>
        private static bool Keep(Tile tile, string key, int value)
        {
            if (value != 0)
                return true;

            try
            {
                tile.Properties.Remove(key);
                return false;
            }
            catch
            {
                // if anything is odd the library does what it always did
                return true;
            }
        }
    }
}
