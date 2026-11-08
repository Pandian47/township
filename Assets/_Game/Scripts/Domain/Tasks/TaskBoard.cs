using System;
using System.Collections.Generic;

namespace TownBuilder.Domain
{
    /// <summary>What a task counts.</summary>
    public enum TaskKind
    {
        /// <summary>Harvest N of a crop (target = crop id, or any crop).</summary>
        Harvest,
        /// <summary>Collect N of a product from factories / animals (target = product id, or any).</summary>
        Collect,
        /// <summary>Deliver N orders (no target).</summary>
        Deliver,
        /// <summary>Place N buildings of a type (target = building id).</summary>
        Build,
    }

    /// <summary>The rules of one task, independent of Unity (built from a TaskDefinition asset).</summary>
    public sealed class TaskSpec
    {
        public TaskSpec(string id, TaskKind kind, string targetId, int amount, int unlockLevel = 1, bool repeatable = false)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Task id is required.", nameof(id));
            Id = id;
            Kind = kind;
            TargetId = string.IsNullOrEmpty(targetId) ? null : targetId;
            Amount = Math.Max(1, amount);
            UnlockLevel = Math.Max(1, unlockLevel);
            Repeatable = repeatable;
        }

        public string Id { get; }
        public TaskKind Kind { get; }
        /// <summary>Crop / product / building id, or null for "any".</summary>
        public string TargetId { get; }
        public int Amount { get; }
        public int UnlockLevel { get; }
        /// <summary>Repeatable tasks can come back after being claimed; one-off (guide) tasks appear only once.</summary>
        public bool Repeatable { get; }

        public bool Matches(TaskKind kind, string targetId) => kind == Kind && (TargetId == null || TargetId == targetId);
    }

    public sealed class ActiveTask
    {
        public ActiveTask(TaskSpec spec, int progress = 0)
        {
            Spec = spec;
            Progress = Math.Max(0, Math.Min(progress, spec.Amount));
        }

        public TaskSpec Spec { get; }
        public int Progress { get; internal set; }
        public bool IsComplete => Progress >= Spec.Amount;
    }

    /// <summary>
    /// The player's active tasks (a few slots). Game events advance matching tasks; completed tasks are claimed for their
    /// reward and the slot is refilled from the catalogue: one-off guide tasks first (in catalogue order), then a random
    /// repeatable task the player has unlocked.
    /// </summary>
    public sealed class TaskBoard
    {
        private readonly ActiveTask[] _slots;
        private readonly HashSet<string> _completed = new HashSet<string>();

        public TaskBoard(int slots)
        {
            _slots = new ActiveTask[Math.Max(1, slots)];
        }

        /// <summary>A slot changed (progress, claimed, refilled).</summary>
        public event Action<int> Changed;
        /// <summary>A task just reached its goal (ready to claim).</summary>
        public event Action<int> TaskCompleted;

        public int SlotCount => _slots.Length;
        public IReadOnlyList<ActiveTask> Slots => _slots;
        /// <summary>Ids of every task claimed at least once (one-off tasks in here never come back).</summary>
        public IReadOnlyCollection<string> Completed => _completed;

        public ActiveTask Get(int slot) => slot >= 0 && slot < _slots.Length ? _slots[slot] : null;

        /// <summary>Adds progress to every active task matching the event. Returns how many tasks advanced.</summary>
        public int Record(TaskKind kind, string targetId, int amount = 1)
        {
            if (amount <= 0) return 0;
            int advanced = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                var task = _slots[i];
                if (task == null || task.IsComplete || !task.Spec.Matches(kind, targetId)) continue;
                task.Progress = Math.Min(task.Spec.Amount, task.Progress + amount);
                advanced++;
                Changed?.Invoke(i);
                if (task.IsComplete) TaskCompleted?.Invoke(i);
            }

            return advanced;
        }

        /// <summary>Removes a completed task from its slot. The caller pays the reward and refills the board.</summary>
        public bool TryClaim(int slot, out TaskSpec spec)
        {
            spec = null;
            var task = Get(slot);
            if (task == null || !task.IsComplete) return false;
            spec = task.Spec;
            _completed.Add(spec.Id);
            _slots[slot] = null;
            Changed?.Invoke(slot);
            return true;
        }

        /// <summary>
        /// Fills empty slots from <paramref name="catalog"/>. <paramref name="alreadyDone"/> lets the game skip one-off
        /// tasks the player has effectively done already (e.g. "Build a Barn" when they own one); those count as completed.
        /// </summary>
        public int Fill(IReadOnlyList<TaskSpec> catalog, int level, Random random, Func<TaskSpec, bool> alreadyDone = null)
        {
            int filled = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null) continue;
                var next = Pick(catalog, level, random, alreadyDone);
                if (next == null) break;
                _slots[i] = new ActiveTask(next);
                filled++;
                Changed?.Invoke(i);
            }

            return filled;
        }

        public void Restore(int slot, TaskSpec spec, int progress)
        {
            if (spec == null || slot < 0 || slot >= _slots.Length) return;
            _slots[slot] = new ActiveTask(spec, progress);
        }

        public void RestoreCompleted(IEnumerable<string> ids)
        {
            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id)) _completed.Add(id);
        }

        private TaskSpec Pick(IReadOnlyList<TaskSpec> catalog, int level, Random random, Func<TaskSpec, bool> alreadyDone)
        {
            // 1) First one-off task in catalogue order.
            foreach (var spec in catalog)
            {
                if (spec.Repeatable || spec.UnlockLevel > level || IsActive(spec.Id) || _completed.Contains(spec.Id)) continue;
                if (alreadyDone != null && alreadyDone(spec))
                {
                    _completed.Add(spec.Id);
                    continue;
                }

                return spec;
            }

            // 2) Otherwise a random unlocked repeatable task that isn't on the board yet.
            var pool = new List<TaskSpec>();
            foreach (var spec in catalog)
                if (spec.Repeatable && spec.UnlockLevel <= level && !IsActive(spec.Id)) pool.Add(spec);
            return pool.Count > 0 ? pool[random.Next(pool.Count)] : null;
        }

        private bool IsActive(string id)
        {
            foreach (var task in _slots)
                if (task != null && task.Spec.Id == id) return true;
            return false;
        }
    }
}
