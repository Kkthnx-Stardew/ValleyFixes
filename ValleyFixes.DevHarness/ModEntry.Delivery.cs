using HarmonyLib;
using StardewValley;
using StardewValley.Quests;

namespace ValleyFixes.DevHarness
{
    /// <summary>Test only. Makes the game's random item pick for quests hand out cooked dishes, so the real quest code runs with one.</summary>
    internal static class ForceDishPick
    {
        private static readonly string[] Dishes = { "216", "194", "195", "196", "198", "199", "200", "201", "204", "205" };
        private static int _next;

        public static void Install()
        {
            var target = AccessTools.Method(typeof(Utility), nameof(Utility.getRandomItemFromSeason), new[] { typeof(Season), typeof(int), typeof(bool), typeof(bool) });
            new Harmony("Kkthnx.ValleyFixes.DevHarness.dishes").Patch(target, postfix: new HarmonyMethod(typeof(ForceDishPick), nameof(After)));
        }

        private static void After(bool forQuest, ref string __result)
        {
            if (forQuest)
                __result = Dishes[_next++ % Dishes.Length];
        }
    }

    /// <summary>
    /// Delivery quest scenario. Builds the quest for every day of 600 and each season with the real quest code, once per combination
    /// (the quest random is seeded by the save and the day), and records which ones throw, which asked for a cooked dish, and a hash of
    /// the text of every quest that did not throw so a fix can be compared against the game's own output.
    /// </summary>
    public partial class ModEntry
    {
        private static string QuestText(ItemDeliveryQuest quest)
        {
            try
            {
                return string.Join("|", quest.parts.Select(p => p.loadDescriptionElement())) + "#" + string.Join("|", quest.dialogueparts.Select(p => p.loadDescriptionElement())) + "#" + quest.target.Value + "#" + quest.ItemId.Value + "#" + quest.moneyReward.Value;
            }
            catch
            {
                return "unreadable";
            }
        }

        private IEnumerable<int> DeliveryScenario()
        {
            var info = new Dictionary<string, object?>();
            _results["delivery"] = info;
            yield return 120;

            foreach (var key in Game1.characterData.Keys)
                if (!Game1.player.friendshipData.ContainsKey(key))
                    Game1.player.friendshipData.Add(key, new Friendship());

            // the requested item comes from the recipes the players know, so a well played save asks for cooked dishes often
            foreach (var recipe in DataLoader.CookingRecipes(Game1.content).Keys)
                if (!Game1.player.cookingRecipes.ContainsKey(recipe))
                    Game1.player.cookingRecipes.Add(recipe, 0);
            info["cookingRecipesKnown"] = Game1.player.cookingRecipes.Count();

            // a throwaway farm gets a new random id every run, and the quest random is seeded by it, so it is pinned to compare runs
            var savedId = Game1.uniqueIDForThisGame;
            Game1.uniqueIDForThisGame = 987654321UL;
            var itemTypes = new Dictionary<string, int>();
            var perQuest = new Dictionary<string, string>();
            ForceDishPick.Install();
            var savedSeason = Game1.season;
            var savedDays = Game1.stats.DaysPlayed;
            int tried = 0, crashed = 0, cooking = 0, cookingCrashed = 0, emptyText = 0;
            var examples = new List<string>();
            ulong hashNotThrown = 14695981039346656037UL;
            ulong hashAll = 14695981039346656037UL;
            foreach (var season in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
            {
                for (uint day = 1; day <= 600; day++)
                {
                    Game1.season = season;
                    Game1.stats.DaysPlayed = day;
                    var quest = new ItemDeliveryQuest();
                    tried++;
                    bool threw = false;
                    try
                    {
                        quest.loadQuestInfo();
                    }
                    catch (NullReferenceException)
                    {
                        threw = true;
                        crashed++;
                    }

                    bool isCooking = false;
                    try
                    {
                        string type = "none";
                        if (quest.ItemId.Value is not null && ItemRegistry.Create(quest.ItemId.Value) is StardewValley.Object o)
                        {
                            type = o.Type ?? "null";
                            isCooking = o.Type == "Cooking" && quest.target.Value != "Wizard";
                        }
                        itemTypes[type] = itemTypes.GetValueOrDefault(type) + 1;
                    }
                    catch
                    {
                    }
                    if (isCooking)
                    {
                        cooking++;
                        if (threw)
                        {
                            cookingCrashed++;
                            if (examples.Count < 12)
                                examples.Add($"{season} day {day} {quest.target.Value} {quest.ItemId.Value}");
                        }
                    }

                    string text = QuestText(quest);
                    ulong own = 14695981039346656037UL;
                    foreach (char c in text)
                    {
                        own ^= c;
                        own *= 1099511628211UL;
                    }
                    perQuest[$"{season}-{day}"] = threw ? "threw" : own.ToString("x16");
                    if (!threw && quest.parts.Count == 0)
                        emptyText++;
                    foreach (char c in $"{season}{day}{text}")
                    {
                        hashAll ^= c;
                        hashAll *= 1099511628211UL;
                        if (!threw)
                        {
                            hashNotThrown ^= c;
                            hashNotThrown *= 1099511628211UL;
                        }
                    }
                }
                yield return 1;
            }

            Game1.season = savedSeason;
            Game1.stats.DaysPlayed = savedDays;
            Game1.uniqueIDForThisGame = savedId;
            info["requestedItemTypes"] = itemTypes;
            info["perQuest"] = perQuest;
            info["tried"] = tried;
            info["threwNullReference"] = crashed;
            info["cookingNonWizard"] = cooking;
            info["cookingThatThrew"] = cookingCrashed;
            info["questsWithNoText"] = emptyText;
            info["hashOfQuestsThatDidNotThrow"] = hashNotThrown.ToString("x16");
            info["hashOfAll"] = hashAll.ToString("x16");
            info["examples"] = examples;
        }
    }
}
