using TMPro;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>"Land for sale" panel: what the plot costs (coins + materials, have/need) and a Buy button.</summary>
    public sealed class LandPanel : MonoBehaviour
    {
        private static readonly Color Enough = new Color(0.2f, 0.55f, 0.25f);
        private static readonly Color NotEnough = new Color(0.8f, 0.25f, 0.2f);

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text infoText;
        [Tooltip("Cost chips: the first one is coins, the rest materials.")]
        [SerializeField] private Image[] costIcons = new Image[0];
        [SerializeField] private TMP_Text[] costTexts = new TMP_Text[0];
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyLabel;
        [SerializeField] private Button closeButton;

        private LandController _land;
        private PlotCoord _plot;
        private bool _open;

        public bool IsOpen => _open;

        public void Initialize(LandController land)
        {
            _land = land;
            buyButton.onClick.AddListener(() =>
            {
                if (_land.TryBuy(_plot)) Hide();
                else Refresh();
            });
            closeButton.onClick.AddListener(Hide);
            Hide();
        }

        public void ShowFor(PlotCoord plot)
        {
            _plot = plot;
            _open = true;
            panelRoot.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            _open = false;
            panelRoot.SetActive(false);
        }

        private void Refresh()
        {
            var tier = _land.TierFor(_plot);
            var cells = _land.Land.PlotCells(_plot);
            titleText.text = "Land for sale";
            infoText.text = $"{cells.X} × {cells.Y} tiles to build on";

            for (int i = 0; i < costTexts.Length; i++)
            {
                bool used = tier != null && (i == 0 || i - 1 < tier.materials.Count);
                costIcons[i].transform.parent.gameObject.SetActive(used); // the whole chip
                if (!used) continue;
                if (i == 0)
                {
                    costTexts[i].text = $"{tier.coins:N0} coins";
                    costTexts[i].color = _land.HasCoins(tier.coins) ? Enough : NotEnough;
                    costIcons[i].enabled = false;
                    continue;
                }

                var cost = tier.materials[i - 1];
                int have = _land.Have(cost.material);
                costTexts[i].text = $"{(cost.material != null ? cost.material.DisplayName : "?")}  {have}/{cost.amount}";
                costTexts[i].color = have >= cost.amount ? Enough : NotEnough;
                costIcons[i].sprite = cost.material != null ? cost.material.Icon : null;
                costIcons[i].enabled = costIcons[i].sprite != null;
            }

            bool levelOk = _land.IsLevelReached(_plot);
            buyButton.interactable = levelOk && _land.CanAfford(_plot);
            buyLabel.text = levelOk ? "Buy land" : $"Level {tier?.unlockLevel}";
        }
    }
}
