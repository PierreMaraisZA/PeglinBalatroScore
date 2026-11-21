using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    public class BombTallyTests
    {
        [Fact]
        public void Empty_HasNothingToTake()
        {
            var tally = new BombTally();

            Assert.False(tally.TakeTotal(out long total));
            Assert.Equal(0, total);
        }

        [Fact]
        public void SumsEachBombOnce()
        {
            var tally = new BombTally();
            tally.Add(1, 30);
            tally.Add(2, 45);

            Assert.True(tally.TakeTotal(out long total));
            Assert.Equal(75, total);
        }

        [Fact]
        public void SameBombHittingSeveralEnemies_CountsOnce()
        {
            var tally = new BombTally();
            tally.Add(7, 30);
            tally.Add(7, 30);
            tally.Add(7, 30);

            Assert.True(tally.TakeTotal(out long total));
            Assert.Equal(30, total);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void ZeroOrNegativeDamage_IsIgnored(long amount)
        {
            var tally = new BombTally();
            tally.Add(1, amount);

            Assert.False(tally.TakeTotal(out _));
        }

        [Fact]
        public void ZeroDamageHit_DoesNotStopTheBombsRealHitCounting()
        {
            var tally = new BombTally();
            tally.Add(1, 0);
            tally.Add(1, 20);

            Assert.True(tally.TakeTotal(out long total));
            Assert.Equal(20, total);
        }

        [Fact]
        public void Take_StartsAgainFromZero_AndForgetsCountedBombs()
        {
            var tally = new BombTally();
            tally.Add(1, 10);
            tally.TakeTotal(out _);

            Assert.False(tally.TakeTotal(out _));
            tally.Add(1, 12);
            Assert.True(tally.TakeTotal(out long total));
            Assert.Equal(12, total);
        }

        [Fact]
        public void Clear_DropsTheTotal()
        {
            var tally = new BombTally();
            tally.Add(1, 10);
            tally.Clear();

            Assert.False(tally.TakeTotal(out _));
        }

        [Fact]
        public void HugeDamage_StopsAtTheLargestValue()
        {
            var tally = new BombTally();
            tally.Add(1, long.MaxValue - 1);
            tally.Add(2, 10);

            Assert.True(tally.TakeTotal(out long total));
            Assert.Equal(long.MaxValue, total);
        }
    }
}
