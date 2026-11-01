using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    public class ScoreExplanationTests
    {
        [Fact]
        public void Build_ExplainsBothSidesAndTheFinalTotal()
        {
            string text = ScoreExplanation.Build(null, false);

            Assert.Contains("relics make a hit worth more", text);
            Assert.Contains("crit damage once a crit has been triggered", text);
            Assert.Contains("always what the game itself works out", text);
        }

        [Fact]
        public void Build_PutsABlankLineBetweenEachPart()
        {
            string[] lines = ScoreExplanation.Build(2410, false).Replace("\r", "").Split('\n');

            Assert.Equal(new[] { 1, 3, 5, 7 }, BlankLineIndexes(lines));
        }

        [Fact]
        public void Build_NoFlameTalk()
        {
            Assert.DoesNotContain("Flame", ScoreExplanation.Build(100, false));
        }

        [Fact]
        public void Build_BeforeAnyShot_LeavesOutLastShot()
        {
            string text = ScoreExplanation.Build(null, false);

            Assert.DoesNotContain("Last shot", text);
            Assert.False(text.EndsWith("\n"));
        }

        [Theory]
        [InlineData(2410L, false, "<b>Last shot:</b> 2,410 damage")]
        [InlineData(12L, true, "<b>Last shot:</b> 12 healing")]
        public void Build_EndsWithLastShot(long total, bool wasHeal, string expected)
        {
            Assert.EndsWith(expected, ScoreExplanation.Build(total, wasHeal));
        }

        private static int[] BlankLineIndexes(string[] lines)
        {
            System.Collections.Generic.List<int> blanks = new System.Collections.Generic.List<int>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                {
                    blanks.Add(i);
                }
            }
            return blanks.ToArray();
        }
    }
}
