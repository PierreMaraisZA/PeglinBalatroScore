using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    public class FlameCurveTests
    {
        [Theory]
        [InlineData(0, 100)]
        [InlineData(9, 100)]
        [InlineData(50, 0)]
        [InlineData(-5, 100)]
        public void Intensity_BelowTenPercentOrNoTarget_IsZero(long score, long target)
        {
            Assert.Equal(0, FlameCurve.Intensity(score, target));
        }

        [Fact]
        public void Intensity_AtTenPercent_StartsAtZero()
        {
            Assert.Equal(0, FlameCurve.Intensity(10, 100), 6);
        }

        [Fact]
        public void Intensity_JustPastTenPercent_IsLit()
        {
            Assert.True(FlameCurve.Intensity(11, 100) > 0);
        }

        [Theory]
        [InlineData(0, 0.05)]
        [InlineData(1, 0.05)]
        [InlineData(2, 0.10)]
        [InlineData(3, 0.10)]
        [InlineData(12, 0.10)]
        public void StartRatioForFloor_FivePercentOnFirstNodeThenTen(int floor, double expected)
        {
            Assert.Equal(expected, FlameCurve.StartRatioForFloor(floor), 6);
        }

        [Fact]
        public void Intensity_EarlyFloorStart_LightsFlamesSooner()
        {
            // Given a score at 8% of the boss's HP
            // Then there are flames on node 1 (start 5%) but none from node 2 on (start 10%)
            Assert.True(FlameCurve.Intensity(8, 100, FlameCurve.StartRatioForFloor(1)) > 0);
            Assert.Equal(0, FlameCurve.Intensity(8, 100, FlameCurve.StartRatioForFloor(2)));
        }

        [Fact]
        public void Intensity_EasesInFromEmbersToColumns()
        {
            // Given act 2's 3000 HP boss and a first-node start of 5% (150 damage)
            double start = FlameCurve.StartRatioForFloor(1);

            double justLit = FlameCurve.Intensity(300, 3000, start);
            double halfway = FlameCurve.Intensity(1500, 3000, start);
            double nearlyThere = FlameCurve.Intensity(2850, 3000, start);

            // Then a shot just past the start is lit but small, and the growth speeds up towards the target
            Assert.InRange(justLit, 0.001, 0.05);
            Assert.True(nearlyThere - halfway > halfway - justLit);
        }

        [Fact]
        public void Intensity_AtTarget_IsOne()
        {
            Assert.Equal(1, FlameCurve.Intensity(100, 100), 6);
        }

        [Fact]
        public void Intensity_GrowsAsScoreApproachesTarget()
        {
            double previous = -1;
            for (long score = 25; score <= 100; score += 5)
            {
                double intensity = FlameCurve.Intensity(score, 100);
                Assert.True(intensity > previous, $"intensity at {score} should be above {previous}");
                previous = intensity;
            }
        }

        [Fact]
        public void Intensity_PastTarget_KeepsGrowingWithNoLimit()
        {
            double atDouble = FlameCurve.Intensity(200, 100);
            double atTenTimes = FlameCurve.Intensity(1000, 100);
            double atMillion = FlameCurve.Intensity(100_000_000, 100);

            Assert.True(atDouble > 1);
            Assert.Equal(1 + FlameCurve.OverflowPerTenfold, atTenTimes, 6);
            Assert.Equal(1 + 6 * FlameCurve.OverflowPerTenfold, atMillion, 6);
        }

        [Fact]
        public void Intensity_InfiniteDamage_IsHugeButFinite()
        {
            double intensity = FlameCurve.Intensity(long.MaxValue, 500);

            Assert.True(intensity > 30);
            Assert.False(double.IsInfinity(intensity) || double.IsNaN(intensity));
        }

        [Theory]
        [InlineData(90, 100, 100, true)]
        [InlineData(90, 150, 100, true)]
        [InlineData(100, 120, 100, false)]
        [InlineData(50, 90, 100, false)]
        [InlineData(0, 10, 0, false)]
        public void CrossedTarget_OnlyOnTheUpdateThatReachesIt(long previous, long score, long target, bool expected)
        {
            Assert.Equal(expected, FlameCurve.CrossedTarget(previous, score, target));
        }
    }
}
