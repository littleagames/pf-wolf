namespace Wolf3D;

internal partial class Program
{
    internal static void CA_WriteFile(string filename, byte[] data, int length)
    {
        try
        {
            using FileStream fs = File.Create(filename);
            using BinaryWriter br = new BinaryWriter(fs);
            {
                br.Write(data);
            }
        }
        catch (FileNotFoundException fnfEx)
        {
            _gameEngineManager.Quit($"Can't open {filename}!");
            return;
        }
        catch (IOException ioEx)
        {
            _gameEngineManager.Quit($"Error writing file {filename}: {ioEx.Message}");
            return;
        }
    }
}
