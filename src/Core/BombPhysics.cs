using System;
using System.Collections.Generic;

namespace PeglinBalatroScore.Core
{
    /// <summary>A round body in the simulation: a bomb tumbling into the chest.</summary>
    public class PhysicsBall
    {
        public float X;
        public float Y;
        public float PreviousX;
        public float PreviousY;
        public float Radius;

        /// <summary>Spin in radians, from rolling along surfaces (for drawing only).</summary>
        public float Angle;

        /// <summary>Has come to rest on something (a wall, the floor or another ball) at least once since it was dropped.</summary>
        public bool Landed;

        public float VelocityX(float dt) => (X - PreviousX) / dt;

        public float VelocityY(float dt) => (Y - PreviousY) / dt;
    }

    /// <summary>A fixed wall, floor or slope the balls collide with.</summary>
    public struct PhysicsSegment
    {
        public float X1;
        public float Y1;
        public float X2;
        public float Y2;

        public PhysicsSegment(float x1, float y1, float x2, float y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }
    }

    /// <summary>
    /// A small Verlet physics world for the bomb pile, in the style of the coins in Kingdom's sack: balls fall, bounce
    /// a little, roll and pile up against walls and each other. Pure C# so it runs on real time independently of
    /// Unity's physics (and so game speed doesn't change it), and so it can be unit tested. Y points up.
    /// </summary>
    public class BombPhysics
    {
        /// <summary>Fixed step the world advances by, so it behaves the same at any frame rate.</summary>
        public const float StepSeconds = 1f / 120f;

        private const int SubSteps = 4;
        private const int SolverIterations = 4;

        private readonly List<PhysicsBall> _balls = new List<PhysicsBall>();
        private readonly List<PhysicsSegment> _segments = new List<PhysicsSegment>();
        private float _accumulator;

        public float Gravity { get; set; } = -1400f;

        /// <summary>
        /// Top speed, in units per second. Pushing overlapping balls apart shows up as speed in Verlet physics, so
        /// this keeps an awkward overlap from flinging a ball across the screen.
        /// </summary>
        public float MaxSpeed { get; set; } = 900f;

        /// <summary>
        /// Balls whose centre is above this height get nudged sideways, away from X = 0, so a heap piled over the rim
        /// slumps and spills off the sides instead of standing as a neat mound (like the jostling coins in a sack).
        /// Off by default.
        /// </summary>
        public float SpillHeight { get; set; } = float.MaxValue;

        /// <summary>Strength of that sideways nudge, in units per second squared.</summary>
        public float SpillPush { get; set; } = 700f;

        /// <summary>A ball counts as resting on the heap (and gets nudged) below this vertical speed.</summary>
        private const float SpillRestingSpeed = 60f;

        /// <summary>Share of sliding speed kept on each contact with a wall (1 = ice, 0 = glue).</summary>
        public float Friction { get; set; } = 0.9f;

        public IReadOnlyList<PhysicsBall> Balls => _balls;

        public IList<PhysicsSegment> Segments => _segments;

        public PhysicsBall AddBall(float x, float y, float radius, float velocityX = 0f, float velocityY = 0f)
        {
            PhysicsBall ball = new PhysicsBall
            {
                X = x,
                Y = y,
                PreviousX = x - velocityX * StepSeconds,
                PreviousY = y - velocityY * StepSeconds,
                Radius = radius,
            };
            _balls.Add(ball);
            return ball;
        }

        public void Clear()
        {
            _balls.Clear();
            _accumulator = 0f;
        }

        /// <summary>Take a ball out of the world (a bomb that has exploded).</summary>
        public void Remove(PhysicsBall ball)
        {
            _balls.Remove(ball);
        }

        /// <summary>A height at least minY where a new ball at x won't overlap any ball already in the world.</summary>
        public float ClearDropHeight(float x, float radius, float minY)
        {
            float y = minY;
            foreach (PhysicsBall ball in _balls)
            {
                float reach = ball.Radius + radius + 1f;
                float dx = Math.Abs(ball.X - x);
                if (dx < reach)
                {
                    float above = ball.Y + (float)Math.Sqrt(reach * reach - dx * dx);
                    y = Math.Max(y, above);
                }
            }
            return y;
        }

        /// <summary>Advance by real time, in as many fixed steps as fit.</summary>
        public void Advance(float seconds)
        {
            _accumulator = Math.Min(_accumulator + seconds, StepSeconds * 8);
            while (_accumulator >= StepSeconds)
            {
                _accumulator -= StepSeconds;
                Step(StepSeconds);
            }
        }

        /// <summary>True when every ball has (nearly) stopped moving.</summary>
        public bool IsSettled(float speedThreshold = 25f)
        {
            foreach (PhysicsBall ball in _balls)
            {
                float vx = ball.VelocityX(StepSeconds / SubSteps);
                float vy = ball.VelocityY(StepSeconds / SubSteps);
                if (vx * vx + vy * vy > speedThreshold * speedThreshold)
                {
                    return false;
                }
            }
            return true;
        }

        public void Step(float dt)
        {
            float h = dt / SubSteps;
            for (int s = 0; s < SubSteps; s++)
            {
                for (int b = 0; b < _balls.Count; b++)
                {
                    PhysicsBall ball = _balls[b];
                    float vx = (ball.X - ball.PreviousX) * 0.999f;
                    float vy = ball.Y - ball.PreviousY;
                    ball.PreviousX = ball.X;
                    ball.PreviousY = ball.Y;
                    float push = 0f;
                    // Only balls resting on the heap: one still falling in from above must land where it was aimed.
                    if (ball.Landed && ball.Y > SpillHeight && Math.Abs(vy) < SpillRestingSpeed * h)
                    {
                        // Outward from the middle; a ball dead centre picks a side by its place in the list.
                        float side = Math.Abs(ball.X) > 0.5f ? Math.Sign(ball.X) : (b % 2 == 0 ? -1f : 1f);
                        push = side * SpillPush * h * h;
                    }
                    ball.X += vx + push;
                    ball.Y += vy + Gravity * h * h;
                }
                for (int i = 0; i < SolverIterations; i++)
                {
                    SolveBallPairs();
                    SolveSegments();
                }
                LimitSpeed(h);
            }
        }

        private void LimitSpeed(float h)
        {
            float maxStep = MaxSpeed * h;
            foreach (PhysicsBall ball in _balls)
            {
                float vx = ball.X - ball.PreviousX;
                float vy = ball.Y - ball.PreviousY;
                float lengthSquared = vx * vx + vy * vy;
                if (lengthSquared > maxStep * maxStep)
                {
                    float scale = maxStep / (float)Math.Sqrt(lengthSquared);
                    ball.PreviousX = ball.X - vx * scale;
                    ball.PreviousY = ball.Y - vy * scale;
                }
            }
        }

        private void SolveBallPairs()
        {
            for (int i = 0; i < _balls.Count; i++)
            {
                PhysicsBall a = _balls[i];
                for (int j = i + 1; j < _balls.Count; j++)
                {
                    PhysicsBall b = _balls[j];
                    float dx = b.X - a.X;
                    float dy = b.Y - a.Y;
                    float minDistance = a.Radius + b.Radius;
                    float distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared >= minDistance * minDistance)
                    {
                        continue;
                    }
                    float distance = (float)Math.Sqrt(distanceSquared);
                    if (distance < 0.0001f)
                    {
                        // Exactly on top of each other: nudge apart sideways.
                        dx = 0.01f * (j % 2 == 0 ? 1 : -1);
                        dy = 0f;
                        distance = 0.01f;
                    }
                    float push = (minDistance - distance) / distance * 0.5f;
                    a.X -= dx * push;
                    a.Y -= dy * push;
                    b.X += dx * push;
                    b.Y += dy * push;
                    // The one on top is now resting on the other.
                    if (b.Y > a.Y)
                    {
                        b.Landed = true;
                    }
                    else
                    {
                        a.Landed = true;
                    }
                }
            }
        }

        private void SolveSegments()
        {
            foreach (PhysicsBall ball in _balls)
            {
                foreach (PhysicsSegment segment in _segments)
                {
                    ClosestPoint(segment, ball.X, ball.Y, out float cx, out float cy);
                    float dx = ball.X - cx;
                    float dy = ball.Y - cy;
                    float distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared >= ball.Radius * ball.Radius)
                    {
                        continue;
                    }
                    float distance = (float)Math.Sqrt(distanceSquared);
                    float nx;
                    float ny;
                    if (distance < 0.0001f)
                    {
                        // Centre right on the line: push out along the segment's normal (left of its direction).
                        float sx = segment.X2 - segment.X1;
                        float sy = segment.Y2 - segment.Y1;
                        float length = (float)Math.Sqrt(sx * sx + sy * sy);
                        nx = -sy / length;
                        ny = sx / length;
                        distance = 0f;
                    }
                    else
                    {
                        nx = dx / distance;
                        ny = dy / distance;
                    }
                    float depth = ball.Radius - distance;
                    ball.X += nx * depth;
                    ball.Y += ny * depth;
                    ball.Landed = true;

                    // Friction: damp the movement along the surface, and roll the ball by what's left.
                    float vx = ball.X - ball.PreviousX;
                    float vy = ball.Y - ball.PreviousY;
                    float along = vx * -ny + vy * nx;
                    float kept = along * Friction;
                    ball.PreviousX += -ny * (along - kept);
                    ball.PreviousY += nx * (along - kept);
                    ball.Angle -= kept / ball.Radius;
                }
            }
        }

        private static void ClosestPoint(PhysicsSegment s, float px, float py, out float cx, out float cy)
        {
            float sx = s.X2 - s.X1;
            float sy = s.Y2 - s.Y1;
            float lengthSquared = sx * sx + sy * sy;
            float t = lengthSquared > 0f ? ((px - s.X1) * sx + (py - s.Y1) * sy) / lengthSquared : 0f;
            t = Math.Max(0f, Math.Min(1f, t));
            cx = s.X1 + sx * t;
            cy = s.Y1 + sy * t;
        }
    }
}
