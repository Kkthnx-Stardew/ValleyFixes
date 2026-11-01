namespace ValleyFixes.Tests
{
    /// <summary>Finds the unpacked game content next to the installed game, or null when it is not there.</summary>
    internal static class GameContent
    {
        public static string? Find(string folder)
        {
            string? game = Environment.GetEnvironmentVariable("STARDEW_GAME_PATH") ?? @"D:\SteamLibrary\steamapps\common\Stardew Valley";
            string path = Path.Combine(game, "Content (unpacked)", folder);
            return Directory.Exists(path) ? path : null;
        }
    }
}
