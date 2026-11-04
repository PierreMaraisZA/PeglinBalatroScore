using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PeglinBalatroScore
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        // BepInEx names the config file after the GUID, so this gives BepInEx/config/PeglinBalatroScore.cfg.
        public const string Guid = "PeglinBalatroScore";
        public const string Name = "PeglinBalatroScore";
        public const string Version = "1.0.2";

        /// <summary>The Peglin version this mod was built and tested against (Unity's Application.version).</summary>
        public const string SupportedGameVersion = "2.0.12";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> ShowScore;
        internal static ConfigEntry<float> ResultSeconds;
        internal static ConfigEntry<bool> ShowFlames;
        internal static ConfigEntry<bool> ShakeNumbers;
        internal static ConfigEntry<bool> ShowOverkill;
        internal static ConfigEntry<float> Scale;
        internal static ConfigEntry<float> OffsetX;
        internal static ConfigEntry<float> OffsetY;
        internal static ConfigEntry<bool> EnableLogging;
        internal static ConfigEntry<bool> ShowBombChest;
        internal static ConfigEntry<float> ChestScale;

#if DEBUG
        // Dev mode only exists in Debug builds (dotnet build -c Debug). Release builds, the ones handed to players,
        // contain none of this code and no [Dev] config section, so it can't be switched on from the config.
        internal static ConfigEntry<bool> DevMode;
        internal static ConfigEntry<KeyboardShortcut> DevBombRelicsHotkey;

        internal static bool DevModeActive => DevMode != null && DevMode.Value;
#else
        internal static bool DevModeActive => false;
#endif

        private void Awake()
        {
            Log = Logger;

            ShowScore = Config.Bind("General", "ShowScore", true,
                "Show the pegs x damage score above the enemy scroll during battles.");
            ResultSeconds = Config.Bind("General", "ResultSeconds", 3f,
                new ConfigDescription("How long the final total stays on screen after a shot, in real seconds (game speed doesn't shorten it).",
                    new AcceptableValueRange<float>(0.5f, 10f)));
            ShowFlames = Config.Bind("Effects", "ShowFlames", true,
                "Flames grow behind the score as it closes in on the act boss's max HP, and burst when it passes it.");
            ShakeNumbers = Config.Bind("Effects", "ShakeNumbers", true,
                "The numbers shake harder the closer the score gets to the act boss's max HP.");
            ShowOverkill = Config.Bind("Effects", "ShowOverkill", true,
                "Stamp a flaming OVERKILL above the result when a shot's damage beats the act boss's max HP (a green OVERHEAL when healing past your max HP).");
            Scale = Config.Bind("Position", "Scale", 1f,
                new ConfigDescription("Size of the score, relative to the width of the enemy scroll.", new AcceptableValueRange<float>(0.5f, 2f)));
            OffsetX = Config.Bind("Position", "OffsetX", 0f,
                "Move the score left (negative) or right (positive), in pixels at 1920x1080.");
            OffsetY = Config.Bind("Position", "OffsetY", 0f,
                "Move the score down (negative) or up (positive), in pixels at 1920x1080.");
            EnableLogging = Config.Bind("General", "EnableLogging", true,
                "Write informational messages (the boss HP target for each act) to the BepInEx log.");
            ShowBombChest = Config.Bind("Bomb Chest", "ShowBombChest", true,
                "A see-through chest behind Peglin catches a bomb as each one is lit, blows apart with Peglin's last bomb, and shows the bombs' total damage.");
            ChestScale = Config.Bind("Bomb Chest", "ChestScale", 1f,
                new ConfigDescription("Size of the chest, relative to Peglin.", new AcceptableValueRange<float>(0.5f, 2f)));

#if DEBUG
            DevMode = Config.Bind("Dev", "DevMode", false,
                "Turn on developer hotkeys. Only exists in Debug builds.");
            DevBombRelicsHotkey = Config.Bind("Dev", "BombRelicsHotkey", new KeyboardShortcut(KeyCode.F3),
                "Give every bomb relic you don't have; press again to take back the ones it gave. Adding a relic also unlocks it in your save for good (this is a development tool).");
#endif

            CheckGameVersion();
            new Harmony(Guid).PatchAll();
            ScoreHud.Ensure();
#if DEBUG
            Log.LogInfo($"{Name} {Version} loaded (DEBUG build, dev mode {(DevModeActive ? "on" : "off")}).");
#else
            Log.LogInfo($"{Name} {Version} loaded.");
#endif
        }

        /// <summary>
        /// Warns when Peglin isn't the version this mod was built for: it reads the game's private battle fields, so a
        /// game update that renames them can make the score stop updating.
        /// </summary>
        private void CheckGameVersion()
        {
            string gameVersion = Application.version;
            if (gameVersion == SupportedGameVersion)
            {
                return;
            }
            Log.LogWarning($"{Name} {Version} was built for Peglin v{SupportedGameVersion}, but this is Peglin v{gameVersion}. " +
                "The score may not work correctly until the mod is updated for this game version.");
        }
    }
}
