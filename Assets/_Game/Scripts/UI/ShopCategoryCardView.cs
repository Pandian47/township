using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// One big card on the Build screen: a ribbon with the category name, a picture, a line underneath
    /// ("5 buildings" / "Level 8"), and a badge with how many of its buildings can be built right now.
    /// </summary>
    public sealed class ShopCategoryCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image ribbon;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image picture;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text badgeText;
        [SerializeField] private GameObject lockOverlay;

        public BuildingCategory Category { get; private set; }
        public Button Button => button;
        public int BadgeCount { get; private set; }

        /// <param name="ribbonSprite">Coloured ribbon art; when null the template ribbon is tinted with <paramref name="colour"/>.</param>
        public void Bind(BuildingCategory category, string title, Sprite ribbonSprite, Color colour, Sprite pictureSprite,
            Action<BuildingCategory> onClick)
        {
            Category = category;
            titleText.text = title.ToUpperInvariant();
            if (ribbonSprite != null)
            {
                ribbon.sprite = ribbonSprite;
                ribbon.color = Color.white;
            }
            else
            {
                ribbon.color = colour;
            }

            picture.sprite = pictureSprite;
            picture.enabled = pictureSprite != null;
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(() => onClick(category));
        }

        /// <summary>
        /// <paramref name="readyNow"/>: buildings that can be built right now (unlocked, affordable, under their limit).
        /// <paramref name="lockedUntil"/>: lowest unlock level when nothing in the category is unlocked yet, else 0.
        /// </summary>
        public void Refresh(int total, int readyNow, int lockedUntil)
        {
            BadgeCount = readyNow;
            bool locked = lockedUntil > 0;
            infoText.text = locked ? $"Level {lockedUntil}" : total == 1 ? "1 building" : $"{total} buildings";
            if (lockOverlay != null) lockOverlay.SetActive(locked);
            if (badge != null) badge.SetActive(!locked && readyNow > 0);
            if (badgeText != null) badgeText.text = readyNow.ToString();
        }
    }
}
