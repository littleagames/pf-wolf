namespace Wolf3D.Extensions;

internal static class StringExtensions
{
    extension(int)
    {
        public static string SecondsAsTime(int value)
        {
            var minutes = value / 60;
            if (minutes > 99)
                minutes = 99;

            var seconds = value % 60;

            return $"{minutes:00}:{seconds:00}";
        }
    }
}
