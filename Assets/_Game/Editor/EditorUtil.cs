using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TownBuilder.EditorTools
{
    /// <summary>Small helpers for building scenes/UI from code and wiring private [SerializeField] references.</summary>
    internal static class EditorUtil
    {
        public static GameObject Go(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        public static T Add<T>(GameObject go) where T : Component => go.AddComponent<T>();

        /// <summary>Assigns object references to serialized fields by name. Throws if a field name is wrong.</summary>
        public static void Wire(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var prop = so.FindProperty(field);
                if (prop == null) throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
                prop.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireArray(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray) throw new InvalidOperationException($"{target.GetType().Name} has no array field '{field}'.");
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ UI

        private static Font _font;
        private static Sprite _rounded;

        private static TMP_FontAsset _fontAsset;

        public static Font UIFont => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>TextMeshPro font used by all UI text (imported by ProjectSetup.EnsureTextMeshPro).</summary>
        public static TMP_FontAsset UIFontAsset
        {
            get
            {
                if (_fontAsset != null) return _fontAsset;
                _fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectSetup.UIFontAssetPath);
                if (_fontAsset == null) throw new InvalidOperationException($"Missing UI font {ProjectSetup.UIFontAssetPath}. Run TownBuilder > Setup.");
                return _fontAsset;
            }
        }

        public const float BoldCharacterSpacing = -4f;

        public static TextAlignmentOptions ToTmp(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        public static FontStyles ToTmp(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Bold: return FontStyles.Bold;
                case FontStyle.Italic: return FontStyles.Italic;
                case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
                default: return FontStyles.Normal;
            }
        }

        /// <summary>A TextMeshPro UI text with the project font, no raycasts, wrapping on.</summary>
        public static TextMeshProUGUI AddText(GameObject go, string text, int fontSize, TextAnchor alignment, Color color,
            FontStyle style = FontStyle.Normal)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = UIFontAsset;
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = ToTmp(alignment);
            t.color = color;
            t.fontStyle = ToTmp(style);
            // TMP's synthetic bold adds letter spacing; pull it back so bold reads like a normal bold weight.
            if ((style & FontStyle.Bold) != 0) t.characterSpacing = BoldCharacterSpacing;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = false;
            return t;
        }
        public static Sprite RoundedSprite => _rounded != null ? _rounded : _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        public static RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent, float padding = 0f)
        {
            var rt = MakeRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
            return rt;
        }

        public static Image AddImage(RectTransform rt, Color color, bool rounded = true, bool raycast = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = RoundedSprite;
                img.type = Image.Type.Sliced;
            }

            img.raycastTarget = raycast;
            return img;
        }

        public static TMP_Text Label(Transform parent, string name, string text, int fontSize, TextAnchor alignment, Color color,
            FontStyle style = FontStyle.Normal)
        {
            var rt = Stretch(name, parent);
            return AddText(rt.gameObject, text, fontSize, alignment, color, style);
        }

        public static Button MakeButton(Transform parent, string name, string label, Vector2 size, Color color, out TMP_Text labelText, int fontSize = 40)
        {
            var rt = MakeRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var img = AddImage(rt, color);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            button.colors = colors;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            labelText = Label(rt, "Label", label, fontSize, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            // Long labels ("Finish now 2 gems") shrink to fit the button instead of touching its edges.
            labelText.enableAutoSizing = true;
            labelText.fontSizeMax = fontSize;
            labelText.fontSizeMin = Mathf.Round(fontSize * 0.6f);
            labelText.margin = new Vector4(10f, 4f, 10f, 4f);
            return button;
        }

        public static Button MakeButton(Transform parent, string name, string label, Vector2 size, Color color, int fontSize = 40) =>
            MakeButton(parent, name, label, size, color, out _, fontSize);

        public static HorizontalLayoutGroup HRow(RectTransform rt, float spacing, TextAnchor alignment, int padding = 0)
        {
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.childAlignment = alignment;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            g.padding = new RectOffset(padding, padding, padding, padding);
            return g;
        }

        public static VerticalLayoutGroup VColumn(RectTransform rt, float spacing, TextAnchor alignment, int padding = 0)
        {
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.childAlignment = alignment;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            g.padding = new RectOffset(padding, padding, padding, padding);
            return g;
        }

        public static void Prefer(Component c, float width, float height)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
        }
    }
}
