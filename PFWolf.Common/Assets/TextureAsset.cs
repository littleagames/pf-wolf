using System;
using System.Collections.Generic;
using System.Text;

namespace PFWolf.Assets
{
    /// <summary>
    /// A WallId texture, stored column by column: column x starts at x * Height, top pixel first.
    /// VSWAP walls are 64x64. Ones from a pack's textures/ folder can be any size: a taller one
    /// spans a story per 64 pixels, from the floor up.
    /// </summary>
    public record TextureAsset : Asset
    {
        public const int StorySize = 64;

        public int Width { get; init; } = StorySize;
        public int Height { get; init; } = StorySize;

        /// <summary>
        /// Made from a picture with clear pixels (now 0xFF), which a DoorNumber jamb shows its WallId
        /// through. VSWAP walls never are: 0xFF is an ordinary color in them.
        /// </summary>
        public bool Masked { get; init; }

        /// <summary>A texture from a picture, which is stored row by row.</summary>
        public static TextureAsset FromGraphic(GraphicAsset graphic)
        {
            int width = graphic.Width, height = graphic.Height;
            // Clear pixels become 0xFF, which flats show through (as sprites do)
            var columns = new byte[width * height];
            var mask = graphic.OpacityMask;
            bool masked = false;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    bool clear = mask != null && mask[y * width + x] == 0;
                    masked |= clear;
                    columns[x * height + y] = clear ? (byte)0xff : graphic.RawData[y * width + x];
                }

            return new TextureAsset { RawData = columns, Width = width, Height = height, Masked = masked };
        }

        /// <summary>
        /// The bottom story as a 64x64 texture (itself, if that's all it is), with the columns
        /// scaled to fit, for things that only show one story, like the automap.
        /// </summary>
        public byte[] BottomStory()
        {
            if (Width == StorySize && Height == StorySize)
                return RawData;

            var story = new byte[StorySize * StorySize];
            int rows = Math.Min(Height, StorySize);
            for (int x = 0; x < StorySize; x++)
            {
                int column = x * Width / StorySize * Height;
                for (int y = 0; y < StorySize; y++)
                    story[x * StorySize + y] = RawData[column + Height - rows + y * rows / StorySize];
            }
            return story;
        }
    }
}
