using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Anything that can sit in the barn: crops now, bakery/factory products later.
    /// Inventory and saves reference items by <see cref="Id"/> only.
    /// </summary>
    public abstract class ItemDefinition : ScriptableObject
    {
        [Tooltip("Stable id stored in saves and inventory. Never change it after release.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [Tooltip("Shown in barn/shop UI.")]
        [SerializeField] private Sprite icon;
        [Tooltip("Coins received per unit when sold from the barn. 0 = cannot be sold.")]
        [SerializeField, Min(0)] private int sellPrice = 1;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public virtual Sprite Icon => icon;
        public int SellPrice => sellPrice;
        public bool CanBeSold => sellPrice > 0;

#if UNITY_EDITOR
        protected void EditorConfigureItem(string newId, string newName, Sprite newIcon, int newSellPrice)
        {
            id = newId;
            displayName = newName;
            icon = newIcon;
            sellPrice = newSellPrice;
        }

        public void EditorSetSellPrice(int price) => sellPrice = price;
#endif
    }
}
