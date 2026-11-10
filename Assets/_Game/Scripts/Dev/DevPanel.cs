using System;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// Developer-mode panel: a small "DEV" button (top-right) opens a list of testing shortcuts.
    /// The button doesn't exist in Google Play builds (see <see cref="DevTools.Enabled"/>).
    /// </summary>
    public sealed class DevPanel : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button coinsButton;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private Button unlockAllButton;
        [SerializeField] private Button fillBarnButton;
        [SerializeField] private Button finishTimersButton;
        [SerializeField] private Button unlockLandButton;
        [SerializeField] private Button dailyAgainButton;

        private TownSceneController _scene;
        private GameContent _content;
        private Action<string> _toast;

        public void Initialize(TownSceneController scene, GameContent content, Action<string> toast)
        {
            _scene = scene;
            _content = content;
            _toast = toast;

            openButton.gameObject.SetActive(DevTools.Enabled);
            panelRoot.SetActive(false);
            if (!DevTools.Enabled) return;

            openButton.onClick.AddListener(() => panelRoot.SetActive(!panelRoot.activeSelf));
            closeButton.onClick.AddListener(() => panelRoot.SetActive(false));
            Bind(coinsButton, () => DevTools.AddCurrency(_scene), "+10,000 coins  +100 gems");
            Bind(levelUpButton, () => DevTools.LevelUp(_scene), null);
            Bind(unlockAllButton, () => DevTools.UnlockAll(_scene), $"Level {DevTools.UnlockAllLevel}: everything unlocked");
            Bind(fillBarnButton, () => DevTools.FillBarn(_scene, _content), "Barn maxed and filled");
            Bind(finishTimersButton, () => DevTools.FinishAllTimers(_scene), "All timers finished");
            Bind(unlockLandButton, () => DevTools.UnlockAllLand(_scene), "All land unlocked");
            if (dailyAgainButton != null)
                Bind(dailyAgainButton, () =>
                {
                    _scene.Daily?.DevAllowClaimAgain();
                    UnityEngine.Object.FindAnyObjectByType<DailyRewardDialog>()?.Open();
                    panelRoot.SetActive(false);
                }, null);
        }

        private void Bind(Button button, Action action, string message)
        {
            button.onClick.AddListener(() =>
            {
                action();
                if (message != null) _toast?.Invoke($"[DEV] {message}");
            });
        }
    }
}
