using TownBuilder.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    /// <summary>
    /// The Collections window (opened by the chest button under the menu button): one row per collection with its five
    /// items (dark until found, with a count), where they're found, the reward and Complete. The button's badge counts
    /// sets ready to complete.
    /// </summary>
    public sealed class CollectionsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text badgeText;
        [SerializeField] private CollectionRowView[] rows = new CollectionRowView[0];

        private CollectionService _service;

        public bool IsOpen => panelRoot.activeSelf;

        public void Initialize(CollectionService service)
        {
            Unbind();
            _service = service;
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(() => panelRoot.SetActive(false));
            _service.Modified += Refresh;
            panelRoot.SetActive(false);
            Refresh();
        }

        public void Open()
        {
            panelRoot.SetActive(true);
            Refresh();
        }

        private void Refresh()
        {
            if (_service != null) Render(_service.Book, _service.Content);
        }

        /// <summary>Draws the rows and badge (also used by the editor preview).</summary>
        public void Render(CollectionBook book, GameContent content)
        {
            int ready = book.CompletableCount;
            if (badge != null) badge.SetActive(ready > 0);
            if (badgeText != null) badgeText.text = ready.ToString();

            int r = 0;
            foreach (var set in book.Sets)
            {
                if (r >= rows.Length) break;
                if (!content.TryGetCollection(set.Id, out var def)) continue;
                string id = set.Id;
                var row = rows[r++];
                row.Show(def, book, () =>
                {
                    _service?.Complete(id);
                    Refresh();
                });
            }

            for (; r < rows.Length; r++) rows[r].Hide();
        }

        private void Unbind()
        {
            if (_service != null) _service.Modified -= Refresh;
        }

        private void OnDestroy() => Unbind();
    }
}
