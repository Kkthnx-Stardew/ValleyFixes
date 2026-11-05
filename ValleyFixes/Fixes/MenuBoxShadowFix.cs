using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley.Menus;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-064: every menu box (item tooltips, dialogue boxes, the toolbar tooltip) draws its drop shadow from eight pieces, but
    /// <c>IClickableMenu.drawTextureBox</c> only draws three of the four corner pieces. The top left one is never drawn, so the shadow
    /// has a notch about 8 by 20 pixels (at 4x zoom) just under the top left corner of the box.
    /// This draws the missing piece, in the same place and with the same colour and layer as its three siblings, before the game draws
    /// the rest. It is a prefix that never skips the original, so the draw order matches the game's own. It is skipped if another mod
    /// already patches the method, which covers other mods that repair the same shadow.
    /// </summary>
    internal static class MenuBoxShadowFix
    {
        public static void Apply(Harmony harmony, IMonitor monitor)
        {
            var target = AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.drawTextureBox), new[]
            {
                typeof(SpriteBatch), typeof(Texture2D), typeof(Rectangle), typeof(int), typeof(int), typeof(int), typeof(int), typeof(Color), typeof(float), typeof(bool), typeof(float)
            });
            if (target is null)
            {
                monitor.Log("Menu box shadow fix is off: IClickableMenu.drawTextureBox was not found, so the game has changed.", LogLevel.Warn);
                return;
            }

            var others = HarmonyOwners.Foreign(target);
            if (others.Count > 0)
            {
                monitor.Log($"Menu box shadow fix is off: {string.Join(", ", others)} already patches IClickableMenu.drawTextureBox, and two patches on one method could interfere.", LogLevel.Info);
                return;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(typeof(MenuBoxShadowFix), nameof(Before)));
        }

        private static void Before(SpriteBatch b, Texture2D texture, Rectangle sourceRect, int x, int y, float scale, bool drawShadow, float draw_layer)
        {
            if (!drawShadow)
                return;

            try
            {
                // the same depth the game uses for the other shadow pieces
                float layerDepth = draw_layer < 0f ? 0.77f : draw_layer - 0.03f;
                int piece = sourceRect.Width / 3;
                b.Draw(texture, new Vector2(x - 8, y + 8), new Rectangle(sourceRect.X, sourceRect.Y, piece, piece), Color.Black * 0.4f, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);
            }
            catch
            {
                // a cosmetic fix, so it never gets in the way of drawing a menu
            }
        }
    }
}
