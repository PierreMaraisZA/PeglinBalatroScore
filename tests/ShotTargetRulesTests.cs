using System;
using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    public class ShotTargetRulesTests
    {
        private static long NotAsked() => throw new InvalidOperationException("this source should not have been read");

        /// <summary>The target logic exactly as 1.0.2 wrote it in ScoreHud.OnShotFired, kept here to compare against.</summary>
        private static long Version102(bool isHeal, long playerMax, long mapTarget, bool isBossBattle, long liveBoss, long liveAny)
        {
            long target = isHeal ? playerMax : mapTarget;
            if (!isHeal && isBossBattle)
            {
                target = Math.Max(target, liveBoss);
            }
            if (target <= 0)
            {
                target = liveAny;
            }
            return target;
        }

        [Fact]
        public void NothingRegistered_BehavesLike102()
        {
            long[] values = { 0, 1, 450, 600000 };
            foreach (bool heal in new[] { false, true })
            foreach (bool boss in new[] { false, true })
            foreach (long playerMax in values)
            foreach (long map in values)
            foreach (long liveBoss in values)
            foreach (long liveAny in values)
            {
                long expected = Version102(heal, playerMax, map, boss, liveBoss, liveAny);
                long actual = ShotTargetRules.Choose(heal, playerMax, 0, map, boss, () => liveBoss, () => liveAny);
                Assert.True(expected == actual,
                    $"heal {heal} boss {boss} playerMax {playerMax} map {map} liveBoss {liveBoss} liveAny {liveAny}: expected {expected}, got {actual}");
            }
        }

        [Fact]
        public void ProviderValue_IsUsedExactly_EvenBelowTheBossAndTheLiveBoss()
        {
            // Boss Rush: the boss's real HP is 100,000 but the map still holds the 600,000 prefab boss
            long target = ShotTargetRules.Choose(false, 80, providerTarget: 100000, mapTarget: 600000,
                isBossBattle: true, liveBoss: () => 600000, liveAny: () => 600000);

            Assert.Equal(100000, target);
        }

        [Fact]
        public void ProviderValue_DoesNotReadTheOtherSources()
        {
            long target = ShotTargetRules.Choose(false, 80, 123, 600000, true, NotAsked, NotAsked);

            Assert.Equal(123, target);
        }

        [Fact]
        public void ProviderValue_VeryLargeIsUsedAsIs()
        {
            long target = ShotTargetRules.Choose(false, 80, long.MaxValue, 600, true, NotAsked, NotAsked);

            Assert.Equal(long.MaxValue, target);
        }

        [Fact]
        public void ProviderValue_IsIgnoredForHealingShots()
        {
            long target = ShotTargetRules.Choose(true, 80, providerTarget: 123, mapTarget: 600000, isBossBattle: false, NotAsked, NotAsked);

            Assert.Equal(80, target);
        }

        [Fact]
        public void Heal_WithNoMaxHealth_FallsBackToTheToughestOnScreen()
        {
            long target = ShotTargetRules.Choose(true, 0, 0, 500, false, NotAsked, () => 40);

            Assert.Equal(40, target);
        }

        [Fact]
        public void BossFight_LiveBossOnlyRaisesTheTarget()
        {
            Assert.Equal(900, ShotTargetRules.Choose(false, 80, 0, 900, true, () => 700, NotAsked));
            Assert.Equal(1200, ShotTargetRules.Choose(false, 80, 0, 900, true, () => 1200, NotAsked));
        }

        [Fact]
        public void NormalFight_UsesTheMapBossWithoutLookingAtTheScreen()
        {
            Assert.Equal(1500, ShotTargetRules.Choose(false, 80, 0, 1500, false, NotAsked, NotAsked));
        }

        [Fact]
        public void NoMapTarget_UsesTheToughestEnemyOnScreen()
        {
            Assert.Equal(75, ShotTargetRules.Choose(false, 80, 0, 0, false, NotAsked, () => 75));
        }

        [Fact]
        public void NoMapTarget_BossFight_UsesTheLiveBoss()
        {
            Assert.Equal(640, ShotTargetRules.Choose(false, 80, 0, 0, true, () => 640, NotAsked));
        }
    }
}
