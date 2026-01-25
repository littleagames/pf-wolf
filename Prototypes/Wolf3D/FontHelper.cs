namespace Wolf3D;

internal static class FontHelper
{
    internal static fontstruct GetFont(byte[] data)
    {
        int dataIndex = 0;
        fontstruct font = new fontstruct();
        font.height = BitConverter.ToInt16(data, dataIndex);
        dataIndex += sizeof(short);
        for (int i = 0; i < font.location.Length; i++)
        {
            font.location[i] = BitConverter.ToInt16(data, dataIndex);
            dataIndex += sizeof(short);
        }

        for (int j = 0; j < font.width.Length; j++)
        {
            font.width[j] = data[dataIndex];
            dataIndex += sizeof(byte);
        }

        return font;
    } 
}
