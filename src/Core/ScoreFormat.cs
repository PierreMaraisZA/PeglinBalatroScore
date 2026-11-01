using System.Globalization;

namespace PeglinBalatroScore.Core
{
    public static class ScoreFormat
    {
        /// <summary>From here on numbers are written like 1.23e8, the way the game writes huge damage.</summary>
        public const long ScientificFrom = 100_000_000;

        /// <summary>"2,410" style numbers, "1.23e8" once they get too wide for the box, "∞" for infinite damage.</summary>
        public static string Format(long value)
        {
            if (value <= 0)
            {
                return "0";
            }
            if (value == long.MaxValue)
            {
                return "∞";
            }
            if (value < ScientificFrom)
            {
                return value.ToString("N0", CultureInfo.InvariantCulture);
            }
            return value.ToString("0.00e0", CultureInfo.InvariantCulture);
        }
    }
}
