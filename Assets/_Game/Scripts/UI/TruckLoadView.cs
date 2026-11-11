using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>One truck load card: up to two item lines (icon, name, have/need), the reward, and Load / "Loaded".</summary>
    public sealed class TruckLoadView : MonoBehaviour
    {
        private static readonly Color Enough = new Color(0.20f, 0.50f, 0.18f);
        private static readonly Color Short = new Color(0.78f, 0.22f, 0.16f);

        [SerializeField] private Image background;
        [SerializeField] private Image[] lineIcons = new Image[0];
        [SerializeField] private TMP_Text[] lineTexts = new TMP_Text[0];
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button loadButton;
        [SerializeField] private TMP_Text loadedText;

        public void Show(IReadOnlyList<(Sprite icon, string text, bool enough)> lines, string reward, bool loaded, bool canLoad, Action onLoad)
        {
            gameObject.SetActive(true);
            background.color = loaded ? new Color(0.82f, 0.94f, 0.80f) : Color.white;
            for (int i = 0; i < lineTexts.Length; i++)
            {
                bool has = i < lines.Count;
                lineIcons[i].gameObject.SetActive(has);
                lineTexts[i].gameObject.SetActive(has);
                if (!has) continue;
                lineIcons[i].sprite = lines[i].icon;
                lineIcons[i].enabled = lines[i].icon != null;
                lineTexts[i].text = lines[i].text;
                lineTexts[i].color = loaded || lines[i].enough ? Enough : Short;
            }

            rewardText.text = reward;
            loadButton.gameObject.SetActive(!loaded);
            loadButton.interactable = canLoad;
            loadButton.onClick.RemoveAllListeners();
            if (onLoad != null) loadButton.onClick.AddListener(() => onLoad());
            loadedText.gameObject.SetActive(loaded);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
