using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace ValleyFixes.Fixes
{
    /// <summary>
    /// VF-047: George's wheelchair wheels do not turn when he walks up in the normal sprite sheet. All four frames of that row are
    /// identical, while the winter sheet has the wheel in two positions. The art in the wheel area is the same in both sheets for the frames
    /// that match, so the missing wheel frames are copied from the winter sheet at load time. Nothing is drawn or shipped by this mod.
    /// It copies nothing unless the sheets look exactly like vanilla, so a sprite replacement mod is left alone.
    /// </summary>
    internal sealed class GeorgeWheelchairFix
    {
        private const string BaseAsset = "Characters/George";
        private const string DonorAsset = "Characters/George_Winter";

        // the walking up row is the third row, each frame is 16 by 32, the wheel is the bottom 14 rows
        private const int FrameWidth = 16;
        private const int FrameHeight = 32;
        private const int UpRow = 2;
        private const int WheelTop = 18;

        private readonly IMonitor _monitor;
        private readonly IGameContentHelper _content;

        public GeorgeWheelchairFix(IMonitor monitor, IGameContentHelper content)
        {
            _monitor = monitor;
            _content = content;
        }

        public void Register(IModEvents events)
        {
            events.Content.AssetRequested += OnAssetRequested;
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(BaseAsset))
                e.Edit(Edit, AssetEditPriority.Late);
        }

        private static Rectangle Wheel(int column)
        {
            return new Rectangle(column * FrameWidth, UpRow * FrameHeight + WheelTop, FrameWidth, FrameHeight - WheelTop);
        }

        private static uint[] Read(Texture2D texture, Rectangle area)
        {
            var pixels = new Color[area.Width * area.Height];
            texture.GetData(0, area, pixels, 0, pixels.Length);
            return pixels.Select(c => c.PackedValue).ToArray();
        }

        private void Edit(IAssetData asset)
        {
            try
            {
                var target = asset.AsImage();
                Texture2D donor = _content.Load<Texture2D>(DonorAsset);
                if (target.Data.Width < 4 * FrameWidth || target.Data.Height < (UpRow + 1) * FrameHeight || donor.Width < 4 * FrameWidth || donor.Height < (UpRow + 1) * FrameHeight)
                    return;

                uint[] first = Read(target.Data, Wheel(0));
                uint[] third = Read(target.Data, Wheel(2));
                uint[] donorFirst = Read(donor, Wheel(0));
                uint[] donorThird = Read(donor, Wheel(2));

                int copied = 0;
                foreach (int column in new[] { 1, 3 })
                {
                    uint[] frame = Read(target.Data, Wheel(column));
                    uint[] donorFrame = Read(donor, Wheel(column));
                    if (!SpriteFrames.ShouldCopyFromDonor(first, frame, third, donorFirst, donorFrame, donorThird))
                        continue;

                    target.PatchImage(donor, Wheel(column), Wheel(column), PatchMode.Replace);
                    copied++;
                }

                _monitor.Log(copied > 0 ? $"George wheelchair fix copied {copied} of 2 missing wheel frames." : "George wheelchair fix found nothing to change (the sheets are not the vanilla ones, or already fixed).", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                _monitor.Log($"George wheelchair fix failed and left the sprite alone. {ex.Message}", LogLevel.Warn);
            }
        }
    }
}
