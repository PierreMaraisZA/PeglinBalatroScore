using System;
using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    public class FireSimulationTests
    {
        private static FireSimulation Run(double intensity, int steps, int seed = 7)
        {
            FireSimulation fire = new FireSimulation(24, 32, seed);
            for (int i = 0; i < steps; i++)
            {
                fire.Step(intensity);
            }
            return fire;
        }

        [Fact]
        public void Constructor_RejectsEmptySize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireSimulation(0, 10, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FireSimulation(10, 0, 1));
        }

        [Fact]
        public void Step_ZeroIntensity_LeavesNoHeat()
        {
            FireSimulation fire = Run(intensity: 0, steps: 40);

            for (int y = 0; y < fire.Height; y++)
            {
                Assert.Equal(0f, fire.RowHeat(y));
            }
        }

        [Fact]
        public void Step_FlameDiesOutWhenIntensityDrops()
        {
            // Given a roaring fire
            FireSimulation fire = Run(intensity: 1, steps: 60);

            // When the intensity drops to zero for as many frames as the fire is tall
            for (int i = 0; i < fire.Height; i++)
            {
                fire.Step(0);
            }

            // Then every row is cold again
            for (int y = 0; y < fire.Height; y++)
            {
                Assert.Equal(0f, fire.RowHeat(y));
            }
        }

        [Fact]
        public void Step_HigherIntensity_ReachesHigher()
        {
            FireSimulation low = Run(intensity: 0.3, steps: 60);
            FireSimulation high = Run(intensity: 1.0, steps: 60);

            int row = low.Height * 2 / 3;
            Assert.True(high.RowHeat(row) > low.RowHeat(row));
        }

        [Fact]
        public void Step_FaintestFlame_StillRisesClearOfTheBase()
        {
            // Given a flame that has only just been lit
            FireSimulation fire = Run(intensity: 0.001, steps: 60);

            // Then there's still heat a good few rows up, not just a glow on the bottom row
            Assert.True(fire.RowHeat(3) > 0f, "row 3 should have some heat");
        }

        [Fact]
        public void Step_TallerArea_IntensityTwoReachesTwiceAsHigh()
        {
            // Given a fire area three times the reference height
            FireSimulation one = new FireSimulation(24, 96, 7, referenceHeight: 32);
            FireSimulation two = new FireSimulation(24, 96, 7, referenceHeight: 32);
            for (int i = 0; i < 120; i++)
            {
                one.Step(1);
                two.Step(2);
            }

            // Then intensity 1 stays roughly within the reference height while 2 climbs well past it
            Assert.True(two.RowHeat(48) > one.RowHeat(48));
            Assert.Equal(0f, one.RowHeat(80));
        }

        [Fact]
        public void CopyFrom_CarriesTheFireIntoATallerArea()
        {
            FireSimulation small = Run(intensity: 1, steps: 60);
            FireSimulation tall = new FireSimulation(small.Width, small.Height * 2, 9, referenceHeight: small.Height);

            tall.CopyFrom(small);

            for (int y = 0; y < small.Height; y++)
            {
                Assert.Equal(small.RowHeat(y), tall.RowHeat(y));
            }
            Assert.Equal(0f, tall.RowHeat(small.Height));
        }

        [Fact]
        public void CopyFrom_DifferentWidth_Throws()
        {
            Assert.Throws<ArgumentException>(() => new FireSimulation(10, 10, 1).CopyFrom(new FireSimulation(12, 10, 1)));
        }

        [Fact]
        public void Step_HeatStaysInRange()
        {
            FireSimulation fire = Run(intensity: 1.5, steps: 60);

            for (int y = 0; y < fire.Height; y++)
            {
                for (int x = 0; x < fire.Width; x++)
                {
                    Assert.InRange(fire[x, y], 0f, 1f);
                }
            }
        }

        [Fact]
        public void Step_SameSeed_IsRepeatable()
        {
            FireSimulation a = Run(intensity: 0.8, steps: 30, seed: 3);
            FireSimulation b = Run(intensity: 0.8, steps: 30, seed: 3);

            for (int y = 0; y < a.Height; y++)
            {
                Assert.Equal(a.RowHeat(y), b.RowHeat(y));
            }
        }
    }
}
