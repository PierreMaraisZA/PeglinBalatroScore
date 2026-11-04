#if DEBUG
using System.Collections.Generic;
using HarmonyLib;
using Map;
using Relics;

namespace PeglinBalatroScore
{
    /// <summary>
    /// Developer shortcuts. Only compiled into Debug builds, and only active when DevMode is on in the config.
    /// </summary>
    internal static class DevTools
    {
        /// <summary>Every relic that changes bombs: how many there are, how they hit, or what they do.</summary>
        private static readonly RelicEffect[] BombRelics =
        {
            RelicEffect.BOMB_SPLASH,
            RelicEffect.PEG_TO_BOMB,
            RelicEffect.BOMBS_RESPAWN,
            RelicEffect.BOMBS_ONE_HIT,
            RelicEffect.BOMB_FORCE_ALWAYS,
            RelicEffect.ADDITIONAL_BOMB_DAMAGE,
            RelicEffect.ADDITIONAL_BOMB_DAMAGE2,
            RelicEffect.ALL_BOMBS_RIGGED,
            RelicEffect.DOUBLE_BOMBS_ON_MAP,
            RelicEffect.ADDITIONAL_STARTING_BOMBS,
            RelicEffect.SPAWN_BOMB_ON_PEG_HITS,
            RelicEffect.BOMBS_APPLY_BLIND,
            RelicEffect.BOMBS_APPLY_POISON,
            RelicEffect.BOMB_NAV_GOLD,
            RelicEffect.CREATE_ADDITIONAL_BOMB,
            RelicEffect.BOMB_TO_DULL,
            RelicEffect.MORE_PIERCE_LESS_BOMB_DAMAGE,
            RelicEffect.STRONG_BOMBS_ON_ONLY_TARGET,
        };

        private static readonly AccessTools.FieldRef<MapController, RelicManager> RelicManagerRef =
            AccessTools.FieldRefAccess<MapController, RelicManager>("_relicManager");

        // RelicEffectActive is false for a relic you own while it's switched off, so ownership is read directly.
        private static readonly AccessTools.FieldRef<RelicManager, Dictionary<RelicEffect, Relic>> OwnedRef =
            AccessTools.FieldRefAccess<RelicManager, Dictionary<RelicEffect, Relic>>("_ownedRelics");

        /// <summary>The bomb relics the hotkey added, so taking them away leaves ones you earned in the run.</summary>
        private static readonly List<Relic> Added = new List<Relic>();

        /// <summary>The run the Added relics came from; a new run starts with a clean list.</summary>
        private static RelicManager _manager;

        public static void Update()
        {
            if (!Plugin.DevModeActive || PauseMenu.Paused)
            {
                return;
            }
            if (Plugin.DevBombRelicsHotkey.Value.IsDown())
            {
                ToggleBombRelics();
            }
        }

        private static void ToggleBombRelics()
        {
            MapController map = MapController.instance;
            RelicManager relics = map != null ? RelicManagerRef(map) : null;
            if (relics == null || relics.globalRelics == null)
            {
                Plugin.Log.LogWarning("Dev bomb relics: no run in progress.");
                return;
            }

            Added.RemoveAll(relic => relic == null);
            if (_manager != relics)
            {
                // A new run: the last run's relics aren't ours to take away any more (they may be owned for real).
                Added.Clear();
                _manager = relics;
            }
            if (Added.Count > 0)
            {
                int removed = 0;
                foreach (Relic relic in Added)
                {
                    if (Owns(relics, relic.effect))
                    {
                        relics.RemoveRelic(relic);
                        removed++;
                    }
                }
                Added.Clear();
                if (removed > 0)
                {
                    ModLog.Info($"Dev bomb relics: took away the {removed} the hotkey gave.");
                    return;
                }
            }

            HashSet<RelicEffect> wanted = new HashSet<RelicEffect>(BombRelics);
            foreach (Relic relic in relics.globalRelics.relics)
            {
                if (relic != null && wanted.Remove(relic.effect) && !Owns(relics, relic.effect))
                {
                    relics.AddRelic(relic);
                    Added.Add(relic);
                }
            }
            string missing = wanted.Count > 0 ? $" Not in the game's relic list: {string.Join(", ", wanted)}." : string.Empty;
            ModLog.Info($"Dev bomb relics: gave {Added.Count} bomb relics (press again to take them away).{missing}");
        }

        private static bool Owns(RelicManager relics, RelicEffect effect)
        {
            Dictionary<RelicEffect, Relic> owned = OwnedRef(relics);
            return owned != null && owned.ContainsKey(effect);
        }
    }
}
#endif
