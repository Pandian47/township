using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Tooltip("Used only when the scene is played directly without going through Boot.")]
        [SerializeField] private GameContent fallbackContent;

        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private ConfirmDialog confirmDialog;

        [SerializeField] private SettingsPanel settingsPanel;

        private GameContext _context;

        private void Start()
        {
            var root = GameRoot.GetOrCreate(fallbackContent);
            if (root == null) return;
            _context = root.Context;

            continueButton.onClick.AddListener(OnContinue);
            newGameButton.onClick.AddListener(OnNewGame);
            var ads = _context.Content.AdsConfig;
            settingsPanel.Initialize(_context.Ads, ads != null ? ads.PrivacyPolicyUrl : "", onMainMenu: null);
            settingsButton.onClick.AddListener(settingsPanel.Open);

            continueButton.interactable = _context.SaveStore.HasSave;
            statusText.text = string.Empty;
        }

        private void OnContinue()
        {
            var result = _context.ContinueGame();
            foreach (var w in result.Warnings) Debug.LogWarning($"[Save] {w}");

            switch (result.Status)
            {
                case LoadStatus.Loaded:
                case LoadStatus.LoadedFromBackup:
                    SceneManager.LoadScene(SceneNames.Town);
                    break;
                case LoadStatus.NoSave:
                    statusText.text = "No saved town found.";
                    continueButton.interactable = false;
                    break;
                case LoadStatus.FromNewerVersion:
                    statusText.text = "This save was made by a newer version of the game. Please update.";
                    break;
                default:
                    statusText.text = "The saved town could not be read. Start a new game to continue.";
                    break;
            }
        }

        private void OnNewGame()
        {
            if (_context.SaveStore.HasSave)
                confirmDialog.Show("Start a new town?\nYour current town will be replaced.", "New town", StartNewGame);
            else
                StartNewGame();
        }

        private void StartNewGame()
        {
            _context.StartNewGame();
            SceneManager.LoadScene(SceneNames.Town);
        }
    }
}
