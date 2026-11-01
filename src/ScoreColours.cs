using UnityEngine;

namespace PeglinBalatroScore
{
    /// <summary>Peglin-flavoured colours: Peglin's olive green for pegs, the crit peg's yellow for damage.</summary>
    internal static class ScoreColours
    {
        public static readonly Color32 PegsFace = new Color32(112, 142, 46, 255);
        public static readonly Color32 PegsShade = new Color32(70, 92, 26, 255);
        public static readonly FlamePalette PegsFlame = new FlamePalette(
            new Color32(66, 92, 24, 255), new Color32(124, 168, 44, 255), new Color32(190, 226, 84, 255), new Color32(246, 252, 190, 255));

        // Always the crit yellow, even when a crit has been downgraded (the game turns that text red).
        public static readonly Color32 DamageFace = new Color32(222, 170, 30, 255);
        public static readonly Color32 DamageShade = new Color32(150, 104, 14, 255);
        public static readonly FlamePalette DamageFlame = new FlamePalette(
            new Color32(150, 54, 16, 255), new Color32(226, 120, 24, 255), new Color32(250, 196, 50, 255), new Color32(255, 246, 180, 255));

        public static readonly Color32 HealFace = new Color32(70, 186, 120, 255);
        public static readonly Color32 HealShade = new Color32(36, 120, 76, 255);
        public static readonly FlamePalette HealFlame = new FlamePalette(
            new Color32(24, 104, 84, 255), new Color32(56, 178, 120, 255), new Color32(130, 232, 160, 255), new Color32(226, 255, 226, 255));

        public static readonly Color32 ResultFace = new Color32(44, 36, 32, 255);
        public static readonly Color32 ResultShade = new Color32(24, 18, 16, 255);
        public static readonly Color32 ResultBorder = new Color32(222, 170, 30, 255);

        public static readonly Color32 Text = new Color32(255, 255, 255, 255);
        public static readonly Color32 TextShadow = new Color32(28, 22, 20, 200);
        public static readonly Color32 Outline = new Color32(24, 18, 16, 255);
        public static readonly Color32 Times = new Color32(222, 170, 30, 255);
    }
}
