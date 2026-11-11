using System.Collections.Generic;
using TMPro;
using TownBuilder.Domain;
using UnityEngine;
using Terrain = TownBuilder.Domain.Terrain;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// The bar shown while using a landscape tool (v0.36.0): the tool's styles (the selected one is framed), Eraser, Undo,
    /// a hint, Flip corner (roads), the red button (remove the pending road / cancel everything) and the green button
    /// (lay the pending road / done).
    /// </summary>
    public sealed class LandscapePanel : MonoBehaviour
    {
        private static readonly Color Selected = new Color(1f, 0.82f, 0.25f);
        private static readonly Color Idle = new Color(1f, 1f, 1f, 0.9f);

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text hintText;
        [Tooltip("Style slots, left to right; unused ones are hidden.")]
        [SerializeField] private Button[] slots = new Button[0];
        [SerializeField] private Image[] slotFrames = new Image[0];
        [SerializeField] private Image[] slotPictures = new Image[0];
        [SerializeField] private TMP_Text[] slotNames = new TMP_Text[0];
        [SerializeField] private Button eraserButton;
        [SerializeField] private Image eraserFrame;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button flipButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text cancelLabel;
        [SerializeField] private Button doneButton;
        [SerializeField] private TMP_Text doneLabel;
        [Tooltip("Swatch pictures in Terrain order.")]
        [SerializeField] private Sprite[] terrainSprites = new Sprite[0];
        [Tooltip("HUD parts hidden while painting (bottom bar, tasks...).")]
        [SerializeField] private GameObject[] hideWhilePainting = new GameObject[0];

        private LandscapeController _landscape;

        public bool IsOpen => panelRoot.activeSelf;

        public void Initialize(LandscapeController landscape)
        {
            _landscape = landscape;
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].onClick.AddListener(() =>
                {
                    var palette = LandscapePalette.For(_landscape.Tool);
                    if (index < palette.Count) _landscape.Select(palette[index]);
                });
            }

            eraserButton.onClick.AddListener(() => _landscape.SelectEraser());
            undoButton.onClick.AddListener(() => _landscape.Undo());
            flipButton.onClick.AddListener(() => _landscape.FlipCorner());
            cancelButton.onClick.AddListener(() => _landscape.Back());
            doneButton.onClick.AddListener(() => _landscape.Confirm());
            _landscape.Started += Show;
            _landscape.Ended += Hide;
            _landscape.StateChanged += Refresh;
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
            if (_landscape == null || !panelRoot.activeSelf) return;
            Render(_landscape.Tool, _landscape.Selected, _landscape.Erasing, _landscape.HasRoute, _landscape.RouteValid, _landscape.CanUndo);
        }

        /// <summary>Draws the bar for a state (also used by the editor preview without a controller).</summary>
        public void Render(LandscapeTool tool, Terrain selected, bool erasing, bool hasRoute, bool routeValid, bool canUndo)
        {
            IReadOnlyList<Terrain> palette = LandscapePalette.For(tool);
            titleText.text = LandscapePalette.Title(tool);
            for (int i = 0; i < slots.Length; i++)
            {
                bool used = i < palette.Count;
                slots[i].gameObject.SetActive(used);
                if (!used) continue;
                var t = palette[i];
                slotPictures[i].sprite = (int)t < terrainSprites.Length ? terrainSprites[(int)t] : null;
                slotNames[i].text = LandscapePalette.Name(t);
                slotFrames[i].color = !erasing && t == selected ? Selected : Idle;
            }

            eraserFrame.color = erasing ? Selected : Idle;
            undoButton.interactable = canUndo;
            bool roads = tool == LandscapeTool.Roads && !erasing;
            flipButton.gameObject.SetActive(roads && hasRoute);
            doneLabel.text = roads && hasRoute ? "Build" : "Done";
            doneButton.interactable = !(roads && hasRoute && !routeValid);
            cancelLabel.text = "X";

            string name = LandscapePalette.Name(selected);
            if (erasing) hintText.text = "Eraser: drag over the ground to turn it back into grass.";
            else if (!roads) hintText.text = $"{name}: drag your finger over your land, then tap Done.";
            else if (!hasRoute) hintText.text = $"{name}: tap where the road starts, then drag the flag to where it ends.";
            else if (!routeValid) hintText.text = "The road can't cross buildings or land you don't own. Move the flag.";
            else hintText.text = "Drag the flag to change the road, then tap Build. X removes this road.";
        }

        private void OnDestroy()
        {
            if (_landscape == null) return;
            _landscape.Started -= Show;
            _landscape.Ended -= Hide;
            _landscape.StateChanged -= Refresh;
        }
    }
}
