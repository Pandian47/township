using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TownBuilder
{
    public sealed class TopBarView : MonoBehaviour
    {
        [SerializeField] private Text levelText;
        [SerializeField] private Text coinsText;
        [SerializeField] private Text gemsText;
        [SerializeField] private Text barnText;

        private Wallet _wallet;
        private PlayerProgress _progress;
        private Inventory _inventory;

        public void Bind(Wallet wallet, PlayerProgress progress, Inventory inventory = null)
        {
            Unbind();
            _wallet = wallet;
            _progress = progress;
            _inventory = inventory;
            _wallet.Changed += Refresh;
            _progress.Changed += Refresh;
            if (_inventory != null) _inventory.Changed += Refresh;
            if (barnText != null) barnText.transform.parent.gameObject.SetActive(_inventory != null);
            Refresh();
        }

        private void Refresh()
        {
            if (_wallet == null || _progress == null) return;
            levelText.text = $"Lv {_progress.Level}  ·  {_progress.Xp:N0} XP";
            coinsText.text = $"Coins  {_wallet.Coins:N0}";
            gemsText.text = $"Gems  {_wallet.Gems:N0}";
            if (_inventory != null && barnText != null)
            {
                barnText.text = $"Barn  {_inventory.Total}/{_inventory.Capacity}";
                barnText.color = _inventory.FreeSpace == 0 ? new Color(1f, 0.55f, 0.5f) : Color.white;
            }
        }

        private void Unbind()
        {
            if (_wallet != null) _wallet.Changed -= Refresh;
            if (_progress != null) _progress.Changed -= Refresh;
            if (_inventory != null) _inventory.Changed -= Refresh;
        }

        private void OnDestroy() => Unbind();
    }
}
