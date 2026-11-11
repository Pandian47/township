using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// Shown above the selection bar while a home is selected: its happiness (bar + %), what it earns, a hint about
    /// decorations, and Collect.
    /// </summary>
    public sealed class HomePanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text happinessText;
        [SerializeField] private Image happinessFill;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Button collectButton;
        [SerializeField] private TMP_Text collectLabel;

        private HappinessController _homes;
        private string _homeId;
        private float _nextRefresh;

        public string HomeId => _homeId;

        public void Initialize(HappinessController homes)
        {
            _homes = homes;
            collectButton.onClick.AddListener(() =>
            {
                if (_homeId != null) _homes.Collect(_homeId);
                Refresh();
            });
            Hide();
        }

        public void ShowFor(string homeId)
        {
            _homeId = homeId;
            panelRoot.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            _homeId = null;
            panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (_homeId == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.5f;
            Refresh();
        }

        private void Refresh()
        {
            if (_homes == null || _homeId == null || !_homes.IsHome(_homeId, out var def)) return;
            int happiness = _homes.Happiness(_homeId);
            int pending = _homes.Pending(_homeId);
            Render(def.DisplayName, happiness, _homes.CoinsPerHour(_homeId), _homes.MaxHours, _homes.Radius, pending);
        }

        /// <summary>Draws the panel (also used by the editor preview).</summary>
        public void Render(string name, int happiness, double coinsPerHour, double maxHours, int radius, int pending)
        {
            titleText.text = name;
            happinessText.text = $"Happiness {happiness}%";
            happinessFill.fillAmount = happiness / 100f;
            happinessFill.color = Color.Lerp(new Color(0.95f, 0.55f, 0.2f), new Color(0.3f, 0.75f, 0.3f), happiness / 100f);
            infoText.text = $"Earns {coinsPerHour:0.#} coins an hour (holds up to {maxHours:0} hours). " +
                            $"Trees, flowers, benches and fountains within {radius} tiles make it happier and earn more.";
            collectButton.interactable = pending > 0;
            collectLabel.text = pending > 0 ? $"Collect\n{pending} coins" : "Nothing yet";
        }
    }
}
