using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace ValleyFixes.Fixes
{
    /// <summary>VF-007: corrects the third place prize in the Fair speech, in every language, without touching the reward.</summary>
    internal sealed class FairTokenTextFix
    {
        private const string AssetName = "Strings/StringsFromCSFiles";

        private readonly IMonitor _monitor;
        private readonly HashSet<string> _reported = new();

        public FairTokenTextFix(IMonitor monitor)
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

            // late, so a translation or text mod that already changed the line is judged by what it left behind
            string locale = e.Name.LocaleCode ?? "default";
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                if (!data.TryGetValue(FairTokenText.Key, out string? text))
                {
                    ReportOnce(locale, "the speech line is missing");
                    return;
                }

                if (FairTokenText.TryFix(text, out string fixedText))
                    data[FairTokenText.Key] = fixedText;
                else
                    ReportOnce(locale, "the line no longer holds a single 200, so it was left as it is");
            }, AssetEditPriority.Late);
        }

        private void ReportOnce(string locale, string reason)
        {
            if (_reported.Add(locale))
                _monitor.Log($"Fair third place text fix skipped for '{locale}': {reason}.", LogLevel.Trace);
        }
    }
}
