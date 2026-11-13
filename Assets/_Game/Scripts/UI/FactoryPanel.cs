using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    /// <summary>
    /// The production window, shown while a factory (e.g. Bakery) is selected (v0.41.0 look): a wooden window over a
    /// darkened town with the factory's name on the sign, ‹ › to step through your other factories, a shelf of baskets
    /// (the queue, plus a padlock basket to buy another slot), a Collect tray for finished products and the products
    /// along the bottom. Tapping a product flies its icon into the next free basket.
    /// </summary>
    public sealed class FactoryPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text queueTitleText;

        [Header("Window (v0.41.0)")]
        [Tooltip("Closes the window and deselects the factory. The darkened town behind the window does the same.")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button dimmerButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [Tooltip("Where flying product icons are drawn (above everything in the window).")]
        [SerializeField] private RectTransform flyLayer;
        [Tooltip("Icon of the first finished product, on the Collect tray (the factory's own picture, faded, when none).")]
        [SerializeField] private Image collectIcon;
        [Tooltip("Golden glow behind the Collect tray's product, only while something is ready.")]
        [SerializeField] private GameObject collectGlow;
        [Tooltip("The machine's lamp: green while something is ready, dark otherwise.")]
        [SerializeField] private Image collectLamp;
        [Tooltip("The conveyor belt under the baskets; runs while the factory is making something.")]
        [SerializeField] private BeltScroller belt;

        [Header("Recipes")]
        [SerializeField] private Transform recipesParent;
        [SerializeField] private RecipeButtonView recipeTemplate;

        [Header("Queue")]
        [SerializeField] private QueueSlotView[] slots = new QueueSlotView[0];
        [SerializeField] private Button collectButton;
        [SerializeField] private TMP_Text collectLabel;
        [SerializeField] private Button finishNowButton;
        [SerializeField] private TMP_Text finishNowLabel;
        [Tooltip("Finish the product being made now for gems (1 per minute left).")]
        [SerializeField] private Button gemsButton;
        [SerializeField] private TMP_Text gemsLabel;
        [Tooltip("Padlock basket after the queue slots: buys an extra queue slot for coins.")]
        [SerializeField] private Button addSlotButton;
        [SerializeField] private TMP_Text addSlotLabel;

        private const float FlySeconds = 0.38f;

        private readonly List<RecipeButtonView> _recipeButtons = new List<RecipeButtonView>();
        private ProductionController _production;
        private GameContent _content;
        private IAdService _ads;
        private ConfirmDialog _confirm;
        private string _factoryId;
        private Sprite _factorySprite;
        private float _nextRefresh;

        public string FactoryId => _factoryId;
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        /// <summary>Close or the darkened town was tapped: the HUD deselects the factory.</summary>
        public event Action CloseRequested;

        /// <summary>‹ or › was tapped: the HUD selects this other factory (instance id).</summary>
        public event Action<string> NavigateRequested;

        /// <summary>Icons currently flying into a basket (tests wait for them).</summary>
        public int FlyingIcons { get; private set; }

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
            if (gemsButton != null)
                gemsButton.onClick.AddListener(() =>
                {
                    if (_factoryId == null) return;
                    _production.FinishWithGems(_factoryId);
                    Refresh();
                });
            if (closeButton != null) closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            if (dimmerButton != null) dimmerButton.onClick.AddListener(() => CloseRequested?.Invoke());
            if (previousButton != null) previousButton.onClick.AddListener(() => Step(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
            _factoryId = null;
            panelRoot.SetActive(false);
        }

        public void ShowFor(string factoryId)
        {
            if (!_production.IsFactory(factoryId, out var def)) { Hide(); return; }
            bool sameType = _factoryId != null && _production.IsFactory(_factoryId, out var shown) && shown == def && _recipeButtons.Count > 0;
            _factoryId = factoryId;
            _factorySprite = def.Sprite;
            titleText.text = def.DisplayName.ToUpperInvariant();
            foreach (var slot in slots) slot.ResetState();

            // Rebuild product badges for this factory type.
            if (!sameType)
            {
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
            }

            bool several = OtherFactories().Count > 1;
            if (previousButton != null) previousButton.gameObject.SetActive(several);
            if (nextButton != null) nextButton.gameObject.SetActive(several);
            var transition = panelRoot.GetComponent<PanelTransition>();
            if (transition != null && transition.IsClosing) panelRoot.SetActive(false); // reopened while closing: open again
            if (!panelRoot.activeSelf) panelRoot.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            _factoryId = null;
            if (!panelRoot.activeSelf) return;
            var transition = panelRoot.GetComponent<PanelTransition>();
            if (transition != null) transition.Close();
            else panelRoot.SetActive(false);
        }

        /// <summary>Every finished factory in town, in a steady order (by type, then by when it was built).</summary>
        private List<string> OtherFactories() => _production.BuiltFactoryIds().ToList();

        private void Step(int direction)
        {
            if (_factoryId == null) return;
            var all = OtherFactories();
            if (all.Count < 2) return;
            int i = all.IndexOf(_factoryId);
            string next = all[((i < 0 ? 0 : i) + direction + all.Count) % all.Count];
            NavigateRequested?.Invoke(next);
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
            Sprite firstReady = null;
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
                if (job.IsDone(now))
                {
                    slots[i].ShowReady(icon);
                    if (firstReady == null) firstReady = icon;
                }
                else if (job == active) slots[i].ShowActive(icon, job.Progress(now), FieldPanel.FormatDuration(job.RemainingMs(now)));
                else slots[i].ShowWaiting(icon);
            }

            if (addSlotButton != null)
            {
                bool canAdd = _production.CanAddSlot(_factoryId);
                addSlotButton.gameObject.SetActive(canAdd);
                if (canAdd)
                {
                    addSlotLabel.text = $"{_production.NextSlotCost(_factoryId):N0}";
                    addSlotButton.interactable = _production.CanAffordNextSlot(_factoryId);
                }
            }

            int ready = queue.CountDone(now);
            collectButton.interactable = ready > 0;
            collectLabel.text = ready > 0 ? $"Collect ({ready})" : "Nothing ready";
            if (collectIcon != null)
            {
                // Something ready: its picture. Nothing: the factory itself, faded, as a quiet placeholder.
                collectIcon.sprite = firstReady != null ? firstReady : _factorySprite;
                collectIcon.enabled = collectIcon.sprite != null;
                collectIcon.color = firstReady != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                collectIcon.preserveAspect = true;
            }

            if (collectGlow != null) collectGlow.SetActive(firstReady != null);
            if (collectLamp != null) collectLamp.color = firstReady != null ? new Color(0.5f, 1f, 0.4f) : new Color(0.35f, 0.4f, 0.42f);
            if (belt != null) belt.Running = active != null;

            bool adsAvailable = !(_ads is NoAdService);
            finishNowButton.gameObject.SetActive(active != null && adsAvailable);
            finishNowButton.interactable = _ads.IsRewardedReady;
            finishNowLabel.text = _ads.IsRewardedReady ? "Finish now\n(watch ad)" : "Ad loading...";

            if (gemsButton != null)
            {
                int gems = active != null ? _production.GemCostToFinish(_factoryId) : 0;
                gemsButton.gameObject.SetActive(gems > 0);
                if (gems > 0)
                {
                    gemsLabel.text = $"Finish now\n{gems} gem{(gems == 1 ? "" : "s")}";
                    gemsButton.interactable = _production.HasGems(gems);
                }
            }
        }

        private void OnRecipeClicked(RecipeDefinition recipe)
        {
            if (_factoryId == null) return;
            var queue = _production.GetQueue(_factoryId);
            int target = queue != null ? queue.Jobs.Count : -1;
            var from = _recipeButtons.FirstOrDefault(b => b.Recipe == recipe);
            bool added = _production.Enqueue(_factoryId, recipe) == QueueResult.Queued;
            Refresh();
            if (!added || target < 0 || target >= slots.Length) return;

            var slot = slots[target];
            var sprite = recipe.Output != null ? recipe.Output.Icon : null;
            if (MotionPreferences.ReduceMotion || flyLayer == null || from == null || sprite == null || !isActiveAndEnabled)
            {
                slot.Pop();
                return;
            }

            StartCoroutine(Fly(sprite, from.IconRect, slot));
        }

        /// <summary>The product's icon arcs from its badge into the basket, then the basket pops.</summary>
        private IEnumerator Fly(Sprite sprite, RectTransform from, QueueSlotView slot)
        {
            FlyingIcons++;
            var go = new GameObject("FlyingIcon", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(flyLayer, false);
            rect.sizeDelta = from.rect.size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var target = (RectTransform)slot.Icon.transform;
            slot.Icon.enabled = false; // revealed when the icon lands

            Vector3 start = from.position, end = target.position;
            Vector2 startSize = from.rect.size, endSize = target.rect.size;
            float lift = Mathf.Abs(end.y - start.y) * 0.5f + 80f * flyLayer.lossyScale.y;
            for (float t = 0f; t < FlySeconds; t += Time.unscaledDeltaTime)
            {
                if (rect == null || slot == null) break;
                float k = t / FlySeconds;
                float e = 1f - (1f - k) * (1f - k);
                var p = Vector3.Lerp(start, end, e);
                p.y += Mathf.Sin(k * Mathf.PI) * lift;
                rect.position = p;
                rect.sizeDelta = Vector2.Lerp(startSize, endSize, e);
                yield return null;
            }

            if (go != null) Destroy(go);
            FlyingIcons--;
            if (slot == null) yield break;
            slot.Icon.enabled = slot.Icon.sprite != null;
            slot.Pop();
        }

        private void OnDisable() => FlyingIcons = 0;

        private void OnAddSlot()
        {
            if (_factoryId == null || !_production.IsFactory(_factoryId, out var def)) return;
            string id = _factoryId;
            long cost = _production.NextSlotCost(id);
            void Buy()
            {
                if (_production.TryAddSlot(id) && _factoryId == id && QueueSlot(id, out var slot)) slot.Pop();
                Refresh();
            }

            if (_confirm != null) _confirm.Show($"Add a queue basket to {def.DisplayName} for {cost:N0} coins?", "Add slot", Buy);
            else Buy();
        }

        /// <summary>The basket a newly bought slot appears as (the last one).</summary>
        private bool QueueSlot(string id, out QueueSlotView slot)
        {
            slot = null;
            var queue = _production.GetQueue(id);
            if (queue == null || queue.Slots < 1 || queue.Slots > slots.Length) return false;
            Refresh();
            slot = slots[queue.Slots - 1];
            return true;
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
