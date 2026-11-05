using System.Runtime.CompilerServices;
using HarmonyLib;
using StardewModdingAPI;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-063: joining a host by IP address or on a local network starts with one UDP packet that asks the host for its farmhand list.
    /// The game's <c>LidgrenClient</c> has the code to send that request again, but its retry limit (<c>maxRetryAttempts</c>) is 0,
    /// so a lost request or a lost answer is never repeated. The player then sits on the connecting screen for the full 45 seconds
    /// (<c>Client.connectionTimeout</c>) and gets a timeout. The game's retry also cannot work with a port typed after the address,
    /// because <c>attemptConnection</c> cuts the port off <c>address</c> on its first call, so a second call would go to the default
    /// port. Here the retry limit is set to 3 with a 3 second wait, and the address is put back before each attempt.
    /// Once the host answers, nothing changes. The patch is skipped if another mod already patches the same game members.
    /// </summary>
    internal static class LidgrenDiscoveryRetryFix
    {
        private const int Retries = 3;
        private const int RetryMs = 3000;

        private static readonly ConditionalWeakTable<object, string> OriginalAddress = new();
        private static readonly ConditionalWeakTable<object, StrongBox<int>> Attempts = new();
        private static AccessTools.FieldRef<object, int>? _maxRetry;
        private static AccessTools.FieldRef<object, int>? _retryMs;
        private static AccessTools.FieldRef<object, string>? _address;
        private static IMonitor _monitor = null!; // set in Apply

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            var type = AccessTools.TypeByName("StardewValley.Network.LidgrenClient");
            var ctor = type is null ? null : AccessTools.Constructor(type, new[] { typeof(string) });
            var attempt = type is null ? null : AccessTools.Method(type, "attemptConnection");
            var maxRetry = type is null ? null : AccessTools.Field(type, "maxRetryAttempts");
            var retryMs = type is null ? null : AccessTools.Field(type, "retryMs");
            var address = type is null ? null : AccessTools.Field(type, "address");
            if (type is null || ctor is null || attempt is null || maxRetry is null || retryMs is null || address is null
                || maxRetry.FieldType != typeof(int) || retryMs.FieldType != typeof(int) || address.FieldType != typeof(string))
            {
                monitor.Log("Join retry is off: the game's LidgrenClient does not have the members this fix needs, so the game has changed.", LogLevel.Warn);
                return;
            }

            var others = HarmonyOwners.Foreign(ctor).Concat(HarmonyOwners.Foreign(attempt)).Distinct().ToList();
            if (others.Count > 0)
            {
                monitor.Log($"Join retry is off: {string.Join(", ", others)} already patches LidgrenClient, and two patches on one method could interfere.", LogLevel.Info);
                return;
            }

            _maxRetry = AccessTools.FieldRefAccess<object, int>(maxRetry);
            _retryMs = AccessTools.FieldRefAccess<object, int>(retryMs);
            _address = AccessTools.FieldRefAccess<object, string>(address);

            harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(LidgrenDiscoveryRetryFix), nameof(AfterCreated)));
            harmony.Patch(attempt, prefix: new HarmonyMethod(typeof(LidgrenDiscoveryRetryFix), nameof(BeforeAttempt)));
        }

        private static void AfterCreated(object __instance, string address)
        {
            try
            {
                OriginalAddress.Remove(__instance);
                OriginalAddress.Add(__instance, address ?? "");
                _maxRetry!(__instance) = Retries;
                _retryMs!(__instance) = RetryMs;
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not set up the join retry. {ex.Message}", LogLevel.Error);
            }
        }

        private static void BeforeAttempt(object __instance)
        {
            try
            {
                // the game cuts the port off the address on the first attempt, so each attempt starts from the text the player typed
                if (OriginalAddress.TryGetValue(__instance, out var original))
                    _address!(__instance) = original;

                if (Attempts.TryGetValue(__instance, out var box))
                {
                    box.Value++;
                    _monitor.Log($"The host has not answered the join request yet, so it is sent again (attempt {box.Value}).", LogLevel.Info);
                }
                else
                {
                    Attempts.Add(__instance, new StrongBox<int>(1));
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Could not restore the host address before a join attempt. {ex.Message}", LogLevel.Error);
            }
        }
    }
}
