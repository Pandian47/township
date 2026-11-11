using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>One Build-shop category tab: a small building picture and a name. Green while selected.</summary>
    public sealed class ShopTabView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;

        public BuildingCategory Category { get; private set; }
        public Button Button => button;

        public void Bind(BuildingCategory category, string text, Sprite picture, Action<BuildingCategory> onClick)
        {
            Category = category;
            label.text = text;
            icon.sprite = picture;
            icon.enabled = picture != null;
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(() => onClick(category));
        }

        public void SetSelected(bool selected, Color activeColor, Color idleColor) =>
            background.color = selected ? activeColor : idleColor;
    }
}
