using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.GameData.FarmAnimals;
using StardewValley.Menus;

namespace ValleyFixes.DevHarness
{
    /// <summary>
    /// Shadow scenario. Draws a menu box and every farm animal into an off-screen target and writes PNG files next to the results file,
    /// so two reported shadow bugs (menu box corner, barn animals with two shadows) can be looked at and counted in pixels.
    /// </summary>
    public partial class ModEntry
    {
        private const int ShadowCanvasW = 960;
        private const int ShadowCanvasH = 540;

        private RenderTarget2D BeginShadowCanvas(SpriteBatch b, Color background)
        {
            var target = new RenderTarget2D(Game1.graphics.GraphicsDevice, ShadowCanvasW, ShadowCanvasH);
            Game1.graphics.GraphicsDevice.SetRenderTarget(target);
            Game1.graphics.GraphicsDevice.Clear(background);
            b.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, null);
            return target;
        }

        private void EndShadowCanvas(SpriteBatch b, RenderTarget2D target, string name)
        {
            b.End();
            Game1.graphics.GraphicsDevice.SetRenderTarget(null);
            string dir = Path.GetDirectoryName(_out!)!;
            using (var fs = File.Create(Path.Combine(dir, name)))
                target.SaveAsPng(fs, target.Width, target.Height);
            target.Dispose();
        }

        private IEnumerable<int> ShadowScenario()
        {
            var info = new Dictionary<string, object?>();
            _results["shadows"] = info;
            string tag = Environment.GetEnvironmentVariable("VFH_TAG") ?? "run";
            yield return 120;

            var b = Game1.spriteBatch;

            // menu box: the same call the item tooltip uses
            var target = BeginShadowCanvas(b, new Color(210, 200, 180));
            // a clear sprite changes only the number of sprites in the batch, to show whether equal-depth sprites change order with the count
            if (Environment.GetEnvironmentVariable("VFH_DUMMY") == "1")
                b.Draw(Game1.staminaRect, new Rectangle(900, 500, 1, 1), null, Color.Transparent, 0f, Vector2.Zero, SpriteEffects.None, 0.5f);
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), 120, 120, 320, 200, Color.White);
            IClickableMenu.drawHoverText(b, "Scythe\nCuts grass and crops.", Game1.smallFont, 0, 0, -1, "Scythe", overrideX: 520, overrideY: 140);
            EndShadowCanvas(b, target, $"shadows-{tag}-box.png");
            yield return 5;

            // each animal, adult and baby, on a flat grass colour
            var animalData = Game1.farmAnimalData;
            var names = animalData.Keys.OrderBy(k => k).ToList();
            var rows = new List<object>();
            info["animals"] = rows;
            var farm = Game1.getFarm();
            Vector2 center = new Vector2(480, 270);
            var savedViewport = Game1.viewport;
            int idx = 0;
            foreach (string type in names)
            {
                FarmAnimal animal;
                try { animal = new FarmAnimal(type, Game1.Multiplayer.getNewID(), Game1.player.UniqueMultiplayerID); }
                catch (Exception ex) { rows.Add(new { type, error = ex.Message }); continue; }
                animal.currentLocation = farm;
                var data = animal.GetAnimalData();
                foreach (bool baby in new[] { false, true })
                {
                    animal.age.Value = baby ? 0 : (data?.DaysToMature ?? 3) + 1;
                    animal.ReloadTextureIfNeeded();
                    animal.Position = new Vector2(2000 + idx * 200, 2000);
                    Game1.viewport = new xTile.Dimensions.Rectangle((int)animal.Position.X - 480 + 64, (int)animal.Position.Y - 270 + 64, ShadowCanvasW, ShadowCanvasH);
                    var t2 = BeginShadowCanvas(b, new Color(96, 160, 64));
                    animal.draw(b);
                    EndShadowCanvas(b, t2, $"shadows-{tag}-animal-{type.Replace(' ', '_')}-{(baby ? "baby" : "adult")}.png");
                    var sh = data?.GetShadow(baby, false);
                    rows.Add(new { type, baby, shadowVisible = sh?.Visible, shadowScale = sh?.Scale, offset = sh?.Offset?.ToString(), spriteW = animal.Sprite?.SpriteWidth, textureW = animal.Sprite?.Texture?.Width, textureH = animal.Sprite?.Texture?.Height });
                    yield return 2;
                }
                idx++;
            }
            Game1.viewport = savedViewport;
        }
    }
}
