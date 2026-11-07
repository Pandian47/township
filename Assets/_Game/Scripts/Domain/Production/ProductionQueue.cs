using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    /// <summary>One queued product. Absolute UTC times, so production continues while the app is closed.</summary>
    public sealed class ProductionJob
    {
        public ProductionJob(string recipeId, long startUnixMs, long durationMs)
        {
            RecipeId = recipeId;
            StartUnixMs = startUnixMs;
            DurationMs = Math.Max(0, durationMs);
        }

        public string RecipeId { get; }
        public long StartUnixMs { get; internal set; }
        public long DurationMs { get; }
        public long EndUnixMs => StartUnixMs + DurationMs;

        public bool IsDone(long nowUnixMs) => nowUnixMs >= EndUnixMs;

        public float Progress(long nowUnixMs)
        {
            if (DurationMs <= 0) return 1f;
            double p = (double)(nowUnixMs - StartUnixMs) / DurationMs;
            return (float)(p < 0 ? 0 : p > 1 ? 1 : p);
        }

        public long RemainingMs(long nowUnixMs) => Math.Max(0, EndUnixMs - nowUnixMs);
    }

    /// <summary>
    /// A factory's queue: jobs run one after another (a job starts when the previous one ends, or when queued if idle).
    /// Finished jobs wait until collected. Capacity = number of slots, counting finished-but-uncollected jobs.
    /// </summary>
    public sealed class ProductionQueue
    {
        private readonly List<ProductionJob> _jobs = new List<ProductionJob>();

        public ProductionQueue(int slots)
        {
            Slots = Math.Max(1, slots);
        }

        public int Slots { get; }
        public IReadOnlyList<ProductionJob> Jobs => _jobs;
        public bool IsFull => _jobs.Count >= Slots;
        public bool IsEmpty => _jobs.Count == 0;

        public int CountDone(long nowUnixMs)
        {
            int n = 0;
            foreach (var job in _jobs)
            {
                if (!job.IsDone(nowUnixMs)) break; // sequential: nothing after an unfinished job is done
                n++;
            }

            return n;
        }

        /// <summary>The job currently being produced (first unfinished), or null when idle / all finished.</summary>
        public ProductionJob ActiveJob(long nowUnixMs)
        {
            foreach (var job in _jobs)
                if (!job.IsDone(nowUnixMs)) return job;
            return null;
        }

        public bool TryEnqueue(string recipeId, long durationMs, long nowUnixMs)
        {
            if (string.IsNullOrEmpty(recipeId)) throw new ArgumentException("Recipe id is required.", nameof(recipeId));
            if (IsFull) return false;

            long start = _jobs.Count > 0 ? Math.Max(nowUnixMs, _jobs[_jobs.Count - 1].EndUnixMs) : nowUnixMs;
            _jobs.Add(new ProductionJob(recipeId, start, durationMs));
            return true;
        }

        /// <summary>Removes and returns the oldest finished job, or null if none is finished.</summary>
        public ProductionJob TryTakeFinished(long nowUnixMs)
        {
            if (_jobs.Count == 0 || !_jobs[0].IsDone(nowUnixMs)) return null;
            var job = _jobs[0];
            _jobs.RemoveAt(0);
            return job;
        }

        /// <summary>Finishes the active job now and moves the following jobs earlier by the time saved.</summary>
        public bool FinishActiveNow(long nowUnixMs)
        {
            var active = ActiveJob(nowUnixMs);
            if (active == null) return false;

            long saved = active.EndUnixMs - nowUnixMs;
            bool shift = false;
            foreach (var job in _jobs)
            {
                if (job == active)
                {
                    job.StartUnixMs -= saved;
                    shift = true;
                }
                else if (shift)
                {
                    job.StartUnixMs -= saved;
                }
            }

            return true;
        }

        /// <summary>Loads a saved job (kept in saved order; no slot check, never lose player items).</summary>
        public void Restore(string recipeId, long startUnixMs, long durationMs)
        {
            if (string.IsNullOrEmpty(recipeId)) return;
            _jobs.Add(new ProductionJob(recipeId, startUnixMs, durationMs));
        }

        public void Clear() => _jobs.Clear();
    }

    /// <summary>All factory queues in the town, keyed by the factory's building instance id.</summary>
    public sealed class Factories
    {
        private readonly Dictionary<string, ProductionQueue> _queues = new Dictionary<string, ProductionQueue>();

        public event Action<string> QueueChanged;

        public IReadOnlyDictionary<string, ProductionQueue> Queues => _queues;

        public ProductionQueue GetOrCreate(string factoryId, int slots)
        {
            if (!_queues.TryGetValue(factoryId, out var queue))
            {
                queue = new ProductionQueue(slots);
                _queues[factoryId] = queue;
            }

            return queue;
        }

        public bool TryGet(string factoryId, out ProductionQueue queue)
        {
            queue = null;
            return factoryId != null && _queues.TryGetValue(factoryId, out queue);
        }

        public bool Remove(string factoryId)
        {
            if (factoryId == null || !_queues.Remove(factoryId)) return false;
            QueueChanged?.Invoke(factoryId);
            return true;
        }

        public void NotifyChanged(string factoryId) => QueueChanged?.Invoke(factoryId);
    }
}
