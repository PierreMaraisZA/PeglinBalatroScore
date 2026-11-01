using System;

namespace PeglinBalatroScore.Core
{
    /// <summary>
    /// How big the flames (and how hard the number shake) should be for a score, measured against the act boss's
    /// max HP. Below StartRatio there are no flames; they grow until the score reaches the target, then burst.
    /// </summary>
    public static class FlameCurve
    {
        /// <summary>From the second node of an act on, embers start at 10% of the boss's max HP.</summary>
        public const double StartRatio = 0.10;

        /// <summary>Early in an act the start drops by this much per node: 5% on node 1, 10% from node 2.</summary>
        public const double StartRatioPerFloor = 0.05;

        /// <summary>
        /// Where flames start for a node of the act (1 = first node). The first fight hits for far less than the boss's
        /// HP, so the bar starts lower there and reaches StartRatio from the second node.
        /// </summary>
        public static double StartRatioForFloor(int floor)
        {
            return Math.Min(StartRatio, Math.Max(1, floor) * StartRatioPerFloor);
        }

        /// <summary>
        /// Extra intensity for every tenfold past the target, with no upper limit: twice the boss's HP gives about 1.6,
        /// ten times gives 3 (well off the top of the screen), a hundred times gives 5.
        /// </summary>
        public const double OverflowPerTenfold = 2.0;

        public static double Ratio(long score, long target)
        {
            if (target <= 0 || score <= 0)
            {
                return 0;
            }
            return (double)score / target;
        }

        /// <summary>
        /// 0 below the start ratio, easing up to 1 at the target. Past the target it keeps climbing without limit
        /// (OverflowPerTenfold for each tenfold) so bigger overkills send the flames higher, off the screen if need be.
        /// </summary>
        public static double Intensity(long score, long target, double startRatio = StartRatio)
        {
            double ratio = Ratio(score, target);
            if (ratio < startRatio)
            {
                return 0;
            }
            if (ratio < 1)
            {
                double t = (ratio - startRatio) / (1 - startRatio);
                // Ease in: embers for a while, then a quick climb into tall columns as the score nears the boss's HP.
                // The fire itself keeps even the smallest lit flame visible (FireSimulation.MinReach).
                return t * t * (3 - 2 * t);
            }
            return 1 + Math.Log10(ratio) * OverflowPerTenfold;
        }

        /// <summary>True on the update where the score first reaches the target (the flames burst).</summary>
        public static bool CrossedTarget(long previousScore, long score, long target)
        {
            return target > 0 && previousScore < target && score >= target;
        }
    }
}
