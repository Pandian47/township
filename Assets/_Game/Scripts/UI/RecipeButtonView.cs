using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    public sealed class RecipeButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text ingredientsText;

        private static readonly Color Ok = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color Missing = new Color(0.85f, 0.2f, 0.2f);

        public RecipeDefinition Recipe { get; private set; }

        public void Bind(RecipeDefinition recipe, Action<RecipeDefinition> onClicked)
        {
            Recipe = recipe;
            icon.sprite = recipe.Output != null ? recipe.Output.Icon : null;
            icon.preserveAspect = true;
            nameText.text = recipe.DisplayName;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(Recipe));
        }

        public void Refresh(bool unlocked, bool hasIngredients, bool queueFull)
        {
            button.interactable = unlocked && hasIngredients && !queueFull;
            ingredientsText.text = unlocked
                ? $"{Recipe.IngredientsText()}\n{FieldPanel.FormatDuration(Recipe.DurationMs)}"
                : $"Level {Recipe.UnlockLevel}";
            ingredientsText.color = unlocked && !hasIngredients ? Missing : Ok;
            icon.color = unlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.8f);
        }
    }
}
