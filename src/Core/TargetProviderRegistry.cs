using System;

namespace PeglinBalatroScore.Core
{
    /// <summary>
    /// Other mods' target providers, one per plugin GUID. A provider returns the target HP it wants the score to aim
    /// for, or a value of 0 or less for "no opinion". The first positive answer, in registration order, wins. A provider
    /// that throws is logged once and treated as having no opinion, so a broken mod can't break the HUD.
    /// </summary>
    public sealed class TargetProviderRegistry
    {
        private readonly GuidRegistry<Func<long>> _providers;

        /// <param name="warn">Where "provider X threw" goes; null to drop it.</param>
        public TargetProviderRegistry(Action<string> warn = null)
        {
            _providers = new GuidRegistry<Func<long>>("score target provider", warn);
        }

        public int Count => _providers.Count;

        /// <summary>
        /// Registers the provider for this GUID. Registering again replaces the old one and keeps its place in the
        /// order. A null or empty GUID is ignored; a null provider removes the GUID's registration.
        /// </summary>
        public void Set(string guid, Func<long> provider) => _providers.Set(guid, provider);

        public void RemoveAll(string guid) => _providers.RemoveAll(guid);

        /// <summary>The first positive value a provider gives, or 0 when none has an opinion.</summary>
        public long Resolve()
        {
            long result = 0;
            _providers.Any(provider =>
            {
                long value = provider();
                if (value <= 0)
                {
                    return false;
                }
                result = value;
                return true;
            });
            return result;
        }
    }
}
