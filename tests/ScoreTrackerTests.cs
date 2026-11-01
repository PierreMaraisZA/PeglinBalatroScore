using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    public class ScoreTrackerTests
    {
        [Fact]
        public void NewTracker_IsReadyAtZero()
        {
            ScoreTracker tracker = new ScoreTracker();

            Assert.Equal(ScorePhase.Ready, tracker.Phase);
            Assert.Equal(0, tracker.Pegs);
            Assert.Equal(0, tracker.PerPeg);
            Assert.Equal(0, tracker.Total);
        }

        [Fact]
        public void Update_BeforeFirstPegHit_StaysZeroByZero()
        {
            // Given a shot that has just been fired
            ScoreTracker tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);

            // When the game reports the orb's damage but no pegs yet
            tracker.Update(pegs: 0, perPeg: 3, total: 2);

            // Then the widget still reads 0 x 0
            Assert.Equal(0, tracker.Pegs);
            Assert.Equal(0, tracker.PerPeg);
        }

        [Fact]
        public void Update_WhileCounting_TakesGameValues()
        {
            ScoreTracker tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);

            tracker.Update(pegs: 30, perPeg: 3, total: 95);

            Assert.Equal(30, tracker.Pegs);
            Assert.Equal(3, tracker.PerPeg);
            Assert.Equal(95, tracker.Total);
        }

        [Fact]
        public void Update_NegativeValues_ClampToZero()
        {
            ScoreTracker tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);

            tracker.Update(pegs: 4, perPeg: -2, total: -8);

            Assert.Equal(0, tracker.PerPeg);
            Assert.Equal(0, tracker.Total);
        }

        [Fact]
        public void Update_OutsideAShot_IsIgnored()
        {
            ScoreTracker tracker = new ScoreTracker();

            tracker.Update(pegs: 5, perPeg: 2, total: 10);

            Assert.Equal(ScorePhase.Ready, tracker.Phase);
            Assert.Equal(0, tracker.Pegs);
        }

        [Fact]
        public void CompleteShot_ShowsRealTotalForHoldTime()
        {
            // Given a shot of 10 pegs x 2 damage where relics add bonus damage
            ScoreTracker tracker = new ScoreTracker { ResultHoldSeconds = 3f };
            tracker.BeginShot(isHeal: false);
            tracker.Update(pegs: 10, perPeg: 2, total: 20);

            // When the balls finish and the game's total is 26
            tracker.CompleteShot(26);

            // Then the result is the game's real total, not pegs x damage
            Assert.Equal(ScorePhase.Result, tracker.Phase);
            Assert.Equal(26, tracker.Total);
            Assert.Equal(3f, tracker.ResultSecondsLeft);
        }

        [Fact]
        public void CompleteShot_WithoutAShot_IsIgnored()
        {
            ScoreTracker tracker = new ScoreTracker();

            tracker.CompleteShot(50);

            Assert.Equal(ScorePhase.Ready, tracker.Phase);
            Assert.Equal(0, tracker.Total);
        }

        [Fact]
        public void Tick_AfterHoldTime_ReturnsToZeroByZero()
        {
            ScoreTracker tracker = new ScoreTracker { ResultHoldSeconds = 3f };
            tracker.BeginShot(isHeal: false);
            tracker.Update(10, 2, 20);
            tracker.CompleteShot(20);

            tracker.Tick(2.9f);
            Assert.Equal(ScorePhase.Result, tracker.Phase);

            tracker.Tick(0.2f);
            Assert.Equal(ScorePhase.Ready, tracker.Phase);
            Assert.Equal(0, tracker.Total);
        }

        [Fact]
        public void BeginShot_DuringResult_ResetsImmediately()
        {
            // Given a result still on show
            ScoreTracker tracker = new ScoreTracker { ResultHoldSeconds = 3f };
            tracker.BeginShot(isHeal: false);
            tracker.Update(10, 2, 20);
            tracker.CompleteShot(20);
            tracker.Tick(1f);

            // When the next shot is fired before the 3 seconds are up
            tracker.BeginShot(isHeal: true);

            // Then it's straight back to counting from 0 x 0
            Assert.Equal(ScorePhase.Counting, tracker.Phase);
            Assert.Equal(0, tracker.Pegs);
            Assert.Equal(0, tracker.Total);
            Assert.True(tracker.IsHeal);
        }

        [Fact]
        public void RefineResult_UpdatesTotalWithoutRestartingTimer()
        {
            ScoreTracker tracker = new ScoreTracker { ResultHoldSeconds = 3f };
            tracker.BeginShot(isHeal: false);
            tracker.CompleteShot(20);
            tracker.Tick(1f);

            tracker.RefineResult(30);

            Assert.Equal(30, tracker.Total);
            Assert.Equal(2f, tracker.ResultSecondsLeft, 3);
        }

        [Fact]
        public void RefineResult_WhileCounting_IsIgnored()
        {
            ScoreTracker tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);
            tracker.Update(10, 2, 20);

            tracker.RefineResult(99);

            Assert.Equal(20, tracker.Total);
        }

        [Fact]
        public void LastShot_IsNullBeforeAnyShotFinishes()
        {
            ScoreTracker tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);
            tracker.Update(5, 2, 10);

            Assert.Null(tracker.LastShotTotal);
        }

        [Fact]
        public void LastShot_SurvivesTheResultTimingOutAndTheNextShot()
        {
            // Given a healing shot that finished and timed out
            ScoreTracker tracker = new ScoreTracker { ResultHoldSeconds = 1f };
            tracker.BeginShot(isHeal: true);
            tracker.CompleteShot(12);
            tracker.Tick(2f);

            // When the next shot starts
            tracker.BeginShot(isHeal: false);

            // Then the previous shot is still remembered, as healing
            Assert.Equal(12, tracker.LastShotTotal);
            Assert.True(tracker.LastShotWasHeal);
        }

        [Fact]
        public void LastShot_FollowsRefinedTotal()
        {
            ScoreTracker tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);
            tracker.CompleteShot(20);

            tracker.RefineResult(30);

            Assert.Equal(30, tracker.LastShotTotal);
        }

        [Fact]
        public void Clear_DropsHealFlag()
        {
            ScoreTracker tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: true);

            tracker.Clear();

            Assert.False(tracker.IsHeal);
            Assert.Equal(ScorePhase.Ready, tracker.Phase);
        }
    }
}
