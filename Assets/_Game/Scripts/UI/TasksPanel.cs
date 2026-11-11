using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// Town tasks (v0.35.2: closed by default). A round Tasks button on the left (red badge = tasks ready to claim)
    /// opens or closes the list under the top bar. Refreshes when a task changes; Claim pays the reward. Tapping any
    /// other HUD button closes the list (see TownHudController.InitializeTasks), so it never stays open behind a window.
    /// The button is hidden while there are no tasks.
    /// </summary>
    public sealed class TasksPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TaskRowView[] rows = new TaskRowView[0];
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text badgeText;

        private TaskService _tasks;
        private bool _wired;

        public bool IsOpen => panelRoot.activeSelf;
        public Button OpenButton => openButton;
        public int ReadyCount { get; private set; }

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

            if (!_wired && openButton != null)
            {
                _wired = true;
                openButton.onClick.AddListener(Toggle);
            }

            _tasks.Board.Changed += OnChanged;
            panelRoot.SetActive(false);
            Refresh();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (openButton != null && !openButton.gameObject.activeSelf) return; // no tasks
            panelRoot.SetActive(true);
            Refresh();
        }

        public void Close() => panelRoot.SetActive(false);

        private void OnChanged(int slot) => Refresh();

        private void Refresh()
        {
            if (_tasks == null) return;
            int shown = 0, ready = 0;
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
                if (task.IsComplete) ready++;
            }

            ReadyCount = ready;
            if (badge != null) badge.SetActive(ready > 0);
            if (badgeText != null) badgeText.text = ready.ToString();
            if (openButton != null) openButton.gameObject.SetActive(shown > 0);
            if (shown == 0) panelRoot.SetActive(false);
        }

        private void Unbind()
        {
            if (_tasks != null) _tasks.Board.Changed -= OnChanged;
        }

        private void OnDestroy() => Unbind();
    }
}
