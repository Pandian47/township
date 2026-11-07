using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>Order board screen: one card per order slot. Refreshes while open (inventory and skip timers change).</summary>
    public sealed class OrdersPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private OrderCardView[] cards = new OrderCardView[0];
        [SerializeField] private Button closeButton;

        private OrderService _orders;
        private GameContent _content;
        private Inventory _inventory;
        private float _nextRefresh;

        public bool IsOpen => panelRoot.activeSelf;

        public void Initialize(OrderService orders, GameContent content, Inventory inventory)
        {
            _orders = orders;
            _content = content;
            _inventory = inventory;
            for (int i = 0; i < cards.Length; i++)
            {
                int slot = i;
                cards[i].Initialize(() => { _orders.Deliver(slot); Refresh(); }, () => { _orders.Skip(slot); Refresh(); });
            }

            closeButton.onClick.AddListener(Close);
            Close();
        }

        public void Open()
        {
            _orders.EnsureFilled();
            panelRoot.SetActive(true);
            Refresh();
        }

        public void Close() => panelRoot.SetActive(false);

        private void Update()
        {
            if (!IsOpen || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }

        private void Refresh()
        {
            var board = _orders.Board;
            long now = _orders.NowUnixMs;
            for (int i = 0; i < cards.Length; i++)
            {
                bool exists = i < board.SlotCount;
                cards[i].gameObject.SetActive(exists);
                if (exists) cards[i].Show(board.Get(i), now, _orders.CanDeliver(i), _content, _inventory);
            }
        }
    }
}
