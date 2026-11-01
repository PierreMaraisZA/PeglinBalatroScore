using System.Collections.Generic;

namespace PeglinBalatroScore.Core
{
    /// <summary>One enemy the boss fight will spawn, in the order the fight lists it.</summary>
    public struct BossFightSpawn
    {
        /// <summary>Turn it spawns on: 0 for enemies on the board at the start, otherwise the wave's turn.</summary>
        public int Turn;

        public bool IsBoss;

        public float StartingHealth;

        public float StartingHealthFirstCruciball;

        public BossFightSpawn(int turn, bool isBoss, float startingHealth, float startingHealthFirstCruciball)
        {
            Turn = turn;
            IsBoss = isBoss;
            StartingHealth = startingHealth;
            StartingHealthFirstCruciball = startingHealthFirstCruciball;
        }
    }

    /// <summary>The score target: the max HP of the act's big boss, as Enemy sets it up.</summary>
    public static class BossHealthRules
    {
        /// <summary>CruciballManager.EnemyShouldUseImprovedHealth gives bosses their higher HP from this level.</summary>
        public const int ImprovedBossHealthCruciball = 10;

        public static float MaxHealth(BossFightSpawn spawn, int cruciballLevel)
        {
            return cruciballLevel >= ImprovedBossHealthCruciball ? spawn.StartingHealthFirstCruciball : spawn.StartingHealth;
        }

        /// <summary>
        /// The HP to beat: the big boss, meaning the boss-flagged enemy with the most HP. Minions are ignored, even
        /// the ones that also carry the boss flag (Leshy's 20 HP helpers). If the fight has no boss-flagged enemy,
        /// the toughest enemy in it. 0 when there's nothing to go on.
        /// </summary>
        public static long TargetHealth(IList<BossFightSpawn> spawns, int cruciballLevel)
        {
            float toughestBoss = 0f;
            float toughest = 0f;
            foreach (BossFightSpawn spawn in spawns)
            {
                float health = MaxHealth(spawn, cruciballLevel);
                toughest = System.Math.Max(toughest, health);
                if (spawn.IsBoss)
                {
                    toughestBoss = System.Math.Max(toughestBoss, health);
                }
            }
            float target = toughestBoss > 0f ? toughestBoss : toughest;
            return target > 0f ? (long)System.Math.Ceiling(target) : 0;
        }
    }
}
