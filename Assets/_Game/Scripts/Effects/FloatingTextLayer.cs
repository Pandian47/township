using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    public readonly struct FloatingLine
    {
        public readonly Sprite Icon;
        public readonly string Text;
        public readonly Color Color;

        public FloatingLine(string text, Color color, Sprite icon = null)
        {
            Text = text;
            Color = color;
            Icon = icon;
        }
    }

    /// <summary>
    /// "+2 Wheat / +1 XP" labels that pop up above a point in the world, rise and fade out.
    /// Lives on a full-screen RectTransform at the back of the HUD canvas (panels draw over it) and follows the
    /// world point every frame, so the label stays on its building while the camera pans or zooms.
    /// </summary>
    public sealed class FloatingTextLayer : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Font font;
        [SerializeField] private int fontSize = 46;
        [SerializeField] private float lineHeight = 58f;
        [SerializeField] private float iconSize = 54f;
        [SerializeField] private float duration = 1.5f;
        [Tooltip("How far a label rises over its lifetime (canvas units, 1080p reference).")]
        [SerializeField] private float rise = 120f;
        [SerializeField] private Color outlineColor = new Color(0.12f, 0.1f, 0.08f, 0.9f);

        private sealed class Popup
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public readonly List<(RectTransform row, Image icon, Text text)> Rows = new List<(RectTransform, Image, Text)>();
            public Vector3 World;
            public float Age;
            public int LineCount;
        }

        private readonly List<Popup> _active = new List<Popup>();
        private readonly Stack<Popup> _pool = new Stack<Popup>();
        private RectTransform _rect;
        private Canvas _canvas;

        public int ActiveCount => _active.Count;

        /// <summary>Texts of the labels currently on screen (for tests and debugging).</summary>
        public IEnumerable<string> ActiveTexts()
        {
            foreach (var p in _active)
                for (int i = 0; i < p.LineCount; i++)
                    yield return p.Rows[i].text.text;
        }

        public void Spawn(Vector3 worldPosition, IReadOnlyList<FloatingLine> lines)
        {
            if (lines == null || lines.Count == 0) return;
            EnsureSetup();

            var popup = _pool.Count > 0 ? _pool.Pop() : CreatePopup();
            popup.Root.gameObject.SetActive(true);
            popup.Root.SetAsLastSibling();
            popup.World = worldPosition;
            popup.Age = 0f;
            popup.LineCount = lines.Count;

            while (popup.Rows.Count < lines.Count) popup.Rows.Add(CreateRow(popup.Root));
            for (int i = 0; i < popup.Rows.Count; i++)
            {
                var (row, icon, text) = popup.Rows[i];
                bool used = i < lines.Count;
                row.gameObject.SetActive(used);
                if (!used) continue;

                var line = lines[i];
                text.text = line.Text;
                text.color = line.Color;
                icon.sprite = line.Icon;
                icon.enabled = line.Icon != null;

                // Centre icon + text as one group: [icon] [text]
                float textWidth = text.preferredWidth;
                float iconWidth = line.Icon != null ? iconSize + 8f : 0f;
                float total = textWidth + iconWidth;
                ((RectTransform)icon.transform).anchoredPosition = new Vector2(-total * 0.5f + iconSize * 0.5f, 0f);
                var textRect = (RectTransform)text.transform;
                textRect.sizeDelta = new Vector2(textWidth + 4f, lineHeight);
                textRect.anchoredPosition = new Vector2(-total * 0.5f + iconWidth + textWidth * 0.5f, 0f);
                row.anchoredPosition = new Vector2(0f, (lines.Count - 1 - i) * lineHeight);
            }

            _active.Add(popup);
            Layout(popup);
        }

        /// <summary>Re-projects every label (the preview renderer calls this after resizing the camera).</summary>
        public void RefreshPositions()
        {
            foreach (var p in _active) Layout(p);
        }

        /// <summary>Moves every label forward in time (previews and tests; the game uses Update).</summary>
        public void Advance(float seconds)
        {
            foreach (var p in _active)
            {
                p.Age += seconds;
                Layout(p);
            }
        }

        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Release(i);
        }

        private void Update()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                p.Age += Time.unscaledDeltaTime;
                if (p.Age >= duration) { Release(i); continue; }
                Layout(p);
            }
        }

        private void Layout(Popup p)
        {
            var cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return;

            float k = Mathf.Clamp01(p.Age / duration);
            Vector2 screen = cam.WorldToScreenPoint(p.World);
            var uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, uiCamera, out var local);

            // Ease-out rise, quick pop-in, fade during the last 40%.
            float eased = 1f - (1f - k) * (1f - k);
            p.Root.anchoredPosition = local + new Vector2(0f, rise * eased);
            float popIn = p.Age < 0.18f ? Mathf.Lerp(0.6f, 1.12f, p.Age / 0.18f) : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((p.Age - 0.18f) / 0.12f));
            p.Root.localScale = Vector3.one * popIn;
            p.Group.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
        }

        private void Release(int index)
        {
            var p = _active[index];
            _active.RemoveAt(index);
            p.Root.gameObject.SetActive(false);
            _pool.Push(p);
        }

        private void EnsureSetup()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (_canvas == null)
            {
                var c = GetComponentInParent<Canvas>();
                _canvas = c != null ? c.rootCanvas : null;
            }

            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private Popup CreatePopup()
        {
            var go = new GameObject("FloatingText", typeof(RectTransform));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(_rect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(10f, 10f);
            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return new Popup { Root = rt, Group = group };
        }

        private (RectTransform, Image, Text) CreateRow(RectTransform parent)
        {
            var row = new GameObject("Line", typeof(RectTransform));
            row.layer = gameObject.layer;
            var rowRect = (RectTransform)row.transform;
            rowRect.SetParent(parent, false);
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            rowRect.sizeDelta = new Vector2(10f, lineHeight);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.layer = gameObject.layer;
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(rowRect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.layer = gameObject.layer;
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rowRect, false);
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            var text = textGo.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            var shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -4f);

            return (rowRect, icon, text);
        }
    }
}
