using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Quests;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-070: an item delivery quest that asks for a cooked dish, for anyone but the Wizard, has two ways to write its text. One of them
    /// (a 33% draw) writes the text and leaves the arrays of lines the other way uses as null, and the code after both then reads the
    /// length of that null array and throws. The random draw is seeded by the save and the day, so a given save either hits it on a given
    /// day or never does. The game's developers list "intermittent crash when an item delivery quest for a non-Wizard NPC requests a
    /// cooking-type item" as fixed in 1.6.16.
    /// The branch that wrote its text now jumps straight to the code that builds the quest dialogue, which is where the original code was
    /// meant to continue, instead of falling into the code that needs the arrays. Every other path through the method is untouched. If the
    /// method does not look the way it was read, nothing is patched.
    /// </summary>
    internal static class DeliveryQuestCookingFix
    {
        private static IMonitor _monitor = null!; // set in Apply

        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            _monitor = monitor;

            var target = AccessTools.Method(typeof(ItemDeliveryQuest), nameof(ItemDeliveryQuest.loadQuestInfo));
            if (target is null)
            {
                monitor.Log("Delivery quest fix is off: ItemDeliveryQuest.loadQuestInfo was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            var others = HarmonyOwners.Foreign(target);
            if (others.Count > 0)
            {
                monitor.Log($"Delivery quest fix is off: {string.Join(", ", others)} already patches ItemDeliveryQuest.loadQuestInfo, and two patches on one method could interfere.", LogLevel.Info);
                return;
            }

            harmony.Patch(target, transpiler: new HarmonyMethod(typeof(DeliveryQuestCookingFix), nameof(Transpile)));
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
        {
            var original = instructions.ToList();
            var codes = original.ToList();
            try
            {
                var dialogueParts = AccessTools.Field(typeof(ItemDeliveryQuest), nameof(ItemDeliveryQuest.dialogueparts));

                // the first 33% draw is the cooking branch, the second is a different one further down
                int draw = codes.FindIndex(c => c.opcode == OpCodes.Ldc_R8 && c.operand is double d && Math.Abs(d - 0.33) < 1e-9);
                // its text starts with this line number, and the jump out of the branch is the first jump after that
                int text = draw < 0 ? -1 : codes.FindIndex(draw, c => c.opcode == OpCodes.Ldstr && c.operand is string s && s == "13333");
                int jump = text < 0 ? -1 : codes.FindIndex(text, c => c.opcode == OpCodes.Br || c.opcode == OpCodes.Br_S);
                // the quest dialogue is built where dialogueparts is first cleared after that branch
                int dialogue = draw < 0 ? -1 : codes.FindIndex(draw, c => c.opcode == OpCodes.Ldfld && Equals(c.operand, dialogueParts));
                if (jump < 0 || dialogue < 1 || codes[dialogue - 1].opcode != OpCodes.Ldarg_0 || codes[dialogue - 1].labels.Count == 0)
                {
                    _monitor.Log("Delivery quest fix is off: ItemDeliveryQuest.loadQuestInfo does not look the way it was read, so the game has changed.", LogLevel.Warn);
                    return original;
                }

                Label dialogueLabel = codes[dialogue - 1].labels[0];
                if (codes[jump].operand is Label current && current == dialogueLabel)
                {
                    _monitor.Log("Delivery quest fix is not needed, the branch already jumps to the quest dialogue.", LogLevel.Info);
                    return original;
                }

                codes[jump] = new CodeInstruction(OpCodes.Br, dialogueLabel) { labels = codes[jump].labels, blocks = codes[jump].blocks };
                _monitor.Log("Delivery quest fix is on. Cooking item delivery quests no longer fail when their text takes the short form.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Delivery quest fix could not be applied, so the method is left as the game wrote it. {ex.Message}", LogLevel.Warn);
                return original;
            }

            return codes;
        }
    }
}
