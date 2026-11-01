using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-036: in Portuguese, single male farmers are listed with the female "single" text on the social page.
    /// Portuguese is the only language in the base game that uses the gendered path, so only that locale is edited.
    /// </summary>
    internal sealed class PortugueseSingleStatusFix
    {
        private const string AssetName = "Strings/StringsFromCSFiles";
        private const string Locale = "pt-BR";

        private readonly IMonitor _monitor;

        public PortugueseSingleStatusFix(IMonitor monitor)
        {
            _monitor = monitor;
        }

        public void Register(IModEvents events)
        {
            events.Content.AssetRequested += OnAssetRequested;
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo(AssetName) || e.Name.LocaleCode != Locale)
                return;

            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                data.TryGetValue(GenderedSingleStatus.FemaleKey, out string? female);
                data.TryGetValue(GenderedSingleStatus.MaleKey, out string? male);

                if (GenderedSingleStatus.TryFix(female, male, out string combined))
                    data[GenderedSingleStatus.FemaleKey] = combined;
                else
                    _monitor.Log("Portuguese single status fix skipped: the strings no longer look like the expected ones.", LogLevel.Trace);
            }, AssetEditPriority.Late);
        }
    }
}
