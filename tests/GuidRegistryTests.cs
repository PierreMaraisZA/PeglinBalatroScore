using System;
using System.Collections.Generic;
using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    public class GuidRegistryTests
    {
        private readonly List<string> _warnings = new List<string>();

        private GuidRegistry<Func<string>> NewRegistry() => new GuidRegistry<Func<string>>("test callback", _warnings.Add);

        private static List<string> CallAll(GuidRegistry<Func<string>> registry)
        {
            var seen = new List<string>();
            registry.Each(f => seen.Add(f()));
            return seen;
        }

        [Fact]
        public void Each_NothingRegistered_CallsNothing()
        {
            Assert.Empty(CallAll(NewRegistry()));
        }

        [Fact]
        public void Each_WalksInRegistrationOrder()
        {
            var registry = NewRegistry();
            registry.Set("a", () => "a");
            registry.Set("b", () => "b");
            registry.Set("c", () => "c");

            Assert.Equal(new[] { "a", "b", "c" }, CallAll(registry));
        }

        [Fact]
        public void Any_StopsAtTheFirstTrue_AndSaysSo()
        {
            var registry = NewRegistry();
            registry.Set("a", () => "a");
            registry.Set("b", () => "b");
            registry.Set("c", () => "c");
            var seen = new List<string>();

            bool any = registry.Any(f =>
            {
                seen.Add(f());
                return seen.Count == 2;
            });

            Assert.True(any);
            Assert.Equal(new[] { "a", "b" }, seen);
        }

        [Fact]
        public void Set_SameGuidReplacesAndKeepsItsPlace()
        {
            var registry = NewRegistry();
            registry.Set("a", () => "a");
            registry.Set("b", () => "b");
            registry.Set("a", () => "a2");

            Assert.Equal(new[] { "a2", "b" }, CallAll(registry));
            Assert.Equal(2, registry.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Set_NullOrEmptyGuid_IsIgnored(string guid)
        {
            var registry = NewRegistry();
            registry.Set(guid, () => "x");

            Assert.Equal(0, registry.Count);
        }

        [Fact]
        public void Set_NullCallback_RemovesTheGuid()
        {
            var registry = NewRegistry();
            registry.Set("a", () => "a");
            registry.Set("a", null);

            Assert.Equal(0, registry.Count);
        }

        [Fact]
        public void RemoveAll_RemovesOnlyThatGuid()
        {
            var registry = NewRegistry();
            registry.Set("a", () => "a");
            registry.Set("b", () => "b");
            registry.RemoveAll("a");
            registry.RemoveAll(null);
            registry.RemoveAll("unknown");

            Assert.Equal(new[] { "b" }, CallAll(registry));
        }

        [Fact]
        public void Each_ThrowingCallbackIsSkippedAndLoggedOnce()
        {
            var registry = NewRegistry();
            registry.Set("bad", () => throw new InvalidOperationException("boom"));
            registry.Set("good", () => "good");

            Assert.Equal(new[] { "good" }, CallAll(registry));
            Assert.Equal(new[] { "good" }, CallAll(registry));
            Assert.Single(_warnings);
            Assert.Contains("'bad'", _warnings[0]);
            Assert.Contains("test callback", _warnings[0]);
        }

        [Fact]
        public void Set_ReRegisteringAThrowingCallback_LogsAgainIfItStillFails()
        {
            var registry = NewRegistry();
            registry.Set("bad", () => throw new InvalidOperationException("one"));
            CallAll(registry);
            registry.Set("bad", () => throw new InvalidOperationException("two"));
            CallAll(registry);

            Assert.Equal(2, _warnings.Count);
        }

        [Fact]
        public void Each_CallbackMayRegisterOrRemoveDuringTheWalk()
        {
            var registry = NewRegistry();
            registry.Set("a", () =>
            {
                registry.RemoveAll("b");
                registry.Set("c", () => "c");
                return "a";
            });
            registry.Set("b", () => "b");

            Assert.Equal(new[] { "a", "b" }, CallAll(registry));
            Assert.Equal(new[] { "a", "c" }, CallAll(registry));
        }

        [Fact]
        public void Any_NoneTrue_IsFalse()
        {
            var registry = NewRegistry();
            registry.Set("a", () => "a");
            registry.Set("b", () => "b");

            Assert.False(registry.Any(f => f() == "z"));
        }

        [Fact]
        public void Any_Empty_IsFalse()
        {
            Assert.False(NewRegistry().Any(f => true));
        }

        [Fact]
        public void Any_ThrowingCallbackCountsAsFalse()
        {
            var registry = NewRegistry();
            registry.Set("bad", () => throw new InvalidOperationException());

            Assert.False(registry.Any(f => f() == "x"));
            Assert.Single(_warnings);
        }

        [Fact]
        public void Any_ThrowingCallbackBeforeATrueOne_StillFindsIt()
        {
            var registry = NewRegistry();
            registry.Set("bad", () => throw new InvalidOperationException());
            registry.Set("good", () => "hide");

            Assert.True(registry.Any(f => f() == "hide"));
        }

        [Fact]
        public void Constructor_NullWarn_ThrowingCallbackStillSkipped()
        {
            var registry = new GuidRegistry<Func<string>>("test", null);
            registry.Set("bad", () => throw new Exception());

            Assert.Empty(CallAll(registry));
        }
    }
}
