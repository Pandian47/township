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

        private Wallet _wallet;
        private PlayerProgress _progress;

        public void Bind(Wallet wallet, PlayerProgress progress)
        {
            Unbind();
            _wallet = wallet;
            _progress = progress;
            _wallet.Changed += Refresh;
            _progress.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            if (_wallet == null || _progress == null) return;
            levelText.text = $"Lv {_progress.Level}";
            coinsText.text = $"Coins  {_wallet.Coins:N0}";
            gemsText.text = $"Gems  {_wallet.Gems:N0}";
        }

        private void Unbind()
        {
            if (_wallet != null) _wallet.Changed -= Refresh;
            if (_progress != null) _progress.Changed -= Refresh;
        }

        private void OnDestroy() => Unbind();
    }
}
