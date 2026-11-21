using System;
using PeglinBalatroScore.Core;

namespace PeglinBalatroScore
{
    /// <summary>
    /// For other mods (found by this simple type name in the plugin's assembly, GUID "PeglinBalatroScore").
    /// Members are only ever added; nothing here changes meaning.
    /// </summary>
    public static class BalatroScoreApi
    {
        internal static readonly TargetProviderRegistry Providers =
            new TargetProviderRegistry(message => Plugin.Log?.LogWarning(message));

        private static readonly GuidRegistry<Func<string, bool>> Suppressors =
            new GuidRegistry<Func<string, bool>>("score HUD suppressor", message => Plugin.Log?.LogWarning(message));

        private static readonly GuidRegistry<Action<string, long, long>> Listeners =
            new GuidRegistry<Action<string, long, long>>("score listener", message => Plugin.Log?.LogWarning(message));

        /// <summary>Version of this API. Currently 1.</summary>
        public static int ApiVersion => 1;

        /// <summary>
        /// The target the HUD is using: what it picked for the latest shot (0 before the first one). It is kept
        /// until the next shot, so the last winning shot's OVERKILL still compares against it; it is the player's
        /// max HP after a healing shot.
        /// </summary>
        public static long CurrentTarget => ScoreHud.Instance != null ? ScoreHud.Instance.CurrentTarget : 0;

        /// <summary>
        /// Sets the target HP for the flames and OVERKILL, asked at the start of each damaging shot. A value
        /// 0 or less means no opinion; a positive value is used exactly as given, even if it is lower than the boss's HP.
        /// Registering again with the same GUID replaces the old provider; a null provider removes it, and a null or
        /// empty GUID is ignored. Healing shots ignore it.
        /// </summary>
        public static void SetTargetProvider(string guid, Func<long> provider)
        {
            Providers.Set(guid, provider);
        }

        /// <summary>
        /// Hides parts of the HUD. Asked often (up to every frame; don't rely on when) for each part by name:
        /// <c>"score"</c> (the score, and the OVERKILL stamp with it), <c>"overkill"</c> (only the OVERKILL/OVERHEAL
        /// stamp), <c>"bomb_chest"</c> (the bomb chest; one in progress is dropped without its burst, and stays gone until
        /// that turn's bombs are all thrown). New parts may be added, so return false for names you don't know unless you
        /// mean to hide everything. True from any suppressor hides that part. Scoring and listeners carry on while
        /// hidden. Keep it cheap. Same GUID rules as <see cref="SetTargetProvider"/>.
        /// </summary>
        public static void SetHudSuppressor(string guid, Func<string, bool> suppress)
        {
            Suppressors.Set(guid, suppress);
        }

        /// <summary>
        /// Told once for each scored result, after its final total is known: the kind, the total, and the target it was
        /// measured against. Kinds: <c>"shot"</c> (a damaging shot; the total is the damage the attack goes out with, told
        /// when the attack goes out, or when the next shot is fired or the battle ends if there is no attack),
        /// <c>"heal"</c> (a healing shot; the target is the player's max HP) and <c>"bombs"</c> (Peglin's thrown bombs
        /// for a turn: each bomb's damage after relics, counted once however many enemies it hits, told when the next
        /// shot is fired or the battle ends; the target is the latest damaging shot's). New kinds may be added, so ignore
        /// ones you don't know. Told whether or not the HUD is showing. A shot is not told if the battle disappears while
        /// it is in play (quitting to the menu). Every listener is told; one that throws is logged once and skipped.
        /// Same GUID rules as <see cref="SetTargetProvider"/>.
        /// </summary>
        public static void SetScoreListener(string guid, Action<string, long, long> listener)
        {
            Listeners.Set(guid, listener);
        }

        /// <summary>Removes everything the GUID registered.</summary>
        public static void RemoveAll(string guid)
        {
            Providers.RemoveAll(guid);
            Suppressors.RemoveAll(guid);
            Listeners.RemoveAll(guid);
        }

        internal const string PartScore = "score";
        internal const string PartOverkill = "overkill";
        internal const string PartBombChest = "bomb_chest";

        /// <summary>True while any suppressor asks for this part to be hidden.</summary>
        internal static bool IsSuppressed(string part)
        {
            return Suppressors.Any(suppress => suppress(part));
        }

        internal static void ReportShot(bool heal, long total, long target)
        {
            string kind = heal ? "heal" : "shot";
            Report(kind, total, target);
        }

        internal static void ReportBombs(long total, long target)
        {
            Report("bombs", total, target);
        }

        private static void Report(string kind, long total, long target)
        {
            Listeners.Each(listener => listener(kind, total, target));
        }
    }
}
