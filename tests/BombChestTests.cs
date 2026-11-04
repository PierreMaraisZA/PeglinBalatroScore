using System;
using System.Collections.Generic;
using PeglinBalatroScore.Core;
using Xunit;

namespace PeglinBalatroScore.Tests
{
    public class BombPhysicsTests
    {
        /// <summary>Drops bombs the way the plugin does: over the chest's opening, a little apart in time.</summary>
        private static BombPhysics DropBombs(int count, int seed = 5)
        {
            BombPhysics world = new BombPhysics { SpillHeight = BombChestShape.SpillHeight, SpillPush = BombChestShape.SpillPush };
            float radius = BombChestShape.BombRadius;
            Random random = new Random(seed);
            BombChestShape.BuildWalls(world.Segments);
            for (int i = 0; i < count; i++)
            {
                float x = BombChestShape.DropX((float)(random.NextDouble() * 2 - 1));
                world.AddBall(x, world.ClearDropHeight(x, radius, BombChestShape.OuterHeight + 60f), radius);
                world.Advance(BombChestTimeline.DropInterval);
            }
            for (int i = 0; i < 600; i++)
            {
                world.Advance(1f / 60f);
            }
            return world;
        }

        private static bool InsideChest(PhysicsBall ball)
        {
            return Math.Abs(ball.X) < BombChestShape.InnerWidth / 2f && ball.Y > BombChestShape.Wall;
        }

        private static bool BesideChest(PhysicsBall ball)
        {
            return Math.Abs(ball.X) > BombChestShape.OuterWidth / 2f;
        }

        [Fact]
        public void OneBomb_SettlesOnTheChestFloor()
        {
            BombPhysics world = DropBombs(1);
            PhysicsBall ball = world.Balls[0];

            Assert.True(InsideChest(ball));
            Assert.Equal(BombChestShape.Wall + ball.Radius, ball.Y, 0);
            Assert.True(world.IsSettled());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(13)]
        [InlineData(29)]
        public void AChestFull_StaysInsideBelowTheRim(int seed)
        {
            BombPhysics world = DropBombs(BombChestShape.FillsAt, seed);

            foreach (PhysicsBall ball in world.Balls)
            {
                Assert.True(InsideChest(ball), $"bomb at ({ball.X:0.0}, {ball.Y:0.0}) should be inside the chest");
                Assert.True(ball.Y + ball.Radius <= BombChestShape.OuterHeight + 1f, "a chest-full shouldn't stick out of the chest");
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(20)]
        public void TooManyBombs_OverflowRollsOffOntoTheGroundBesideTheChest(int seed)
        {
            // Given far more bombs than the chest holds, all dropped into it
            BombPhysics world = DropBombs(BombChestShape.FillsAt * 3, seed);

            // Then the chest is full, the rest have rolled off beside it onto the ground, and none flew off-screen
            int inside = 0;
            int beside = 0;
            foreach (PhysicsBall ball in world.Balls)
            {
                if (InsideChest(ball))
                {
                    inside++;
                }
                else if (BesideChest(ball))
                {
                    beside++;
                    Assert.True(ball.Y >= ball.Radius - 0.5f, "spilled bombs should rest on the ground, not sink into it");
                    Assert.True(Math.Abs(ball.X) < 250f, $"bomb at x {ball.X:0} rolled too far away");
                }
            }
            Assert.True(inside >= BombChestShape.FillsAt - 1, $"chest should stay full, has {inside}");
            Assert.True(beside >= 5, $"expected the overflow beside the chest, got {beside}");
        }

        [Fact]
        public void Bombs_DoNotSinkIntoEachOther()
        {
            BombPhysics world = DropBombs(BombChestShape.FillsAt);

            for (int i = 0; i < world.Balls.Count; i++)
            {
                for (int j = i + 1; j < world.Balls.Count; j++)
                {
                    PhysicsBall a = world.Balls[i];
                    PhysicsBall b = world.Balls[j];
                    float distance = (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
                    Assert.True(distance > (a.Radius + b.Radius) * 0.9f, "bombs overlap too much");
                }
            }
        }

        [Fact]
        public void DropX_IsAlwaysOverTheOpening()
        {
            float limit = BombChestShape.InnerWidth / 2f - BombChestShape.BombRadius;

            Assert.Equal(limit, BombChestShape.DropX(5f), 3);
            Assert.Equal(-limit, BombChestShape.DropX(-1f), 3);
            Assert.Equal(0f, BombChestShape.DropX(0f), 3);
        }

        [Fact]
        public void GroundOnly_LetsBombsFallOutOnceTheChestBursts()
        {
            // Given bombs settled in the chest
            BombPhysics world = DropBombs(4);

            // When the chest is gone
            BombChestShape.BuildGroundOnly(world.Segments);
            for (int i = 0; i < 120; i++)
            {
                world.Advance(1f / 60f);
            }

            // Then they drop to the ground
            foreach (PhysicsBall ball in world.Balls)
            {
                Assert.True(ball.Y < BombChestShape.Wall + ball.Radius, "bombs should fall to the ground once the chest has burst");
            }
        }

        [Fact]
        public void Advance_SameInputs_IsRepeatable()
        {
            BombPhysics a = DropBombs(6, seed: 3);
            BombPhysics b = DropBombs(6, seed: 3);

            for (int i = 0; i < a.Balls.Count; i++)
            {
                Assert.Equal(a.Balls[i].X, b.Balls[i].X);
                Assert.Equal(a.Balls[i].Y, b.Balls[i].Y);
            }
        }

        [Fact]
        public void ClearDropHeight_StartsAboveABallAlreadyThere()
        {
            BombPhysics world = new BombPhysics();
            world.AddBall(0f, 100f, 8f);

            float y = world.ClearDropHeight(3f, 8f, 50f);

            Assert.True(y > 100f + 15f, $"drop height {y} would overlap the ball at 100");
            Assert.Equal(50f, world.ClearDropHeight(40f, 8f, 50f));
        }

        [Fact]
        public void MaxSpeed_StopsOverlapsFlingingBalls()
        {
            // Given two balls spawned almost on top of each other
            BombPhysics world = new BombPhysics();
            world.AddBall(0f, 200f, 8f);
            world.AddBall(1f, 201f, 8f);

            world.Step(BombPhysics.StepSeconds);

            foreach (PhysicsBall ball in world.Balls)
            {
                float speed = (float)Math.Sqrt(Math.Pow(ball.VelocityX(BombPhysics.StepSeconds / 4), 2) + Math.Pow(ball.VelocityY(BombPhysics.StepSeconds / 4), 2));
                Assert.True(speed <= world.MaxSpeed + 1f, $"speed {speed} over the cap");
            }
        }
    }

    public class BombChestTimelineTests
    {
        private static List<ChestEvent> Tick(BombChestTimeline chest, float seconds)
        {
            List<ChestEvent> events = new List<ChestEvent>();
            chest.Tick(seconds, events);
            return events;
        }

        private static List<ChestEventKind> Kinds(List<ChestEvent> events)
        {
            return events.ConvertAll(e => e.Kind);
        }

        /// <summary>A chest with this many bombs dropped in and waiting for Peglin to throw.</summary>
        private static BombChestTimeline Filled(int bombs)
        {
            BombChestTimeline chest = new BombChestTimeline();
            for (int i = 0; i < bombs; i++)
            {
                chest.BombAdded(false);
            }
            for (int i = 0; i < bombs; i++)
            {
                Tick(chest, BombChestTimeline.DropInterval);
            }
            return chest;
        }

        [Fact]
        public void StartsHidden()
        {
            Assert.Equal(ChestPhase.Hidden, new BombChestTimeline().Phase);
        }

        [Fact]
        public void BombAdded_ShowsAnEmptyChestThenDropsOneBombEach()
        {
            // Given three bombs lit during a shot, one rigged
            BombChestTimeline chest = new BombChestTimeline();
            chest.BombAdded(rigged: true);
            chest.BombAdded(rigged: false);
            chest.BombAdded(rigged: false);
            Assert.Equal(ChestPhase.Filling, chest.Phase);

            // Then they drop in one at a time, in order, a short gap apart
            List<ChestEvent> drops = new List<ChestEvent>();
            for (int i = 0; i < 10; i++)
            {
                drops.AddRange(Tick(chest, 0.05f));
            }
            Assert.Equal(3, drops.Count);
            Assert.True(drops[0].Rigged);
            Assert.False(drops[1].Rigged);
            Assert.Equal(3, chest.Dropped);
        }

        [Fact]
        public void Filling_WaitsForPeglinsThrowHoweverLongItTakes()
        {
            BombChestTimeline chest = Filled(2);

            List<ChestEvent> events = Tick(chest, 30f);

            Assert.Empty(events);
            Assert.Equal(ChestPhase.Filling, chest.Phase);
        }

        [Fact]
        public void ThrowFinished_BurstsOnTheSameTickAsTheLastBomb()
        {
            // Given a chest whose bombs have all been thrown and their damage counted
            BombChestTimeline chest = Filled(3);
            chest.AddDamage(50);

            // When the game reports the last bomb has detonated
            chest.ThrowFinished();

            // Then the chest goes off straight away, with no fuse of its own
            Assert.Equal(ChestPhase.Exploded, chest.Phase);
            List<ChestEventKind> events = Kinds(Tick(chest, 0f));
            Assert.Equal(new[] { ChestEventKind.Burst, ChestEventKind.DetonateAll, ChestEventKind.ShowTotal }, events);
            Assert.Equal(50, chest.Damage);
        }

        [Fact]
        public void ThrowFinished_AgainAfterTheBurst_DoesNotExplodeTwice()
        {
            BombChestTimeline chest = Filled(2);
            chest.ThrowFinished();
            Tick(chest, 0.05f);

            chest.ThrowFinished();

            Assert.Empty(Tick(chest, 0f));
            Assert.Equal(ChestPhase.Exploded, chest.Phase);
        }

        [Fact]
        public void ThrowFinished_OnALongFrame_StillShowsTheBurstBeforeHiding()
        {
            // Given the chest bursting and a frame so long the aftermath timer would run out in one go
            BombChestTimeline chest = Filled(1);
            chest.ThrowFinished();

            // Then the burst is shown, not swallowed by the same tick's Hide
            List<ChestEventKind> events = Kinds(Tick(chest, BombChestTimeline.AfterExplosionSeconds + 1f));
            Assert.Equal(new[] { ChestEventKind.Burst, ChestEventKind.DetonateAll, ChestEventKind.ShowTotal }, events);

            // And it hides once the aftermath has had its time
            Assert.Equal(ChestEventKind.Hide, Tick(chest, BombChestTimeline.AfterExplosionSeconds)[0].Kind);
        }

        [Fact]
        public void ThrowFinished_BeforeAllHaveDropped_DropsTheRestThenBursts()
        {
            // Given four bombs lit but only one dropped so far
            BombChestTimeline chest = new BombChestTimeline();
            for (int i = 0; i < 4; i++)
            {
                chest.BombAdded(false);
            }
            Tick(chest, 0f);
            Assert.Equal(1, chest.Dropped);

            // When the last of Peglin's bombs detonates
            chest.ThrowFinished();
            List<ChestEventKind> events = Kinds(Tick(chest, 0f));

            // Then the rest drop in at once, then the chest goes off with them
            Assert.Equal(new[]
            {
                ChestEventKind.Drop, ChestEventKind.Drop, ChestEventKind.Drop,
                ChestEventKind.Burst, ChestEventKind.DetonateAll, ChestEventKind.ShowTotal,
            }, events);
            Assert.Equal(4, chest.Dropped);
            Assert.Equal(ChestPhase.Exploded, chest.Phase);
        }

        [Fact]
        public void ThrowFinished_WithNoChest_IsIgnored()
        {
            BombChestTimeline chest = new BombChestTimeline();

            chest.ThrowFinished();

            Assert.Empty(Tick(chest, 0f));
            Assert.Equal(ChestPhase.Hidden, chest.Phase);
        }

        [Fact]
        public void ThrowFinished_PendingBombsGoInWhereTheyWouldHaveLanded()
        {
            // Given four bombs lit but only one dropped before the last real bomb goes off
            BombChestTimeline chest = new BombChestTimeline();
            for (int i = 0; i < 4; i++)
            {
                chest.BombAdded(false);
            }
            Tick(chest, 0f);
            chest.ThrowFinished();

            // Then the rest are placed inside the chest, not left falling from above the blast
            List<ChestEvent> events = Tick(chest, 0f);
            List<ChestEvent> drops = events.FindAll(e => e.Kind == ChestEventKind.Drop);
            Assert.Equal(3, drops.Count);
            Assert.True(drops.TrueForAll(drop => drop.Instant));
        }

        [Fact]
        public void EveryNewChestGetsANewId()
        {
            BombChestTimeline chest = Filled(1);
            int first = chest.ChestId;
            chest.ThrowFinished();
            Tick(chest, BombChestTimeline.AfterExplosionSeconds + 0.01f);

            chest.BombAdded(false);

            Assert.NotEqual(first, chest.ChestId);
        }

        [Fact]
        public void SeveralBombsDroppedInOneFrame_StillBelongToTheSameChest()
        {
            // Given a long frame that drops several of a new chest's bombs at once (the plugin resets on a new id)
            BombChestTimeline chest = new BombChestTimeline();
            chest.BombAdded(false);
            chest.BombAdded(false);
            chest.BombAdded(false);

            List<ChestEvent> events = Tick(chest, BombChestTimeline.DropInterval * 3);

            Assert.Equal(3, events.FindAll(e => e.Kind == ChestEventKind.Drop).Count);
            Assert.Equal(1, chest.ChestId);
        }

        [Fact]
        public void AfterTheExplosion_HidesAndTheNextBombStartsAFreshChest()
        {
            BombChestTimeline chest = Filled(1);
            chest.ThrowFinished();
            chest.AddDamage(50);
            Tick(chest, 0f);

            List<ChestEvent> events = Tick(chest, BombChestTimeline.AfterExplosionSeconds + 0.01f);

            Assert.Equal(ChestEventKind.Hide, events[0].Kind);
            Assert.Equal(ChestPhase.Hidden, chest.Phase);

            chest.BombAdded(false);
            Assert.Equal(0, chest.Dropped);
            Assert.Equal(0, chest.Damage);
        }

        [Fact]
        public void AddDamage_SumsWhileShownAndIgnoresWhenHidden()
        {
            BombChestTimeline chest = new BombChestTimeline();
            chest.AddDamage(100);
            Assert.Equal(0, chest.Damage);

            chest.BombAdded(false);
            chest.AddDamage(50);
            chest.AddDamage(200);
            chest.AddDamage(-5);

            Assert.Equal(250, chest.Damage);
        }

        [Fact]
        public void AddDamage_SaturatesInsteadOfOverflowing()
        {
            BombChestTimeline chest = new BombChestTimeline();
            chest.BombAdded(false);
            chest.AddDamage(long.MaxValue - 10);
            chest.AddDamage(100);

            Assert.Equal(long.MaxValue, chest.Damage);
        }
    }

    public class ChestDebrisTests
    {
        [Fact]
        public void Burst_FliesOutThenComesToRestOnTheGround()
        {
            ChestDebris debris = new ChestDebris();
            debris.Burst(planks: 8, splinters: 12, seed: 4);
            Assert.Equal(20, debris.Pieces.Count);

            for (int i = 0; i < 600; i++)
            {
                debris.Step(1f / 60f);
            }

            foreach (DebrisPiece piece in debris.Pieces)
            {
                Assert.True(piece.Y >= 0f);
                Assert.Equal(0f, piece.VelocityY);
            }
        }

        [Fact]
        public void Shatter_FliesAwayFromTheCentreAndUp()
        {
            // Given the two ends of a number shattering from its middle
            ChestDebris debris = new ChestDebris();
            Random random = new Random(2);
            DebrisPiece left = debris.Shatter(-20f, 40f, 0f, 40f, random);
            DebrisPiece right = debris.Shatter(20f, 40f, 0f, 40f, random);

            // Then they head outward on their own side, and upward
            Assert.True(left.VelocityX < 0f);
            Assert.True(right.VelocityX > 0f);
            Assert.True(left.VelocityY > 0f && right.VelocityY > 0f);
            Assert.Equal(2, debris.Pieces.Count);
        }

        [Fact]
        public void Shatter_PieceAtTheCentre_StillMoves()
        {
            ChestDebris debris = new ChestDebris();

            DebrisPiece piece = debris.Shatter(0f, 40f, 0f, 40f, new Random(1));

            Assert.True(piece.VelocityY > 0f);
        }

        [Fact]
        public void Burst_ThrowsPiecesBothWays()
        {
            ChestDebris debris = new ChestDebris();
            debris.Burst(planks: 10, splinters: 10, seed: 9);
            for (int i = 0; i < 30; i++)
            {
                debris.Step(1f / 60f);
            }

            Assert.Contains(debris.Pieces, p => p.X < -BombChestShape.OuterWidth);
            Assert.Contains(debris.Pieces, p => p.X > BombChestShape.OuterWidth);
        }
    }
}
