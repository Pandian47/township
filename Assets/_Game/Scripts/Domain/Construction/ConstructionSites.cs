using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    /// <summary>A building that is still being built. Absolute UTC times, so construction continues while the app is closed.</summary>
    public sealed class ConstructionSite
    {
        public ConstructionSite(long startUnixMs, long durationMs)
        {
            StartUnixMs = startUnixMs;
            DurationMs = Math.Max(0, durationMs);
        }

        public long StartUnixMs { get; internal set; }
        public long DurationMs { get; }
        public long EndUnixMs => StartUnixMs + DurationMs;

        public bool IsDone(long nowUnixMs) => nowUnixMs >= EndUnixMs;
        public long RemainingMs(long nowUnixMs) => Math.Max(0, EndUnixMs - nowUnixMs);

        public float Progress(long nowUnixMs)
        {
            if (DurationMs <= 0) return 1f;
            double p = (double)(nowUnixMs - StartUnixMs) / DurationMs;
            return (float)(p < 0 ? 0 : p > 1 ? 1 : p);
        }
    }

    /// <summary>
    /// Buildings under construction, keyed by building instance id. A building without an entry is complete.
    /// Finished sites stay listed until <see cref="TakeFinished"/> is called, so the runtime can grant XP exactly once.
    /// </summary>
    public sealed class ConstructionSites
    {
        private readonly Dictionary<string, ConstructionSite> _sites = new Dictionary<string, ConstructionSite>();

        public event Action<string> Changed;

        public IReadOnlyDictionary<string, ConstructionSite> Sites => _sites;

        public bool IsUnderConstruction(string instanceId) => instanceId != null && _sites.ContainsKey(instanceId);

        public bool TryGet(string instanceId, out ConstructionSite site)
        {
            site = null;
            return instanceId != null && _sites.TryGetValue(instanceId, out site);
        }

        public void Start(string instanceId, long nowUnixMs, long durationMs)
        {
            if (string.IsNullOrEmpty(instanceId)) throw new ArgumentException("Instance id is required.", nameof(instanceId));
            if (durationMs <= 0) return; // instant buildings never become sites
            _sites[instanceId] = new ConstructionSite(nowUnixMs, durationMs);
            Changed?.Invoke(instanceId);
        }

        /// <summary>Finishes a site immediately (rewarded ad, gems).</summary>
        public bool FinishNow(string instanceId, long nowUnixMs)
        {
            if (!TryGet(instanceId, out var site) || site.IsDone(nowUnixMs)) return false;
            site.StartUnixMs = nowUnixMs - site.DurationMs;
            Changed?.Invoke(instanceId);
            return true;
        }

        /// <summary>Removes and returns the ids of all finished sites.</summary>
        public List<string> TakeFinished(long nowUnixMs)
        {
            var done = new List<string>();
            foreach (var pair in _sites)
                if (pair.Value.IsDone(nowUnixMs)) done.Add(pair.Key);
            foreach (var id in done)
            {
                _sites.Remove(id);
                Changed?.Invoke(id);
            }

            return done;
        }

        public bool Remove(string instanceId)
        {
            if (instanceId == null || !_sites.Remove(instanceId)) return false;
            Changed?.Invoke(instanceId);
            return true;
        }

        public void Restore(string instanceId, long endUnixMs, long durationMs)
        {
            if (string.IsNullOrEmpty(instanceId) || durationMs <= 0) return;
            _sites[instanceId] = new ConstructionSite(endUnixMs - durationMs, durationMs);
        }

        /// <summary>Gems to finish now: 1 per started minute remaining, at least 1.</summary>
        public static int GemCostToFinish(long remainingMs) => TimeSkip.GemCost(remainingMs);
    }
}
