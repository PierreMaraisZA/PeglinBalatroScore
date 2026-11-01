using System;

namespace PeglinBalatroScore.Core
{
    /// <summary>
    /// A small "Doom fire" style flame: heat starts on the bottom row and drifts upwards, cooling and blowing sideways
    /// at random as it rises. Kept low resolution and drawn with point filtering so it looks like pixel art.
    /// Row 0 is the bottom. Heat runs from 0 (nothing) to 1 (hottest).
    /// </summary>
    public class FireSimulation
    {
        /// <summary>Share of the height even the faintest lit flame reaches.</summary>
        public const double MinReach = 0.12;

        private readonly float[] _heat;
        private readonly Random _random;

        public int Width { get; }

        public int Height { get; }

        /// <summary>Rows an intensity-1 flame reaches. A taller simulation leaves room for flames above 1.</summary>
        public int ReferenceHeight { get; }

        /// <param name="referenceHeight">Rows intensity 1 reaches; defaults to the full height.</param>
        public FireSimulation(int width, int height, int seed, int referenceHeight = 0)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(width <= 0 ? nameof(width) : nameof(height));
            }
            Width = width;
            Height = height;
            ReferenceHeight = referenceHeight > 0 ? referenceHeight : height;
            _heat = new float[width * height];
            _random = new Random(seed);
        }

        public float this[int x, int y] => _heat[y * Width + x];

        /// <summary>
        /// One frame of fire. Intensity 0 lets the flame die out; 1 makes it reach about ReferenceHeight rows; 2
        /// twice that, and so on, until it runs out of rows.
        /// </summary>
        public void Step(double intensity)
        {
            bool lit = intensity > 0;
            for (int x = 0; x < Width; x++)
            {
                // A ragged base looks more like fire than a solid bar.
                _heat[x] = lit ? (float)(0.85 + _random.NextDouble() * 0.15) : 0f;
            }

            // Average cooling per row. A lit flame always reaches at least MinReach of the height, so the first sign
            // of fire is visible; from there it tops out about intensity x ReferenceHeight rows up.
            double reach = ReferenceHeight * (MinReach + (1 - MinReach) * Math.Max(0, intensity));
            double cooling = lit ? 1.0 / reach : 1.0;
            for (int y = Height - 1; y >= 1; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    // Pull heat up from the row below, from straight under or one cell either side, so it flickers.
                    int sourceX = x + _random.Next(-1, 2);
                    if (sourceX < 0 || sourceX >= Width)
                    {
                        _heat[y * Width + x] = 0f;
                        continue;
                    }
                    float below = _heat[(y - 1) * Width + sourceX];
                    float cooled = below - (float)(_random.NextDouble() * 2 * cooling);
                    _heat[y * Width + x] = cooled > 0f ? cooled : 0f;
                }
            }
        }

        /// <summary>Carry the fire over from another simulation of the same width (as many rows as both have).</summary>
        public void CopyFrom(FireSimulation other)
        {
            if (other.Width != Width)
            {
                throw new ArgumentException("Fire can only be copied between simulations of the same width.", nameof(other));
            }
            Array.Copy(other._heat, _heat, Math.Min(other._heat.Length, _heat.Length));
        }

        /// <summary>Average heat of one row, for tests and tuning.</summary>
        public float RowHeat(int y)
        {
            float sum = 0f;
            for (int x = 0; x < Width; x++)
            {
                sum += _heat[y * Width + x];
            }
            return sum / Width;
        }

        public void Clear()
        {
            Array.Clear(_heat, 0, _heat.Length);
        }
    }
}
