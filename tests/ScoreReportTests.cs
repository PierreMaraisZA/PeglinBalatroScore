using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    /// <summary>The one-time report of a finished shot that BalatroScoreApi's listeners get.</summary>
    public class ScoreReportTests
    {
        [Fact]
        public void NewTracker_HasNothingToReport()
        {
            var tracker = new ScoreTracker();

            Assert.False(tracker.ReportPending);
            Assert.False(tracker.TakeReport(out _, out _));
        }

        [Fact]
        public void WhileCounting_NothingToReport()
        {
            var tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);
            tracker.Update(3, 4, 12);

            Assert.False(tracker.TakeReport(out _, out _));
        }

        [Fact]
        public void CompletedShot_IsReportedExactlyOnce()
        {
            var tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);
            tracker.CompleteShot(120);

            Assert.True(tracker.TakeReport(out long total, out bool heal));
            Assert.Equal(120, total);
            Assert.False(heal);
            Assert.False(tracker.TakeReport(out _, out _));
        }

        [Fact]
        public void RefinedResult_ReportsTheRefinedTotal()
        {
            var tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);
            tracker.CompleteShot(120);
            tracker.RefineResult(150);

            Assert.True(tracker.TakeReport(out long total, out _));
            Assert.Equal(150, total);
        }

        [Fact]
        public void HealShot_IsReportedAsHeal()
        {
            var tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: true);
            tracker.CompleteShot(9);

            Assert.True(tracker.TakeReport(out long total, out bool heal));
            Assert.Equal(9, total);
            Assert.True(heal);
        }

        [Fact]
        public void ResultTimingOut_StillReportsTheShot()
        {
            var tracker = new ScoreTracker { ResultHoldSeconds = 1f };
            tracker.BeginShot(isHeal: false);
            tracker.CompleteShot(77);
            tracker.Tick(2f);

            Assert.Equal(ScorePhase.Ready, tracker.Phase);
            Assert.True(tracker.TakeReport(out long total, out _));
            Assert.Equal(77, total);
        }

        [Fact]
        public void AttackAfterTheResultTimedOut_StillRefinesTheReport()
        {
            var tracker = new ScoreTracker { ResultHoldSeconds = 0.5f };
            tracker.BeginShot(isHeal: false);
            tracker.CompleteShot(100);
            tracker.Tick(1f);
            tracker.RefineResult(130);

            Assert.Equal(ScorePhase.Ready, tracker.Phase);
            Assert.Equal(0, tracker.Total);
            Assert.True(tracker.TakeReport(out long total, out _));
            Assert.Equal(130, total);
        }

        [Fact]
        public void RefineAfterTheReport_DoesNotChangeTheLastShot()
        {
            var tracker = new ScoreTracker { ResultHoldSeconds = 0.5f };
            tracker.BeginShot(isHeal: false);
            tracker.CompleteShot(100);
            tracker.Tick(1f);
            tracker.TakeReport(out _, out _);
            tracker.RefineResult(130);

            Assert.Equal(100, tracker.LastShotTotal);
        }

        [Fact]
        public void RefineWithNoShotEver_DoesNothing()
        {
            var tracker = new ScoreTracker();
            tracker.RefineResult(50);

            Assert.Null(tracker.LastShotTotal);
            Assert.False(tracker.TakeReport(out _, out _));
        }

        [Fact]
        public void ShotCutOffMidFlight_IsNotReported()
        {
            var tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);
            tracker.Update(2, 5, 10);
            tracker.Clear();

            Assert.False(tracker.TakeReport(out _, out _));
        }

        [Fact]
        public void EachShot_IsReportedSeparately()
        {
            var tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);
            tracker.CompleteShot(10);
            tracker.TakeReport(out _, out _);
            tracker.BeginShot(isHeal: false);
            tracker.CompleteShot(20);

            Assert.True(tracker.TakeReport(out long total, out _));
            Assert.Equal(20, total);
        }

        [Fact]
        public void CompleteShotTwice_IsStillOneReport()
        {
            var tracker = new ScoreTracker();
            tracker.BeginShot(isHeal: false);
            tracker.CompleteShot(10);
            tracker.CompleteShot(99);

            Assert.True(tracker.TakeReport(out long total, out _));
            Assert.Equal(10, total);
            Assert.False(tracker.TakeReport(out _, out _));
        }
    }
}
