using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace ValleyFixes.DevHarness
{
    /// <summary>The slice of the Generic Mod Config Menu API needed to open a mod's page.</summary>
    public interface IGmcmOpen
    {
        void OpenModMenu(IManifest mod);
    }

    /// <summary>
    /// Config menu scenario. Opens ValleyFixes' own Generic Mod Config Menu page in the running game, draws it into an off-screen
    /// target and writes a PNG at each scroll position, so the page can be read the way a player sees it.
    /// </summary>
    public partial class ModEntry
    {
        private IEnumerable<int> ConfigShotScenario()
        {
            var info = new Dictionary<string, object?>();
            _results["config_menu"] = info;
            yield return 120;

            var api = Helper.ModRegistry.GetApi<IGmcmOpen>("spacechase0.GenericModConfigMenu");
            var mod = Helper.ModRegistry.Get("Kkthnx.ValleyFixes");
            if (api is null || mod is null)
            {
                info["error"] = $"api {(api is null ? "missing" : "ok")}, mod {(mod is null ? "missing" : "ok")}";
                yield break;
            }

            api.OpenModMenu(mod.Manifest);
            yield return 30;
            var menu = Game1.activeClickableMenu;
            info["menu"] = menu?.GetType().FullName;
            if (menu is null)
                yield break;

            var b = Game1.spriteBatch;
            int width = Game1.uiViewport.Width, height = Game1.uiViewport.Height;
            info["viewport"] = $"{width}x{height}";
            string dir = Path.GetDirectoryName(_out!)!;
            string tag = Environment.GetEnvironmentVariable("VFH_TAG") ?? "run";
            int shots = 0;
            for (int i = 0; i < 24; i++)
            {
                menu = Game1.activeClickableMenu!;
                var target = new RenderTarget2D(Game1.graphics.GraphicsDevice, width, height);
                Game1.graphics.GraphicsDevice.SetRenderTarget(target);
                Game1.graphics.GraphicsDevice.Clear(new Color(40, 40, 40));
                b.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, null);
                menu.draw(b);
                b.End();
                Game1.graphics.GraphicsDevice.SetRenderTarget(null);
                using (var fs = File.Create(Path.Combine(dir, $"config-{tag}-{i:00}.png")))
                    target.SaveAsPng(fs, width, height);
                target.Dispose();
                shots++;

                // scroll the page one screen down, the same call the mouse wheel makes
                for (int k = 0; k < 6; k++)
                    menu.receiveScrollWheelAction(-120);
                yield return 5;
            }
            info["screenshots"] = shots;
            Game1.exitActiveMenu();
        }
    }
}
