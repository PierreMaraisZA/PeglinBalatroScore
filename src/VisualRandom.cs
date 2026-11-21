using UnityEngine;

namespace PeglinBalatroScore
{
    /// <summary>
    /// Random numbers for purely visual effects (shake, tilt, wobble). Uses its own generator so the mod never takes
    /// numbers from UnityEngine.Random, which the game uses for its own rolls: the same seed plays out the same with
    /// or without the mod. Main thread only.
    /// </summary>
    internal static class VisualRandom
    {
        private static readonly System.Random Rng = new System.Random();

        /// <summary>A number from 0 to 1.</summary>
        public static float Value => (float)Rng.NextDouble();

        /// <summary>A number between min and max.</summary>
        public static float Range(float min, float max) => min + (max - min) * Value;

        /// <summary>A point inside a circle of radius 1, spread evenly.</summary>
        public static Vector2 InsideUnitCircle
        {
            get
            {
                float angle = Range(0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(Value);
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }
    }
}
