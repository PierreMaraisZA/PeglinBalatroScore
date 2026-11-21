using System;

namespace PeglinBalatroScore.Core
{
    /// <summary>Which target the HUD aims at for a shot, given what each source says. Pure, so it can be tested.</summary>
    public static class ShotTargetRules
    {
        /// <summary>
        /// The target for a shot.
        /// A healing shot aims at the player's max HP. Otherwise a mod's provider value wins and is used exactly as
        /// given. Failing that, the act boss's HP from the map; in the boss fight itself the live boss can only raise
        /// it. With nothing else to go on, the toughest enemy on screen.
        /// </summary>
        /// <param name="providerTarget">The first positive value from the registered providers, or 0.</param>
        /// <param name="mapTarget">The act boss's HP read from the map, or 0 if unknown.</param>
        /// <param name="isBossBattle">Whether this fight is the act's boss fight.</param>
        /// <param name="liveBoss">Asked only in a boss battle: the toughest boss enemy now on screen, 0 if none.</param>
        /// <param name="liveAny">Asked only if nothing else gave a target: the toughest enemy on screen.</param>
        public static long Choose(bool isHeal, long playerMaxHealth, long providerTarget, long mapTarget,
            bool isBossBattle, Func<long> liveBoss, Func<long> liveAny)
        {
            if (!isHeal && providerTarget > 0)
            {
                return providerTarget;
            }

            long target = isHeal ? playerMaxHealth : mapTarget;
            if (!isHeal && isBossBattle)
            {
                // In the boss fight itself the live boss is the final word (e.g. relics that change its HP).
                target = Math.Max(target, liveBoss());
            }
            return target > 0 ? target : liveAny();
        }
    }
}
