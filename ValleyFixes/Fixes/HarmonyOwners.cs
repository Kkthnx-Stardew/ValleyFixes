using System.Reflection;
using HarmonyLib;

namespace ValleyFixes.Fixes
{
    /// <summary>Finds which other mods already patch a game method, so a fix can stand aside instead of stacking on top of them.</summary>
    internal static class HarmonyOwners
    {
        private const string OwnIdPrefix = "Kkthnx.ValleyFixes";

        /// <summary>The distinct owner ids of every patch on the method, leaving out ValleyFixes itself and its test tool.</summary>
        public static List<string> Foreign(MethodBase method)
        {
            var info = Harmony.GetPatchInfo(method);
            if (info is null)
                return new List<string>();

            return info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers)
                .Select(p => p.owner)
                .Where(owner => !owner.StartsWith(OwnIdPrefix, StringComparison.Ordinal))
                .Distinct()
                .ToList();
        }
    }
}
