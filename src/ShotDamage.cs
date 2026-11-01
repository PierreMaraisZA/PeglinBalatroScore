using Battle;
using Battle.Attacks;
using HarmonyLib;

namespace PeglinBalatroScore
{
    /// <summary>The numbers Attack.GetDamage works from, read straight off the battle as the shot plays out.</summary>
    internal struct ShotDamage
    {
        public int PegTally;
        public int CritCount;
        public float DamageMultiplier;
        public int DamageBonus;

        /// <summary>Damage per peg: the orb's normal damage, or its crit damage once a crit is active.</summary>
        public long PerPeg;

        /// <summary>The game's own total for the shot so far (heal amount for heal orbs).</summary>
        public long Total;

        private static readonly AccessTools.FieldRef<BattleController, int> PegTallyRef =
            AccessTools.FieldRefAccess<BattleController, int>("_pegMultiplierDamageTally");
        private static readonly AccessTools.FieldRef<BattleController, float> DamageMultiplierRef =
            AccessTools.FieldRefAccess<BattleController, float>("_damageMultiplier");
        private static readonly AccessTools.FieldRef<BattleController, int> DamageBonusRef =
            AccessTools.FieldRefAccess<BattleController, int>("_damageBonus");
        private static readonly AccessTools.FieldRef<AttackManager, Attack> AttackRef =
            AccessTools.FieldRefAccess<AttackManager, Attack>("_attack");

        public bool SameInputs(ShotDamage other)
        {
            return PegTally == other.PegTally && CritCount == other.CritCount
                && DamageMultiplier == other.DamageMultiplier && DamageBonus == other.DamageBonus;
        }

        /// <summary>Reads only the cheap fields; call Calculate when they've changed.</summary>
        public static ShotDamage ReadInputs(BattleController battle)
        {
            return new ShotDamage
            {
                PegTally = PegTallyRef(battle),
                CritCount = BattleController.criticalHitCount,
                DamageMultiplier = DamageMultiplierRef(battle),
                DamageBonus = DamageBonusRef(battle),
            };
        }

        /// <summary>Works out PerPeg and Total with the game's own methods, the same calls it uses for its damage text.</summary>
        public void Calculate(BattleController battle)
        {
            AttackManager attackManager = battle.attackManager;
            Attack attack = attackManager != null ? AttackRef(attackManager) : null;
            if (attack == null)
            {
                PerPeg = 0;
                Total = 0;
                return;
            }
            PerPeg = attack.GetModifiedDamagePerPeg(CritCount, calculateStaticDamageModificersInPlace: false);
            Total = attackManager.GetCurrentDamage(PegTally, DamageMultiplier, DamageBonus, CritCount);
        }
    }
}
