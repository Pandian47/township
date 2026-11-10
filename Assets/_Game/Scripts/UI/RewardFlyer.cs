using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// When coins, gems or XP go up, a few icons fly from where the player just tapped (or the screen centre) to the
    /// matching pill in the top bar. Watches the wallet and progress, so every reward source is covered without changes.
    /// Spending doesn't fly anything. Lives on a full-screen layer above the panels; purely cosmetic.
    /// </summary>
    public sealed class RewardFlyer : MonoBehaviour
    {
        [SerializeField] private RectTransform coinsTarget;
        [SerializeField] private RectTransform gemsTarget;
        [SerializeField] private RectTransform xpTarget;
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite gemSprite;
        [SerializeField] private Sprite xpSprite;
        [SerializeField] private float iconSize = 56f;
        [SerializeField] private float flightSeconds = 0.65f;
        [SerializeField] private float stagger = 0.06f;
        [Tooltip("Seconds after a tap during which rewards fly from the tap position.")]
        [SerializeField] private float tapMemory = 1.2f;

        private sealed class Icon
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 From, Control;
            public RectTransform Target;
            public float Delay, Age;
        }

        private readonly List<Icon> _active = new List<Icon>();
        private readonly Stack<Icon> _pool = new Stack<Icon>();
        private RectTransform _rect;
        private Canvas _canvas;
        private Wallet _wallet;
        private PlayerProgress _progress;
        private long _coins, _gems, _xp;
        private Vector2 _lastTap;
        private float _lastTapTime = -10f;

        public int ActiveIcons => _active.Count;

        public void Initialize(Wallet wallet, PlayerProgress progress)
        {
            Unbind();
            _rect = (RectTransform)transform;
            var c = GetComponentInParent<Canvas>();
            _canvas = c != null ? c.rootCanvas : null;
            _wallet = wallet;
            _progress = progress;
            _coins = wallet.Coins;
            _gems = wallet.Gems;
            _xp = progress.Xp;
            _wallet.Changed += OnWalletChanged;
            _progress.Changed += OnProgressChanged;
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (_wallet != null) _wallet.Changed -= OnWalletChanged;
            if (_progress != null) _progress.Changed -= OnProgressChanged;
        }

        private void OnWalletChanged()
        {
            long coins = _wallet.Coins, gems = _wallet.Gems;
            if (coins > _coins) Launch(coinSprite, coinsTarget, (int)Mathf.Clamp(1 + (coins - _coins) / 25, 1, 8));
            if (gems > _gems) Launch(gemSprite, gemsTarget, (int)Mathf.Clamp(gems - _gems, 1, 5));
            _coins = coins;
            _gems = gems;
        }

        private void OnProgressChanged()
        {
            long xp = _progress.Xp;
            if (xp > _xp) Launch(xpSprite, xpTarget, (int)Mathf.Clamp(1 + (xp - _xp) / 5, 1, 6));
            _xp = xp;
        }

        private void Launch(Sprite sprite, RectTransform target, int count)
        {
            if (sprite == null || target == null || !isActiveAndEnabled) return;
            Vector2 screen = Time.unscaledTime - _lastTapTime <= tapMemory ? _lastTap : new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
            var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, cam, out var from);

            for (int i = 0; i < count; i++)
            {
                var icon = _pool.Count > 0 ? _pool.Pop() : Create();
                icon.Image.sprite = sprite;
                icon.Target = target;
                icon.From = from + Random.insideUnitCircle * 30f;
                icon.Control = icon.From + new Vector2(Random.Range(-160f, 160f), Random.Range(120f, 260f));
                icon.Delay = i * stagger;
                icon.Age = 0f;
                icon.Rect.anchoredPosition = icon.From;
                icon.Rect.localScale = Vector3.zero;
                icon.Rect.gameObject.SetActive(true);
                icon.Rect.SetAsLastSibling();
                _active.Add(icon);
            }
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer != null && (pointer.press.wasPressedThisFrame || pointer.press.isPressed))
            {
                _lastTap = pointer.position.ReadValue();
                _lastTapTime = Time.unscaledTime;
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var icon = _active[i];
                icon.Age += Time.unscaledDeltaTime;
                float t = (icon.Age - icon.Delay) / flightSeconds;
                if (t < 0f) continue;
                if (t >= 1f)
                {
                    _active.RemoveAt(i);
                    icon.Rect.gameObject.SetActive(false);
                    _pool.Push(icon);
                    continue;
                }

                Vector2 to = _rect.InverseTransformPoint(icon.Target.position);
                float e = t * t * (3f - 2f * t);
                Vector2 a = Vector2.Lerp(icon.From, icon.Control, e), b = Vector2.Lerp(icon.Control, to, e);
                icon.Rect.anchoredPosition = Vector2.Lerp(a, b, e);
                float pop = t < 0.15f ? t / 0.15f * 1.2f : Mathf.Lerp(1.2f, 0.65f, (t - 0.15f) / 0.85f);
                icon.Rect.localScale = Vector3.one * pop;
            }
        }

        private Icon Create()
        {
            var go = new GameObject("FlyingReward", typeof(RectTransform));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(iconSize, iconSize);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            return new Icon { Rect = rt, Image = image };
        }
    }
}
