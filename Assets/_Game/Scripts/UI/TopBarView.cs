using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TownBuilder
{
    public sealed class TopBarView : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text gemsText;
        [SerializeField] private TMP_Text barnText;

        private Wallet _wallet;
        private PlayerProgress _progress;
        private Inventory _inventory;

        // Last shown values: a counter "pops" when its value goes up.
        private bool _hasShown;
        private long _lastCoins, _lastGems, _lastXp;
        private int _lastBarn;

        public void Bind(Wallet wallet, PlayerProgress progress, Inventory inventory = null)
        {
            Unbind();
            _wallet = wallet;
            _progress = progress;
            _inventory = inventory;
            _hasShown = false;
            _wallet.Changed += Refresh;
            _progress.Changed += Refresh;
            if (_inventory != null) _inventory.Changed += Refresh;
            if (barnText != null) barnText.transform.parent.gameObject.SetActive(_inventory != null);
            Refresh();
        }

        private void Refresh()
        {
            if (_wallet == null || _progress == null) return;
            levelText.text = _progress.HasCurve && !_progress.IsMaxLevel
                ? $"Lv {_progress.Level}  ·  {_progress.XpIntoLevel}/{_progress.XpForNextLevel} XP"
                : $"Lv {_progress.Level}  ·  {_progress.Xp:N0} XP";
            coinsText.text = $"Coins  {_wallet.Coins:N0}";
            gemsText.text = $"Gems  {_wallet.Gems:N0}";
            if (_inventory != null && barnText != null)
            {
                barnText.text = $"Barn  {_inventory.Total}/{_inventory.Capacity}";
                barnText.color = _inventory.FreeSpace == 0 ? new Color(1f, 0.55f, 0.5f) : Color.white;
            }

            if (_hasShown && Application.isPlaying)
            {
                if (_wallet.Coins > _lastCoins) PopPill(coinsText);
                if (_wallet.Gems > _lastGems) PopPill(gemsText);
                if (_progress.Xp > _lastXp) PopPill(levelText);
                if (_inventory != null && _inventory.Total > _lastBarn) PopPill(barnText);
            }

            _hasShown = true;
            _lastCoins = _wallet.Coins;
            _lastGems = _wallet.Gems;
            _lastXp = _progress.Xp;
            _lastBarn = _inventory != null ? _inventory.Total : 0;
        }

        private static void PopPill(TMP_Text text)
        {
            if (text != null && text.transform.parent != null)
                PopAnimator.Play(text.transform.parent, PopAnimator.Style.Uniform, 0.08f, 0.3f);
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
