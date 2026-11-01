using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    public class ScoreFormatTests
    {
        [Theory]
        [InlineData(0, "0")]
        [InlineData(-12, "0")]
        [InlineData(30, "30")]
        [InlineData(2410, "2,410")]
        [InlineData(99_999_999, "99,999,999")]
        [InlineData(123_456_789, "1.23e8")]
        [InlineData(long.MaxValue, "∞")]
        public void Format_MatchesBalatroAndPeglinStyle(long value, string expected)
        {
            Assert.Equal(expected, ScoreFormat.Format(value));
        }
    }
}
