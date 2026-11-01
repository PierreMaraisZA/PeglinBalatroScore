using System;

namespace PeglinBalatroScore.Core
{
    public enum ScorePhase
    {
        /// <summary>Waiting for a shot: the widget shows 0 x 0.</summary>
        Ready,

        /// <summary>A shot is in play and the pegs x damage values are updating.</summary>
        Counting,

        /// <summary>The shot is over and the final total is on show for a few seconds.</summary>
        Result
    }

    /// <summary>
    /// What the score widget should show for the current shot. Pure C# (no Unity/BepInEx) so it can be unit tested.
    /// The plugin feeds it the game's own numbers; this only decides when to reset, count and hold the result.
    /// </summary>
    public class ScoreTracker
    {
        public ScorePhase Phase { get; private set; } = ScorePhase.Ready;

        /// <summary>The game's peg tally (pegsMultiplierTally in Attack.GetDamage), so relics that change it count.</summary>
        public long Pegs { get; private set; }

        /// <summary>Damage per peg: the orb's normal damage, or its crit damage once a crit peg has been hit.</summary>
        public long PerPeg { get; private set; }

        /// <summary>The game's real damage (or heal) for the shot, including bonuses and multipliers.</summary>
        public long Total { get; private set; }

        /// <summary>True when the orb in play heals instead of attacking.</summary>
        public bool IsHeal { get; private set; }

        /// <summary>Real-time seconds left before the result goes back to 0 x 0.</summary>
        public float ResultSecondsLeft { get; private set; }

        /// <summary>Total of the most recent finished shot, kept after the result is gone (for the tooltip). Null before any.</summary>
        public long? LastShotTotal { get; private set; }

        /// <summary>Whether the most recent finished shot healed rather than dealt damage.</summary>
        public bool LastShotWasHeal { get; private set; }

        /// <summary>How long the result stays on screen, in real seconds (game speed doesn't shorten it).</summary>
        public float ResultHoldSeconds { get; set; } = 3f;

        /// <summary>A shot was fired. Starts again from 0 x 0, even if the last result is still showing.</summary>
        public void BeginShot(bool isHeal)
        {
            Zero();
            IsHeal = isHeal;
            Phase = ScorePhase.Counting;
        }

        /// <summary>
        /// Latest values from the game while the shot is in play. Stays at 0 x 0 until the first peg is hit, so the
        /// numbers pop in with the hit rather than showing the orb's damage on launch.
        /// </summary>
        public void Update(long pegs, long perPeg, long total)
        {
            if (Phase != ScorePhase.Counting)
            {
                return;
            }
            if (pegs <= 0)
            {
                Pegs = 0;
                PerPeg = 0;
            }
            else
            {
                Pegs = pegs;
                PerPeg = Math.Max(0, perPeg);
            }
            Total = Math.Max(0, total);
        }

        /// <summary>Every ball from the shot has finished: show the final total.</summary>
        public void CompleteShot(long total)
        {
            if (Phase != ScorePhase.Counting)
            {
                return;
            }
            Total = Math.Max(0, total);
            Phase = ScorePhase.Result;
            ResultSecondsLeft = ResultHoldSeconds;
            LastShotTotal = Total;
            LastShotWasHeal = IsHeal;
        }

        /// <summary>
        /// The attack went out with a slightly different total (a few relics apply their multiplier only as the attack
        /// starts). Corrects the result on show without restarting its timer.
        /// </summary>
        public void RefineResult(long total)
        {
            if (Phase == ScorePhase.Result)
            {
                Total = Math.Max(0, total);
                LastShotTotal = Total;
            }
        }

        /// <summary>Advance by real (unscaled) time.</summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (Phase != ScorePhase.Result)
            {
                return;
            }
            ResultSecondsLeft -= unscaledDeltaTime;
            if (ResultSecondsLeft <= 0f)
            {
                Clear();
            }
        }

        /// <summary>Back to 0 x 0 (battle over, or the result timed out).</summary>
        public void Clear()
        {
            Zero();
            IsHeal = false;
            Phase = ScorePhase.Ready;
        }

        private void Zero()
        {
            Pegs = 0;
            PerPeg = 0;
            Total = 0;
            ResultSecondsLeft = 0f;
        }
    }
}
