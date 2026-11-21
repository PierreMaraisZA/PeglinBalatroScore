using System;
using System.Collections.Generic;

namespace PeglinBalatroScore.Core
{
    /// <summary>
    /// Other mods' callbacks, one per plugin GUID, kept in registration order. Registering again with the same GUID
    /// replaces the callback and keeps its place. A callback that throws is logged once (until it is registered again)
    /// and skipped, so a broken mod can't break the HUD. Main thread only, like the rest of the game.
    /// </summary>
    public sealed class GuidRegistry<T> where T : class
    {
        private sealed class Entry
        {
            public string Guid;
            public T Callback;
            public bool LoggedFailure;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly string _what;
        private readonly Action<string> _warn;

        /// <param name="what">What the callbacks are, for the log ("score target provider").</param>
        /// <param name="warn">Where "callback X threw" goes; null to drop it.</param>
        public GuidRegistry(string what, Action<string> warn = null)
        {
            _what = what;
            _warn = warn;
        }

        public int Count => _entries.Count;

        /// <summary>A null or empty GUID is ignored; a null callback removes the GUID's registration.</summary>
        public void Set(string guid, T callback)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }
            if (callback == null)
            {
                RemoveAll(guid);
                return;
            }
            Entry existing = _entries.Find(e => e.Guid == guid);
            if (existing != null)
            {
                existing.Callback = callback;
                existing.LoggedFailure = false;
            }
            else
            {
                _entries.Add(new Entry { Guid = guid, Callback = callback });
            }
        }

        public void RemoveAll(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }
            _entries.RemoveAll(e => e.Guid == guid);
        }

        /// <summary>
        /// Calls <paramref name="call"/> for each callback in registration order until it returns true, and says whether
        /// one did. A callback that throws is skipped. Callbacks may register or remove during the walk; it works on a copy.
        /// </summary>
        public bool Any(Func<T, bool> call)
        {
            foreach (Entry entry in _entries.ToArray())
            {
                bool stop;
                try
                {
                    stop = call(entry.Callback);
                }
                catch (Exception e)
                {
                    if (!entry.LoggedFailure)
                    {
                        entry.LoggedFailure = true;
                        _warn?.Invoke($"The {_what} from '{entry.Guid}' threw {e.GetType().Name}: {e.Message}. It is ignored whenever it fails.");
                    }
                    continue;
                }
                if (stop)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Calls every callback in registration order (one that throws is skipped).</summary>
        public void Each(Action<T> call)
        {
            Any(callback =>
            {
                call(callback);
                return false;
            });
        }
    }
}
