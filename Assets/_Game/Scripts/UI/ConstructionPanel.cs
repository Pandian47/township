using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>Shown above the selection bar while a building under construction is selected.</summary>
    public sealed class ConstructionPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text statusText;
        [SerializeField] private Image progressFill;
        [SerializeField] private Button adButton;
        [SerializeField] private Text adLabel;
        [SerializeField] private Button gemsButton;
        [SerializeField] private Text gemsLabel;

        private ConstructionController _construction;
        private IAdService _ads;
        private string _instanceId;
        private float _nextRefresh;

        public string InstanceId => _instanceId;

        public void Initialize(ConstructionController construction, IAdService ads)
        {
            _construction = construction;
            _ads = ads;
            adButton.onClick.AddListener(OnAd);
            gemsButton.onClick.AddListener(() =>
            {
                if (_instanceId != null) _construction.FinishWithGems(_instanceId);
                Refresh();
            });
            Hide();
        }

        public void ShowFor(string instanceId, string displayName)
        {
            _instanceId = instanceId;
            titleText.text = $"{displayName}: under construction";
            panelRoot.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            _instanceId = null;
            panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (_instanceId == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.2f;
            Refresh();
        }

        private void Refresh()
        {
            if (_instanceId == null) return;
            if (!_construction.TryGetSite(_instanceId, out var site))
            {
                Hide(); // finished: the HUD shows the normal panels on the next selection refresh
                return;
            }

            long now = _construction.NowUnixMs;
            statusText.text = $"Ready in {FieldPanel.FormatDuration(site.RemainingMs(now))}";
            progressFill.fillAmount = site.Progress(now);

            bool adsAvailable = !(_ads is NoAdService);
            adButton.gameObject.SetActive(adsAvailable);
            adButton.interactable = _ads.IsRewardedReady;
            adLabel.text = _ads.IsRewardedReady ? "Finish now\n(watch ad)" : "Ad loading...";

            int gems = _construction.GemCostToFinish(_instanceId);
            gemsLabel.text = $"Finish now\n{gems} gem{(gems == 1 ? "" : "s")}";
        }

        private void OnAd()
        {
            if (_instanceId == null) return;
            string id = _instanceId;
            adButton.interactable = false;
            _ads.ShowRewarded(earned =>
            {
                if (earned) _construction.FinishNow(id);
                Refresh();
            });
        }
    }
}
