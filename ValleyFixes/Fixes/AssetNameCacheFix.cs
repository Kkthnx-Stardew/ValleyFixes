using System.Collections.Concurrent;
using HarmonyLib;
using StardewModdingAPI;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-061: every asset name SMAPI parses builds a new <c>AssetName</c> object, several strings and two delegates, and some mods
    /// do that for every sprite they see drawn (Content Patcher Animations runs it after each <c>SpriteBatch.Draw</c>, about 2,000
    /// times a frame in a big mod list). On a real 100 mod save it was the largest source of garbage, which makes the game collect
    /// memory dozens of times a second. An <c>AssetName</c> never changes and the parse depends only on the raw text and SMAPI's table
    /// of locale codes, so the answer for a name already seen can be handed back as is. The cache is cleared whenever SMAPI replaces
    /// that table (when custom languages load), is capped in size, and is skipped altogether if another mod already patches the
    /// same SMAPI method.
    /// </summary>
    internal static class AssetNameCacheFix
    {
        private const int MaxEntries = 8192;

        private static readonly ConcurrentDictionary<string, object> WithLocales = new();
        private static readonly ConcurrentDictionary<string, object> WithoutLocales = new();
        private static object? _localesFor;
        private static IMonitor _monitor = null!; // set in Apply
        private static long _hits;
        private static long _misses;

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            var type = Type.GetType("StardewModdingAPI.Framework.ContentCoordinator, StardewModdingAPI");
            var target = type?.GetMethod("ParseAssetName", new[] { typeof(string), typeof(bool) });
            var localeField = type is null ? null : AccessTools.Field(type, "LocaleCodes");
            if (target is null || localeField is null)
            {
                monitor.Log("Asset name cache is off: SMAPI's ParseAssetName or its locale table was not found, so SMAPI has changed.", LogLevel.Warn);
                return;
            }

            var existing = Harmony.GetPatchInfo(target);
            var others = existing is null
                ? new List<string>()
                : existing.Prefixes.Concat(existing.Postfixes).Concat(existing.Transpilers).Concat(existing.Finalizers).Select(p => p.owner).Distinct().ToList();
            if (others.Count > 0)
            {
                monitor.Log($"Asset name cache is off: {string.Join(", ", others)} already patches SMAPI's ParseAssetName, and two patches on one method could interfere.", LogLevel.Info);
                return;
            }

            harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(AssetNameCacheFix), nameof(Before)) { priority = Priority.Last },
                postfix: new HarmonyMethod(typeof(AssetNameCacheFix), nameof(After)));
        }

        private static bool Before(string rawName, bool allowLocales, object ___LocaleCodes, ref object __result, out bool __state)
        {
            __state = false;
            try
            {
                if (string.IsNullOrWhiteSpace(rawName))
                    return true;

                if (!ReferenceEquals(___LocaleCodes, _localesFor))
                {
                    WithLocales.Clear();
                    WithoutLocales.Clear();
                    _localesFor = ___LocaleCodes;
                }

                if ((allowLocales ? WithLocales : WithoutLocales).TryGetValue(rawName, out object? cached))
                {
                    __result = cached;
                    __state = true;
                    Interlocked.Increment(ref _hits);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Asset name cache lookup failed, so SMAPI's own parse is used. {ex.Message}", LogLevel.Warn);
            }
            return true;
        }

        private static void After(string rawName, bool allowLocales, object ___LocaleCodes, object __result, bool __state)
        {
            if (__state || __result is null || !ReferenceEquals(___LocaleCodes, _localesFor))
                return;

            try
            {
                var cache = allowLocales ? WithLocales : WithoutLocales;
                if (cache.Count >= MaxEntries)
                    cache.Clear();
                cache[rawName] = __result;
                Interlocked.Increment(ref _misses);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Asset name cache store failed. {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>Used by the log line and the tests: how often a parse was answered from the cache and how often it was not.</summary>
        internal static (long Hits, long Misses) Counts => (Interlocked.Read(ref _hits), Interlocked.Read(ref _misses));
    }
}
