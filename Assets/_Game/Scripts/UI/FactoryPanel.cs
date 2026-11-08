using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    /// <summary>
    /// Shown above the selection bar while a factory (e.g. Bakery) is selected:
    /// recipe cards on the left (tap to queue), the production queue on the right, Collect / Finish now buttons.
    /// </summary>
    public sealed class FactoryPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text queueTitleText;

        [Header("Recipes")]
        [SerializeField] private Transform recipesParent;
        [SerializeField] private RecipeButtonView recipeTemplate;

        [Header("Queue")]
        [SerializeField] private QueueSlotView[] slots = new QueueSlotView[0];
        [SerializeField] private Button collectButton;
        [SerializeField] private TMP_Text collectLabel;
        [SerializeField] private Button finishNowButton;
        [SerializeField] private TMP_Text finishNowLabel;
        [Tooltip("'+1 slot' card after the queue slots: buys an extra queue slot for coins.")]
        [SerializeField] private Button addSlotButton;
        [SerializeField] private TMP_Text addSlotLabel;

        private readonly List<RecipeButtonView> _recipeButtons = new List<RecipeButtonView>();
        private ProductionController _production;
        private GameContent _content;
        private IAdService _ads;
        private ConfirmDialog _confirm;
        private string _factoryId;
        private float _nextRefresh;

        public string FactoryId => _factoryId;

        public void Initialize(ProductionController production, GameContent content, IAdService ads, ConfirmDialog confirm = null)
        {
            _production = production;
            _content = content;
            _ads = ads;
            _confirm = confirm;
            if (addSlotButton != null) addSlotButton.onClick.AddListener(OnAddSlot);
            recipeTemplate.gameObject.SetActive(false);
            collectButton.onClick.AddListener(() =>
            {
                if (_factoryId != null) _production.Collect(_factoryId);
                Refresh();
            });
            finishNowButton.onClick.AddListener(OnFinishNow);
            Hide();
        }

        public void ShowFor(string factoryId)
        {
            if (!_production.IsFactory(factoryId, out var def)) { Hide(); return; }
            _factoryId = factoryId;
            titleText.text = def.DisplayName;

            // Rebuild recipe cards for this factory type.
            foreach (var b in _recipeButtons) Destroy(b.gameObject);
            _recipeButtons.Clear();
            foreach (var recipe in def.Recipes)
            {
                if (recipe == null) continue;
                var button = Instantiate(recipeTemplate, recipesParent);
                button.gameObject.SetActive(true);
                button.Bind(recipe, OnRecipeClicked);
                _recipeButtons.Add(button);
            }

            panelRoot.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            _factoryId = null;
            panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (_factoryId == null || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.2f;
            Refresh();
        }

        private void Refresh()
        {
            if (_factoryId == null) return;
            var queue = _production.GetQueue(_factoryId);
            if (queue == null) { Hide(); return; }
            long now = _production.NowUnixMs;

            foreach (var b in _recipeButtons)
                b.Refresh(_production.IsUnlocked(b.Recipe), _production.HasIngredients(b.Recipe), queue.IsFull);

            queueTitleText.text = $"Queue {queue.Jobs.Count}/{queue.Slots}";
            var active = queue.ActiveJob(now);
            for (int i = 0; i < slots.Length; i++)
            {
                bool exists = i < queue.Slots;
                slots[i].gameObject.SetActive(exists);
                if (!exists) continue;

                if (i >= queue.Jobs.Count)
                {
                    slots[i].ShowEmpty();
                    continue;
                }

                var job = queue.Jobs[i];
                _content.TryGetRecipe(job.RecipeId, out var recipe);
                var icon = recipe != null && recipe.Output != null ? recipe.Output.Icon : null;
                if (job.IsDone(now)) slots[i].ShowReady(icon);
                else if (job == active) slots[i].ShowActive(icon, job.Progress(now), FieldPanel.FormatDuration(job.RemainingMs(now)));
                else slots[i].ShowWaiting(icon);
            }

            if (addSlotButton != null)
            {
                bool canAdd = _production.CanAddSlot(_factoryId);
                addSlotButton.gameObject.SetActive(canAdd);
                if (canAdd)
                {
                    addSlotLabel.text = $"+1 slot\n{_production.NextSlotCost(_factoryId):N0}";
                    addSlotButton.interactable = _production.CanAffordNextSlot(_factoryId);
                }
            }

            int ready = queue.CountDone(now);
            collectButton.gameObject.SetActive(ready > 0);
            collectLabel.text = $"Collect ({ready})";

            bool adsAvailable = !(_ads is NoAdService);
            finishNowButton.gameObject.SetActive(active != null && adsAvailable);
            finishNowButton.interactable = _ads.IsRewardedReady;
            finishNowLabel.text = _ads.IsRewardedReady ? "Finish now\n(watch ad)" : "Ad loading...";
        }

        private void OnRecipeClicked(RecipeDefinition recipe)
        {
            if (_factoryId == null) return;
            _production.Enqueue(_factoryId, recipe);
            Refresh();
        }

        private void OnAddSlot()
        {
            if (_factoryId == null || !_production.IsFactory(_factoryId, out var def)) return;
            string id = _factoryId;
            long cost = _production.NextSlotCost(id);
            void Buy()
            {
                _production.TryAddSlot(id);
                Refresh();
            }

            if (_confirm != null) _confirm.Show($"Add a queue slot to {def.DisplayName} for {cost:N0} coins?", "Add slot", Buy);
            else Buy();
        }

        private void OnFinishNow()
        {
            if (_factoryId == null) return;
            string id = _factoryId;
            finishNowButton.interactable = false;
            _ads.ShowRewarded(earned =>
            {
                if (earned) _production.FinishActiveNow(id);
                Refresh();
            });
        }
    }
}
