using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>One train crate: item icon and name, have/need, reward, and a Load button (or "Loaded").</summary>
    public sealed class TrainCrateView : MonoBehaviour
    {
        private static readonly Color Enough = new Color(0.2f, 0.55f, 0.25f);
        private static readonly Color NotEnough = new Color(0.8f, 0.25f, 0.2f);
        private static readonly Color LoadedBackground = new Color(0.78f, 0.93f, 0.76f);

        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text haveText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button loadButton;

        private Action _onLoad;

        private void Awake()
        {
            if (loadButton != null) loadButton.onClick.AddListener(() => _onLoad?.Invoke());
        }

        public void Show(Sprite sprite, string itemName, int amount, int have, long coins, long xp, bool loaded, Action onLoad)
        {
            gameObject.SetActive(true);
            _onLoad = onLoad;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            nameText.text = $"{amount} {itemName}";
            rewardText.text = $"+{coins:N0} coins  +{xp} XP";
            background.color = loaded ? LoadedBackground : Color.white;
            loadButton.gameObject.SetActive(!loaded);
            if (loaded)
            {
                haveText.text = "Loaded";
                haveText.color = Enough;
            }
            else
            {
                haveText.text = $"Have {have}/{amount}";
                haveText.color = have >= amount ? Enough : NotEnough;
                loadButton.interactable = have >= amount;
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
