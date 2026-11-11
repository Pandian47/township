using TMPro;
using TownBuilder.Domain;
using UnityEngine;
using Terrain = TownBuilder.Domain.Terrain;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// The paint bar shown while painting the landscape: one swatch per ground type (the selected one is framed),
    /// a hint, Cancel (undo all) and Done (keep).
    /// </summary>
    public sealed class LandscapePanel : MonoBehaviour
    {
        private static readonly Color Selected = new Color(1f, 0.82f, 0.25f);
        private static readonly Color Idle = new Color(1f, 1f, 1f, 0.9f);

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button[] swatches = new Button[0];
        [SerializeField] private Image[] swatchFrames = new Image[0];
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button doneButton;
        [Tooltip("HUD parts hidden while painting (bottom bar, tasks...).")]
        [SerializeField] private GameObject[] hideWhilePainting = new GameObject[0];

        private LandscapeController _landscape;

        public bool IsOpen => panelRoot.activeSelf;

        public void Initialize(LandscapeController landscape)
        {
            _landscape = landscape;
            for (int i = 0; i < swatches.Length; i++)
            {
                var terrain = (Terrain)i;
                swatches[i].onClick.AddListener(() =>
                {
                    _landscape.Select(terrain);
                    Refresh();
                });
            }

            cancelButton.onClick.AddListener(() => _landscape.Cancel());
            doneButton.onClick.AddListener(() => _landscape.Done());
            _landscape.Started += Show;
            _landscape.Ended += Hide;
            panelRoot.SetActive(false);
        }

        private void Show()
        {
            panelRoot.SetActive(true);
            foreach (var go in hideWhilePainting)
                if (go != null) go.SetActive(false);
            Refresh();
        }

        private void Hide()
        {
            panelRoot.SetActive(false);
            foreach (var go in hideWhilePainting)
                if (go != null) go.SetActive(true);
        }

        private void Refresh()
        {
            for (int i = 0; i < swatchFrames.Length; i++)
                swatchFrames[i].color = (int)_landscape.Selected == i ? Selected : Idle;
            hintText.text = $"Painting {_landscape.Selected}: drag your finger over your land, then tap Done.";
        }

        private void OnDestroy()
        {
            if (_landscape == null) return;
            _landscape.Started -= Show;
            _landscape.Ended -= Hide;
        }
    }
}
