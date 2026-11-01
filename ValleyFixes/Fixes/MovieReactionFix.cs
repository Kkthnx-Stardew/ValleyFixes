using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Movies;

namespace ValleyFixes.Fixes
{
    /// <summary>VF-001: removes the stray NPC name from three movie reaction scripts so the real text is shown.</summary>
    internal sealed class MovieReactionFix
    {
        private const string AssetName = "Data/MoviesReactions";

        private readonly IMonitor _monitor;

        public MovieReactionFix(IMonitor monitor)
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
                int fixedCount = 0;
                foreach (MovieCharacterReaction? character in asset.GetData<List<MovieCharacterReaction>>())
                {
                    if (character?.Reactions is null)
                        continue;

                    foreach (MovieReaction? reaction in character.Reactions)
                    {
                        var during = reaction?.SpecialResponses?.DuringMovie;
                        if (during is null)
                            continue;

                        if (MovieScripts.TryFix(character.NPCName, reaction!.Tag, during.Script, out string fixedScript))
                        {
                            during.Script = fixedScript;
                            fixedCount++;
                        }
                    }
                }

                _monitor.Log($"Movie reaction fix changed {fixedCount} of 3 expected scripts.", fixedCount == 3 ? LogLevel.Trace : LogLevel.Debug);
            }, AssetEditPriority.Late);
        }
    }
}
