using System;
using System.Collections.Generic;

namespace PeglinBalatroScore.Core
{
    /// <summary>
    /// The chest's shape in its own units: origin at the middle of the chest's bottom edge, Y up, sitting on the
    /// ground at Y = 0. There's no limit on bombs: every one drops into the chest, and once the pile is over the rim
    /// the physics rolls the extras off the top and down the sides.
    /// </summary>
    public static class BombChestShape
    {
        public const float InnerWidth = 64f;
        public const float InnerHeight = 44f;
        public const float Wall = 5f;

        /// <summary>
        /// Bombs are sized for this many to fill the chest. A settled pile packs tighter than that sizing assumes, so in
        /// practice around 15 fit before the heap tops the rim and the rest spill over.
        /// </summary>
        public const int FillsAt = 10;

        /// <summary>How hard bombs heaped over the rim get nudged off the sides (see BombPhysics.SpillPush).</summary>
        public const float SpillPush = 2000f;

        public static float OuterWidth => InnerWidth + 2 * Wall;

        public static float OuterHeight => InnerHeight + Wall;

        /// <summary>
        /// Share of the inside a chest-full of round bombs takes up. A loose pile of same-size balls leaves plenty of
        /// gaps, so this is set low enough that FillsAt bombs settle just below the rim.
        /// </summary>
        private const float PackingDensity = 0.5f;

        public static float BombRadius => (float)Math.Sqrt(PackingDensity * InnerWidth * InnerHeight / (FillsAt * Math.PI));

        /// <summary>The ground, and the chest's floor and walls (inside face, top and outside face of each wall).</summary>
        public static void BuildWalls(IList<PhysicsSegment> segments)
        {
            segments.Clear();
            float half = InnerWidth / 2f;
            float top = OuterHeight;
            segments.Add(Ground);
            segments.Add(new PhysicsSegment(-half, Wall, half, Wall));
            // Left wall: inside face, top, outside face.
            segments.Add(new PhysicsSegment(-half, Wall, -half, top));
            segments.Add(new PhysicsSegment(-half - Wall, top, -half, top));
            segments.Add(new PhysicsSegment(-half - Wall, 0f, -half - Wall, top));
            // Right wall.
            segments.Add(new PhysicsSegment(half, Wall, half, top));
            segments.Add(new PhysicsSegment(half, top, half + Wall, top));
            segments.Add(new PhysicsSegment(half + Wall, 0f, half + Wall, top));
        }

        /// <summary>
        /// Height above which a bomb is heaped over the rim and gets nudged off the sides: its centre is clear of the
        /// rim by a bit, so bombs still inside the chest (even the top layer) are left alone.
        /// </summary>
        public static float SpillHeight => OuterHeight + BombRadius * 0.4f;

        /// <summary>Just the ground: what's left for the bombs to roll on once the chest has burst.</summary>
        public static void BuildGroundOnly(IList<PhysicsSegment> segments)
        {
            segments.Clear();
            segments.Add(Ground);
        }

        private static PhysicsSegment Ground => new PhysicsSegment(-2000f, 0f, 2000f, 0f);

        /// <summary>
        /// Where a bomb drops from: always over the chest's opening, so it lands in the chest (or on the pile, and
        /// rolls off if the pile is already over the rim). spread runs from -1 to 1 (random in the plugin).
        /// </summary>
        public static float DropX(float spread)
        {
            return Math.Max(-1f, Math.Min(1f, spread)) * (InnerWidth / 2f - BombRadius);
        }
    }

    public enum ChestPhase
    {
        /// <summary>Nothing on screen.</summary>
        Hidden,

        /// <summary>Bombs are dropping in, one for each bomb lit, until Peglin throws them.</summary>
        Filling,

        /// <summary>Everything has gone off; debris and the damage total are on show.</summary>
        Exploded
    }

    public enum ChestEventKind
    {
        /// <summary>Drop a bomb in from above (Rigged says which kind).</summary>
        Drop,

        /// <summary>The chest bursts into planks on the frame the last real bomb detonates.</summary>
        Burst,

        /// <summary>Set off every bomb left in the chest at once.</summary>
        DetonateAll,

        /// <summary>Pop the bombs' damage total out of the wreckage.</summary>
        ShowTotal,

        /// <summary>Fade everything away; the chest is done.</summary>
        Hide
    }

    public struct ChestEvent
    {
        public ChestEventKind Kind;
        public bool Rigged;

        /// <summary>A Drop that goes straight into the chest (the last bomb has already gone off) rather than falling in.</summary>
        public bool Instant;

        public ChestEvent(ChestEventKind kind, bool rigged = false, bool instant = false)
        {
            Kind = kind;
            Rigged = rigged;
            Instant = instant;
        }
    }

    /// <summary>
    /// What the bomb chest is doing, on real time. It appears empty when the first bomb of a turn is lit, and a bomb
    /// drops in for each one. When Peglin's last thrown bomb detonates, the chest bursts on that same frame and every
    /// bomb left in it goes off with it, then the damage total pops. Pure C# so it can be unit tested; the plugin turns
    /// its events into physics and pixels.
    /// </summary>
    public class BombChestTimeline
    {
        /// <summary>Gap between bombs dropping in, so a burst of bombs doesn't land as one lump.</summary>
        public const float DropInterval = 0.12f;

        /// <summary>How long the damage number holds after it pops before it bursts into pieces too.</summary>
        public const float NumberShatterSeconds = 1.5f;

        /// <summary>How long the debris and number stay up after everything has gone off (long enough for pieces to land).</summary>
        public const float AfterExplosionSeconds = 3.2f;

        private readonly Queue<bool> _pending = new Queue<bool>();
        private readonly List<ChestEvent> _queued = new List<ChestEvent>();
        private float _dropTimer;
        private float _timer;

        public ChestPhase Phase { get; private set; } = ChestPhase.Hidden;

        /// <summary>Which chest this is, so the plugin can tell a fresh chest from additions to the last one.</summary>
        public int ChestId { get; private set; }

        /// <summary>Bombs dropped into this chest so far.</summary>
        public int Dropped { get; private set; }

        /// <summary>Total damage of the thrown bombs since the chest appeared.</summary>
        public long Damage { get; private set; }

        /// <summary>A bomb was lit: drop one into the chest, starting a fresh chest if there isn't one.</summary>
        public void BombAdded(bool rigged)
        {
            if (Phase == ChestPhase.Hidden || Phase == ChestPhase.Exploded)
            {
                Phase = ChestPhase.Filling;
                ChestId++;
                Dropped = 0;
                Damage = 0;
                _pending.Clear();
                _queued.Clear();
                _dropTimer = 0f;
            }
            _pending.Enqueue(rigged);
        }

        /// <summary>
        /// Peglin's last thrown bomb has detonated (or the battle ended before he threw them): burst the chest now.
        /// The game's bomb counters only reach zero at the instant of the last real explosion, so no fuse of our own
        /// is needed; bursting here lands the chest on the same frame as the bomb that set it off.
        /// </summary>
        public void ThrowFinished()
        {
            if (Phase != ChestPhase.Filling)
            {
                return;
            }
            DropEverythingNow();
            _queued.Add(new ChestEvent(ChestEventKind.Burst));
            _queued.Add(new ChestEvent(ChestEventKind.DetonateAll));
            _queued.Add(new ChestEvent(ChestEventKind.ShowTotal));
            Phase = ChestPhase.Exploded;
            _timer = AfterExplosionSeconds;
        }

        /// <summary>A thrown bomb's damage (counted once per bomb).</summary>
        public void AddDamage(long amount)
        {
            if (Phase == ChestPhase.Hidden || amount <= 0)
            {
                return;
            }
            Damage = Damage > long.MaxValue - amount ? long.MaxValue : Damage + amount;
        }

        /// <summary>Advance by real time, collecting what the plugin should do this frame.</summary>
        public void Tick(float seconds, List<ChestEvent> events)
        {
            if (Phase == ChestPhase.Filling)
            {
                _dropTimer -= seconds;
                while (_pending.Count > 0 && _dropTimer <= 0f)
                {
                    events.Add(new ChestEvent(ChestEventKind.Drop, _pending.Dequeue()));
                    Dropped++;
                    _dropTimer += DropInterval;
                }
                if (_pending.Count == 0)
                {
                    _dropTimer = Math.Max(_dropTimer, 0f);
                }
            }

            // ThrowFinished queues the burst; don't also burn the aftermath timer on that same tick, or a long
            // frame (a hitch) could hide the debris and damage number before they are ever shown.
            bool bursting = _queued.Count > 0;
            events.AddRange(_queued);
            _queued.Clear();

            if (Phase == ChestPhase.Exploded && !bursting)
            {
                _timer -= seconds;
                if (_timer <= 0f)
                {
                    events.Add(new ChestEvent(ChestEventKind.Hide));
                    Phase = ChestPhase.Hidden;
                }
            }
        }

        /// <summary>Drop everything at once (battle over).</summary>
        public void Reset()
        {
            Phase = ChestPhase.Hidden;
            Dropped = 0;
            Damage = 0;
            _pending.Clear();
            _queued.Clear();
        }

        /// <summary>Any bombs still waiting to drop go in straight away, ahead of the blast.</summary>
        private void DropEverythingNow()
        {
            while (_pending.Count > 0)
            {
                // These go in on the frame of the blast, so put them where they would have landed rather than
                // letting them explode in mid-air above the chest.
                _queued.Add(new ChestEvent(ChestEventKind.Drop, _pending.Dequeue(), instant: true));
                Dropped++;
            }
        }
    }

    /// <summary>One flying plank or splinter from the exploded chest.</summary>
    public class DebrisPiece
    {
        public float X;
        public float Y;
        public float VelocityX;
        public float VelocityY;
        public float Angle;
        public float Spin;
        public bool Splinter;
    }

    /// <summary>The chest's planks and splinters flying apart, bouncing and sliding to a stop on the ground (Y = 0).</summary>
    public class ChestDebris
    {
        private readonly List<DebrisPiece> _pieces = new List<DebrisPiece>();

        public float Gravity { get; set; } = -1400f;

        public float Bounce { get; set; } = 0.35f;

        public float GroundFriction { get; set; } = 0.75f;

        public IReadOnlyList<DebrisPiece> Pieces => _pieces;

        /// <summary>Blow the chest into planks (from its walls and floor) and smaller splinters, flying out and up.</summary>
        public void Burst(int planks, int splinters, int seed)
        {
            _pieces.Clear();
            Random random = new Random(seed);
            float half = BombChestShape.OuterWidth / 2f;
            for (int i = 0; i < planks + splinters; i++)
            {
                bool splinter = i >= planks;
                float x = (float)(random.NextDouble() * 2 - 1) * half;
                float y = (float)random.NextDouble() * BombChestShape.OuterHeight;
                // Outward from the chest's middle, always with some lift.
                float outward = x / half;
                float speed = (float)(splinter ? 350 + random.NextDouble() * 450 : 250 + random.NextDouble() * 300);
                _pieces.Add(new DebrisPiece
                {
                    X = x,
                    Y = y,
                    VelocityX = (outward + (float)(random.NextDouble() - 0.5)) * speed,
                    VelocityY = speed * (0.8f + (float)random.NextDouble() * 0.6f),
                    Angle = (float)(random.NextDouble() * Math.PI * 2),
                    Spin = (float)((random.NextDouble() * 2 - 1) * (splinter ? 25 : 12)),
                    Splinter = splinter,
                });
            }
        }

        /// <summary>
        /// Throw one more piece into the debris (a shard of the damage number, say), flying away from the given
        /// centre and upwards, so a set of them bursts apart from that point.
        /// </summary>
        public DebrisPiece Shatter(float x, float y, float centreX, float centreY, Random random)
        {
            float dx = x - centreX;
            float dy = y - centreY;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            float outX = length > 0.01f ? dx / length : (float)(random.NextDouble() * 2 - 1);
            float outY = length > 0.01f ? dy / length : 0f;
            float speed = 220f + (float)random.NextDouble() * 260f;
            DebrisPiece piece = new DebrisPiece
            {
                X = x,
                Y = y,
                VelocityX = outX * speed,
                VelocityY = Math.Max(outY, 0f) * speed + 200f + (float)random.NextDouble() * 200f,
                Spin = (float)((random.NextDouble() * 2 - 1) * 14),
            };
            _pieces.Add(piece);
            return piece;
        }

        public void Clear()
        {
            _pieces.Clear();
        }

        public void Step(float seconds)
        {
            foreach (DebrisPiece piece in _pieces)
            {
                piece.VelocityY += Gravity * seconds;
                piece.X += piece.VelocityX * seconds;
                piece.Y += piece.VelocityY * seconds;
                piece.Angle += piece.Spin * seconds;
                if (piece.Y < 0f)
                {
                    piece.Y = 0f;
                    piece.VelocityY = -piece.VelocityY * Bounce;
                    piece.VelocityX *= GroundFriction;
                    piece.Spin *= GroundFriction;
                    if (Math.Abs(piece.VelocityY) < 40f)
                    {
                        piece.VelocityY = 0f;
                    }
                }
            }
        }
    }
}
