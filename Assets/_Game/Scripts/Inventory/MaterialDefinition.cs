using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// A building material (Planks, Bricks, Nails). Not grown or produced: earned from train crates and some tasks,
    /// kept in the barn, spent on land expansion. Never requested by orders or the train.
    /// </summary>
    [CreateAssetMenu(menuName = "TownBuilder/Material Definition", fileName = "Material_New")]
    public sealed class MaterialDefinition : ItemDefinition
    {
#if UNITY_EDITOR
        public void EditorConfigure(string newId, string newName, Sprite newIcon, int sellPrice) =>
            EditorConfigureItem(newId, newName, newIcon, sellPrice);
#endif
    }
}
