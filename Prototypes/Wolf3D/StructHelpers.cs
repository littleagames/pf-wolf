using System.Runtime.InteropServices;

namespace Wolf3D;

internal static class StructHelpers
{
    internal static byte[] StructToBytes<T>(this T value) where T : struct
    {
        // Create a span over the single struct instance
        Span<T> structSpan = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref value, 1);

        // Cast the struct span to a byte span
        Span<byte> byteSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(structSpan);

        // Copy to a new byte array to return
        byte[] result = new byte[byteSpan.Length];
        byteSpan.CopyTo(result);
        return result;
    }

    internal static byte[] StructArrayToBytes<T>(this T[] value) where T : struct
    {
        if (value == null || value.Length == 0)
        {
            return Array.Empty<byte>();
        }

        // Create a span over the array contents
        Span<T> structSpan = System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref value[0], value.Length);

        // Cast the struct span to a byte span
        Span<byte> byteSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(structSpan);

        // Copy to a new byte array to return
        byte[] result = new byte[byteSpan.Length];
        byteSpan.CopyTo(result);
        return result;
    }

    internal static T BytesToStruct<T>(byte[] bytes) where T : struct
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        int structSize = Marshal.SizeOf<T>();
        if (structSize <= 0 || bytes.Length != structSize)
            throw new ArgumentException("Byte array length must exactly match the size of the target struct.", nameof(bytes));

        var span = new ReadOnlySpan<byte>(bytes);
        return MemoryMarshal.Read<T>(span);
    }

    internal static T[] BytesToStructArray<T>(byte[] bytes) where T : struct
    {
        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        int structSize = Marshal.SizeOf<T>();
        if (structSize == 0 || (bytes.Length % structSize) != 0)
            throw new ArgumentException("Byte array length is not a multiple of the struct size.", nameof(bytes));

        int count = bytes.Length / structSize;
        var result = new T[count];
        var destBytes = MemoryMarshal.AsBytes(result.AsSpan());
        new ReadOnlySpan<byte>(bytes).CopyTo(destBytes);
        return result;
    }
}
