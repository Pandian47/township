using System;
using System.Collections.Generic;
using UnityEngine;

namespace TownBuilder
{
    [Serializable]
    public struct Ingredient
    {
        public ItemDefinition item;
        [Min(1)] public int amount;
    }

    /// <summary>What a factory can make: ingredients in, product out, after a duration.</summary>
    [CreateAssetMenu(menuName = "TownBuilder/Recipe Definition", fileName = "Recipe_New")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        [Tooltip("Stable id stored in saves. Never change it after release.")]
        [SerializeField] private string id;
        [SerializeField] private ProductDefinition output;
        [SerializeField, Min(1)] private int outputAmount = 1;
        [SerializeField] private List<Ingredient> ingredients = new List<Ingredient>();
        [SerializeField, Min(1)] private int durationSeconds = 180;
        [Tooltip("XP granted when the product is collected.")]
        [SerializeField, Min(0)] private int xp = 2;
        [SerializeField, Min(1)] private int unlockLevel = 1;

        public string Id => id;
        public ProductDefinition Output => output;
        public int OutputAmount => Mathf.Max(1, outputAmount);
        public IReadOnlyList<Ingredient> Ingredients => ingredients;
        public int DurationSeconds => Mathf.Max(1, durationSeconds);
        public long DurationMs => DurationSeconds * 1000L;
        public int Xp => xp;
        public int UnlockLevel => unlockLevel;
        public string DisplayName => output != null ? output.DisplayName : id;

        /// <summary>"2 Wheat + 1 Corn"</summary>
        public string IngredientsText()
        {
            var parts = new List<string>();
            foreach (var i in ingredients)
                if (i.item != null) parts.Add($"{i.amount} {i.item.DisplayName}");
            return parts.Count == 0 ? "free" : string.Join(" + ", parts);
        }

#if UNITY_EDITOR
        public void EditorConfigure(string newId, ProductDefinition newOutput, int amount, List<Ingredient> newIngredients,
            int seconds, int newXp, int unlock)
        {
            id = newId;
            output = newOutput;
            outputAmount = amount;
            ingredients = newIngredients;
            durationSeconds = seconds;
            xp = newXp;
            unlockLevel = unlock;
        }
#endif
    }
}
