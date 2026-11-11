using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// The Settings window, shared by the main menu and the town: music and sound volume (device settings in
    /// <see cref="AudioPreferences"/>), the ad consent form when Google requires it, the privacy policy link, the
    /// credits page (with the font licence) and, in the town, a way back to the main menu.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        public const string NoPrivacyUrlMessage = "The privacy policy link will be added before the game is released.";

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Button closeButton;
        [Tooltip("Re-opens the ad consent form. Required by Google in GDPR regions; hidden elsewhere.")]
        [SerializeField] private Button privacyOptionsButton;
        [SerializeField] private Button privacyPolicyButton;
        [SerializeField] private Button creditsButton;
        [Tooltip("Town only: saves and goes back to the main menu. Hidden in the main menu.")]
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text versionText;

        [Header("Credits")]
        [SerializeField] private GameObject creditsRoot;
        [SerializeField] private TMP_Text creditsText;
        [SerializeField] private Button creditsCloseButton;
        [SerializeField] private ScrollRect creditsScroll;
        [Tooltip("Data/Text/Credits.txt. {version} is replaced by the app version.")]
        [SerializeField] private TextAsset creditsSource;

        private IAdService _ads;
        private string _privacyPolicyUrl;
        private Action _onMainMenu;
        private bool _wired;

        public bool IsOpen => panelRoot.activeSelf;
        public bool CreditsOpen => creditsRoot.activeSelf;
        public string CreditsText => creditsText.text;
        public string Message => messageText.text;
        public Button MainMenuButton => mainMenuButton;
        public Button PrivacyPolicyButton => privacyPolicyButton;
        public Button CreditsButton => creditsButton;

        /// <param name="privacyPolicyUrl">Empty until the policy is hosted (AdsConfig > Privacy Policy Url).</param>
        /// <param name="onMainMenu">Null hides the Main menu button (when already in the main menu).</param>
        public void Initialize(IAdService ads, string privacyPolicyUrl, Action onMainMenu)
        {
            _ads = ads;
            _privacyPolicyUrl = privacyPolicyUrl;
            _onMainMenu = onMainMenu;
            if (!_wired)
            {
                _wired = true;
                closeButton.onClick.AddListener(Close);
                privacyOptionsButton.onClick.AddListener(() => _ads?.ShowPrivacyOptions(null));
                privacyPolicyButton.onClick.AddListener(OpenPrivacyPolicy);
                creditsButton.onClick.AddListener(OpenCredits);
                creditsCloseButton.onClick.AddListener(() => creditsRoot.SetActive(false));
                mainMenuButton.onClick.AddListener(() =>
                {
                    Close();
                    _onMainMenu?.Invoke();
                });
                musicSlider.onValueChanged.AddListener(v => AudioPreferences.MusicVolume = v);
                sfxSlider.onValueChanged.AddListener(v => AudioPreferences.SfxVolume = v);
            }

            mainMenuButton.gameObject.SetActive(onMainMenu != null);
            versionText.text = $"Version {Application.version}";
            creditsText.text = creditsSource != null ? creditsSource.text.Replace("{version}", Application.version) : "TownBuilder";
            panelRoot.SetActive(false);
            creditsRoot.SetActive(false);
        }

        public void Open()
        {
            musicSlider.SetValueWithoutNotify(AudioPreferences.MusicVolume);
            sfxSlider.SetValueWithoutNotify(AudioPreferences.SfxVolume);
            privacyOptionsButton.gameObject.SetActive(_ads != null && _ads.PrivacyOptionsRequired);
            messageText.text = string.Empty;
            creditsRoot.SetActive(false);
            panelRoot.SetActive(true);
        }

        public void Close()
        {
            creditsRoot.SetActive(false);
            panelRoot.SetActive(false);
        }

        public void OpenCredits()
        {
            creditsRoot.SetActive(true);
            if (creditsScroll != null) creditsScroll.verticalNormalizedPosition = 1f;
        }

        private void OpenPrivacyPolicy()
        {
            if (string.IsNullOrWhiteSpace(_privacyPolicyUrl))
            {
                messageText.text = NoPrivacyUrlMessage;
                return;
            }

            Application.OpenURL(_privacyPolicyUrl.Trim());
        }
    }
}
