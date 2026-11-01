using System.Collections.Generic;
using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    public class BossHealthRulesTests
    {
        [Theory]
        [InlineData(0, 400)]
        [InlineData(9, 400)]
        [InlineData(10, 550)]
        [InlineData(20, 550)]
        public void MaxHealth_UsesImprovedHealthFromCruciballTen(int cruciball, float expected)
        {
            BossFightSpawn boss = new BossFightSpawn(0, isBoss: true, startingHealth: 400, startingHealthFirstCruciball: 550);

            Assert.Equal(expected, BossHealthRules.MaxHealth(boss, cruciball));
        }

        [Fact]
        public void TargetHealth_PicksTheBigBossNotATougherMinion()
        {
            // Given a boss fight with a minion that has more HP than the boss but no boss flag
            List<BossFightSpawn> spawns = new List<BossFightSpawn>
            {
                new BossFightSpawn(0, isBoss: true, 650, 800),
                new BossFightSpawn(0, isBoss: false, 900, 900),
            };

            // Then the target is the boss's HP, using the higher value from Cruciball 10
            Assert.Equal(650, BossHealthRules.TargetHealth(spawns, cruciballLevel: 0));
            Assert.Equal(800, BossHealthRules.TargetHealth(spawns, cruciballLevel: 12));
        }

        [Fact]
        public void TargetHealth_IgnoresBossFlaggedHelpers()
        {
            // Given a boss that summons 20 HP helpers which also carry the boss flag (like Leshy)
            List<BossFightSpawn> spawns = new List<BossFightSpawn>
            {
                new BossFightSpawn(0, isBoss: true, 450, 600),
                new BossFightSpawn(2, isBoss: true, 20, 25),
                new BossFightSpawn(4, isBoss: true, 20, 25),
            };

            // Then the target is the big boss, not a helper that spawns later
            Assert.Equal(450, BossHealthRules.TargetHealth(spawns, 0));
        }

        [Fact]
        public void TargetHealth_SeveralBosses_TakesTheToughest()
        {
            List<BossFightSpawn> spawns = new List<BossFightSpawn>
            {
                new BossFightSpawn(0, isBoss: true, 300, 300),
                new BossFightSpawn(3, isBoss: true, 700, 700),
                new BossFightSpawn(5, isBoss: true, 500, 500),
            };

            Assert.Equal(700, BossHealthRules.TargetHealth(spawns, 0));
        }

        [Fact]
        public void TargetHealth_NoBossFlag_UsesToughestEnemy()
        {
            List<BossFightSpawn> spawns = new List<BossFightSpawn>
            {
                new BossFightSpawn(0, isBoss: false, 120, 150),
                new BossFightSpawn(1, isBoss: false, 80, 90),
            };

            Assert.Equal(120, BossHealthRules.TargetHealth(spawns, 0));
        }

        [Fact]
        public void TargetHealth_RoundsFractionalHealthUp()
        {
            List<BossFightSpawn> spawns = new List<BossFightSpawn> { new BossFightSpawn(0, true, 249.5f, 300) };

            Assert.Equal(250, BossHealthRules.TargetHealth(spawns, 0));
        }

        [Fact]
        public void TargetHealth_Empty_IsZero()
        {
            Assert.Equal(0, BossHealthRules.TargetHealth(new List<BossFightSpawn>(), 0));
        }
    }
}
