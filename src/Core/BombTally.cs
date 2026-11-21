using System.Collections.Generic;

namespace PeglinBalatroScore.Core
{
    /// <summary>
    /// The thrown bombs' total damage for a turn, for BalatroScoreApi's listeners. Counted whether or not the bomb chest
    /// is showing. Each bomb counts once, however many enemies it hits. Pure C# so it can be unit tested.
    /// </summary>
    public sealed class BombTally
    {
        private readonly HashSet<int> _counted = new HashSet<int>();
        private long _total;

        /// <summary>A thrown bomb hit for this much. Later hits by the same bomb, and amounts of 0 or less, are ignored.</summary>
        public void Add(int bombId, long amount)
        {
            if (amount <= 0 || !_counted.Add(bombId))
            {
                return;
            }
            _total = _total > long.MaxValue - amount ? long.MaxValue : _total + amount;
        }

        /// <summary>The total since the last take, once: true if any bomb did damage, then it starts again from 0.</summary>
        public bool TakeTotal(out long total)
        {
            total = _total;
            bool any = _counted.Count > 0 && _total > 0;
            Clear();
            return any;
        }

        /// <summary>Forget everything counted (e.g. a new battle starting after one that never ended cleanly).</summary>
        public void Clear()
        {
            _total = 0;
            _counted.Clear();
        }
    }
}
