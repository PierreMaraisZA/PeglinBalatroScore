using System.Text;

namespace PeglinBalatroScore.Core
{
    /// <summary>The hover tooltip's text: how a shot's damage adds up, in plain words, plus the last shot's result.</summary>
    public static class ScoreExplanation
    {
        /// <param name="lastShotTotal">The last finished shot's total, or null if there hasn't been one yet.</param>
        /// <param name="lastShotWasHeal">Whether that shot healed instead of dealing damage.</param>
        public static string Build(long? lastShotTotal, bool lastShotWasHeal)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("<b>How your shot adds up</b>");
            text.AppendLine();
            text.AppendLine("<b>Left:</b> what the pegs your orb has hit are worth. Most pegs add 1, but some pegs and relics make a hit worth more.");
            text.AppendLine();
            text.AppendLine("<b>Right:</b> your orb's damage per peg, or its crit damage once a crit has been triggered.");
            text.AppendLine();
            text.Append("The final total is always what the game itself works out, so it can be a little off from left x right because of relics, bonuses and other effects.");
            if (lastShotTotal.HasValue)
            {
                text.AppendLine();
                text.AppendLine();
                text.Append(LastShot(lastShotTotal.Value, lastShotWasHeal));
            }
            return text.ToString();
        }

        public static string LastShot(long total, bool wasHeal)
        {
            return $"<b>Last shot:</b> {ScoreFormat.Format(total)} {(wasHeal ? "healing" : "damage")}";
        }
    }
}
