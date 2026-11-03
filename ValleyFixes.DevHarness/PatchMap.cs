using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Test tool only. Lists which mods have Harmony patches on the game's hottest methods and how many methods each patching mod
    /// touches, so a slow method can be tied to the mods that changed it, and a fix can know whom it would collide with.
    /// </summary>
    internal static class PatchMap
    {
        private static readonly (Type Type, string Name, Type[]? Args)[] Hot =
        {
            (typeof(GameLocation), "updateEvenIfFarmerIsntHere", null),
            (typeof(GameLocation), "updateCharacters", null),
            (typeof(GameLocation), "UpdateWhenCurrentLocation", null),
            (typeof(GameLocation), "MakeMapModifications", null),
            (typeof(GameLocation), "performTenMinuteUpdate", null),
            (typeof(GameLocation), "draw", new[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch) }),
            (typeof(NPC), "update", new[] { typeof(Microsoft.Xna.Framework.GameTime), typeof(GameLocation) }),
            (typeof(Monster), "update", new[] { typeof(Microsoft.Xna.Framework.GameTime), typeof(GameLocation) }),
            (typeof(Game1), "_update", null),
            (typeof(Game1), "_draw", null),
            (typeof(Game1), "shouldTimePass", null),
            (typeof(Game1), "performTenMinuteClockUpdate", null),
            (typeof(Game1), "drawHUD", null),
            (typeof(Game1), "drawWorld", null),
            (typeof(Game1), "UpdateLocations", null),
            (typeof(Game1), "_UpdateLocation", null)
        };

        public static object Build()
        {
            var perMethod = new List<object>();
            foreach (var (type, name, args) in Hot)
            {
                MethodBase? method = null;
                try
                {
                    method = args is null ? AccessTools.Method(type, name) : AccessTools.Method(type, name, args);
                }
                catch
                {
                    // ambiguous overload: report it as not resolved
                }
                if (method is null)
                {
                    perMethod.Add(new { method = $"{type.Name}.{name}", note = "not found or ambiguous" });
                    continue;
                }

                var info = Harmony.GetPatchInfo(method);
                if (info is null)
                {
                    perMethod.Add(new { method = $"{type.Name}.{name}", prefixes = Array.Empty<string>(), postfixes = Array.Empty<string>(), transpilers = Array.Empty<string>(), finalizers = Array.Empty<string>() });
                    continue;
                }
                perMethod.Add(new
                {
                    method = $"{type.Name}.{name}",
                    prefixes = info.Prefixes.Select(p => p.owner).ToArray(),
                    postfixes = info.Postfixes.Select(p => p.owner).ToArray(),
                    transpilers = info.Transpilers.Select(p => p.owner).ToArray(),
                    finalizers = info.Finalizers.Select(p => p.owner).ToArray()
                });
            }

            var owners = new Dictionary<string, int>();
            int patched = 0;
            foreach (var method in Harmony.GetAllPatchedMethods())
            {
                patched++;
                var info = Harmony.GetPatchInfo(method);
                if (info is null)
                    continue;
                foreach (string owner in info.Owners)
                    owners[owner] = owners.TryGetValue(owner, out int n) ? n + 1 : 1;
            }

            return new
            {
                patchedMethods = patched,
                hotMethods = perMethod,
                methodsPatchedByOwner = owners.OrderByDescending(o => o.Value).Take(30).ToDictionary(o => o.Key, o => o.Value)
            };
        }
    }
}
