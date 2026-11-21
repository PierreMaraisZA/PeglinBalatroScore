using System;
using System.Collections.Generic;
using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    public class TargetProviderRegistryTests
    {
        private readonly List<string> _warnings = new List<string>();

        private TargetProviderRegistry NewRegistry() => new TargetProviderRegistry(_warnings.Add);

        [Fact]
        public void Resolve_NothingRegistered_IsZero()
        {
            Assert.Equal(0, NewRegistry().Resolve());
        }

        [Fact]
        public void Resolve_UsesThePositiveValueExactly_EvenWhenSmall()
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("a", () => 1);

            Assert.Equal(1, registry.Resolve());
        }

        [Fact]
        public void Resolve_FirstPositiveInRegistrationOrderWins()
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("a", () => 0);
            registry.Set("b", () => 500);
            registry.Set("c", () => 900);

            Assert.Equal(500, registry.Resolve());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(long.MinValue)]
        public void Resolve_ZeroOrNegativeIsNoOpinion(long value)
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("a", () => value);
            registry.Set("b", () => 70);

            Assert.Equal(70, registry.Resolve());
        }

        [Fact]
        public void Resolve_AllNoOpinion_IsZero()
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("a", () => 0);
            registry.Set("b", () => -5);

            Assert.Equal(0, registry.Resolve());
        }

        [Fact]
        public void Set_SameGuidReplacesAndKeepsItsPlace()
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("a", () => 100);
            registry.Set("b", () => 200);

            registry.Set("a", () => 300);

            Assert.Equal(2, registry.Count);
            Assert.Equal(300, registry.Resolve());
        }

        [Fact]
        public void Set_AsksTheProviderEveryTime_NotOnce()
        {
            TargetProviderRegistry registry = NewRegistry();
            long current = 100;
            registry.Set("a", () => current);

            Assert.Equal(100, registry.Resolve());
            current = 40;
            Assert.Equal(40, registry.Resolve());
        }

        [Fact]
        public void Set_NullProviderRemovesTheRegistration()
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("a", () => 100);

            registry.Set("a", null);

            Assert.Equal(0, registry.Count);
            Assert.Equal(0, registry.Resolve());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Set_NullOrEmptyGuidIsIgnored(string guid)
        {
            TargetProviderRegistry registry = NewRegistry();

            registry.Set(guid, () => 100);

            Assert.Equal(0, registry.Count);
            Assert.Equal(0, registry.Resolve());
        }

        [Fact]
        public void RemoveAll_RemovesOnlyThatGuid()
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("a", () => 100);
            registry.Set("b", () => 200);

            registry.RemoveAll("a");

            Assert.Equal(200, registry.Resolve());
        }

        [Fact]
        public void RemoveAll_UnknownOrNullGuid_DoesNothing()
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("a", () => 100);

            registry.RemoveAll("nobody");
            registry.RemoveAll(null);

            Assert.Equal(100, registry.Resolve());
        }

        [Fact]
        public void Resolve_ThrowingProviderIsNoOpinion_AndLoggedOnce()
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("bad", () => throw new InvalidOperationException("boom"));
            registry.Set("good", () => 250);

            Assert.Equal(250, registry.Resolve());
            Assert.Equal(250, registry.Resolve());
            Assert.Equal(250, registry.Resolve());

            Assert.Single(_warnings);
            Assert.Contains("bad", _warnings[0]);
            Assert.Contains("boom", _warnings[0]);
        }

        [Fact]
        public void Resolve_OnlyThrowingProvider_IsZero()
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("bad", () => throw new Exception("boom"));

            Assert.Equal(0, registry.Resolve());
        }

        [Fact]
        public void Resolve_ReRegisteringAThrowingGuidLogsAgain()
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("bad", () => throw new Exception("one"));
            registry.Resolve();

            registry.Set("bad", () => throw new Exception("two"));
            registry.Resolve();

            Assert.Equal(2, _warnings.Count);
        }

        [Fact]
        public void Resolve_NoWarnCallback_StillSwallowsExceptions()
        {
            TargetProviderRegistry registry = new TargetProviderRegistry();
            registry.Set("bad", () => throw new Exception("boom"));

            Assert.Equal(0, registry.Resolve());
        }

        [Fact]
        public void Resolve_ProviderRemovingItselfDuringTheCall_DoesNotBreakTheWalk()
        {
            TargetProviderRegistry registry = NewRegistry();
            registry.Set("a", () =>
            {
                registry.RemoveAll("a");
                registry.Set("c", () => 999);
                return 0;
            });
            registry.Set("b", () => 200);

            Assert.Equal(200, registry.Resolve());
            // "a" is gone and "c" was added during the walk; both effects show up on the next call
            Assert.Equal(2, registry.Count);
            Assert.Equal(200, registry.Resolve());
        }
    }
}
