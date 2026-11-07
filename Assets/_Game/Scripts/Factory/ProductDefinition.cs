using UnityEngine;

namespace TownBuilder
{
    /// <summary>An item made in a factory (Bread, Corn bread...). Id, name, icon and sell price come from ItemDefinition.</summary>
    [CreateAssetMenu(menuName = "TownBuilder/Product Definition", fileName = "Product_New")]
    public sealed class ProductDefinition : ItemDefinition
    {
#if UNITY_EDITOR
        public void EditorConfigure(string newId, string newName, Sprite newIcon, int sellPrice) =>
            EditorConfigureItem(newId, newName, newIcon, sellPrice);
#endif
    }
}
