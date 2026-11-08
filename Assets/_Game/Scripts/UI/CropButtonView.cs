using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    public sealed class CropButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text infoText;

        public CropDefinition Crop { get; private set; }

        public void Bind(CropDefinition crop, Action<CropDefinition> onClicked)
        {
            Crop = crop;
            icon.sprite = crop.Icon;
            icon.preserveAspect = true;
            nameText.text = crop.DisplayName;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(Crop));
        }

        public void Refresh(bool unlocked, bool affordable)
        {
            button.interactable = unlocked && affordable;
            string cost = Crop.SeedCoinCost > 0 ? $"{Crop.SeedCoinCost} coins" : "free";
            infoText.text = unlocked
                ? $"{FieldPanel.FormatDuration(Crop.GrowDurationMs)} · {cost}"
                : $"Level {Crop.UnlockLevel}";
            icon.color = unlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.8f);
        }
    }
}
