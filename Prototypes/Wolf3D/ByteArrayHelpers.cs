using System.Diagnostics;
using System.IO;

namespace Wolf3D;

internal static class ByteArrayHelpers
{
    public static void Write(this BinaryWriter writer, byte[,] data)
    {
        // Optional: write dimensions first so you know how to read it back
        //writer.Write(data.GetLength(0)); // Length of dimension 0 (rows)
        //writer.Write(data.GetLength(1)); // Length of dimension 1 (columns)

        for (int i = 0; i < data.GetLength(0); i++)
        {
            for (int j = 0; j < data.GetLength(1); j++)
            {
                writer.Write(data[i, j]);
            }
        }
    }

    public static byte[] Flatten(this byte[,] input)
    {
        int rows = input.GetLength(0);
        int cols = input.GetLength(1);

        int size = rows * cols;
        byte[] flat = new byte[size];
        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                flat[idx++] = input[r, c];
            }
        }

        return flat;
    }

    public static byte[,] ToFixedArray(this byte[] input, int xSize, int ySize)
    {
        Debug.Assert(xSize * ySize > 0 && xSize * ySize == input.Length, "Invalid fixed array size");

        byte[,] fixedArray = new byte[xSize, ySize];

        int idx = 0;
        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                fixedArray[x, y] = input[idx++];
            }
        }

        return fixedArray;
    }
}
