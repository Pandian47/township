using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Always-visible task list under the top bar (left). Refreshes when a task changes; Claim pays the reward.
    /// Hidden while there are no tasks.
    /// </summary>
    public sealed class TasksPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TaskRowView[] rows = new TaskRowView[0];

        private TaskService _tasks;

        public void Initialize(TaskService tasks)
        {
            Unbind();
            _tasks = tasks;
            for (int i = 0; i < rows.Length; i++)
            {
                int slot = i;
                rows[i].ClaimButton.onClick.RemoveAllListeners();
                rows[i].ClaimButton.onClick.AddListener(() => _tasks.Claim(slot));
            }

            _tasks.Board.Changed += OnChanged;
            Refresh();
        }

        private void OnChanged(int slot) => Refresh();

        private void Refresh()
        {
            if (_tasks == null) return;
            int shown = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                var task = _tasks.Board.Get(i);
                var def = _tasks.DefinitionAt(i);
                if (task == null || def == null)
                {
                    rows[i].Hide();
                    continue;
                }

                rows[i].Show(def.Describe(), task.Progress, task.Spec.Amount, def.RewardText(), task.IsComplete);
                shown++;
            }

            if (panelRoot != null) panelRoot.SetActive(shown > 0);
        }

        private void Unbind()
        {
            if (_tasks != null) _tasks.Board.Changed -= OnChanged;
        }

        private void OnDestroy() => Unbind();
    }
}
