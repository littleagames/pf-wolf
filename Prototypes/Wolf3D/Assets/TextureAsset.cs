using System;
using System.Collections.Generic;
using System.Text;

namespace Wolf3D.Assets
{
    /// <summary>
    /// A wall texture, stored column by column: column x starts at x * Height, top pixel first.
    /// VSWAP walls are 64x64. Ones from a pack's textures/ folder can be any size: a taller one
    /// spans a story per 64 pixels, from the floor up.
    /// </summary>
    internal record TextureAsset : Asset
    {
        public const int StorySize = 64;

        public int Width { get; init; } = StorySize;
        public int Height { get; init; } = StorySize;

        /// <summary>A texture from a picture, which is stored row by row.</summary>
        public static TextureAsset FromGraphic(GraphicAsset graphic)
        {
            int width = graphic.Width, height = graphic.Height;
            var columns = new byte[width * height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    columns[x * height + y] = graphic.RawData[y * width + x];

            return new TextureAsset { RawData = columns, Width = width, Height = height };
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

        public override void Merge(Asset other)
        {
            // For now, do nothing
        }
    }
}
