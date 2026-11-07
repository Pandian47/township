using System;
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

        public static Font UIFont => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        public static Text Label(Transform parent, string name, string text, int fontSize, TextAnchor alignment, Color color,
            FontStyle style = FontStyle.Normal)
        {
            var rt = Stretch(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UIFont;
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        public static Button MakeButton(Transform parent, string name, string label, Vector2 size, Color color, out Text labelText, int fontSize = 40)
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
