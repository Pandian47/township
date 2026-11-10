using System;
using TownBuilder.Domain;

namespace TownBuilder
{
    /// <summary>One day of the daily reward calendar (content).</summary>
    [Serializable]
    public sealed class DailyRewardEntry
    {
        public int coins;
        public int gems;
        public MaterialDefinition material;
        public int materialAmount;
    }

    /// <summary>
    /// Daily reward in the Town scene: knows today's local day, pays the calendar slot on claim (materials go to the
    /// barn; if it's full they're paid as coins instead). Plain C#, owned by TownSceneController.
    /// </summary>
    public sealed class DailyRewardService
    {
        private readonly DailyRewardTrack _track;
        private readonly GameContent _content;
        private readonly Wallet _wallet;
        private readonly Inventory _inventory;
        private readonly IClock _clock;
        private readonly Func<long> _utcOffsetMs;

        public DailyRewardService(DailyRewardTrack track, GameContent content, Wallet wallet, Inventory inventory, IClock clock,
            Func<long> utcOffsetMs)
        {
            _track = track;
            _content = content;
            _wallet = wallet;
            _inventory = inventory;
            _clock = clock;
            _utcOffsetMs = utcOffsetMs ?? (() => 0L);
        }

        public event Action<string> Notification;
        public event Action Modified;
        /// <summary>A reward was claimed (calendar slot, 0-based).</summary>
        public event Action<int> Claimed;

        public DailyRewardTrack Track => _track;
        public int CalendarLength => _content.DailyRewards.Count;
        public long Today => DailyRewardTrack.LocalDay(_clock.UtcNowUnixMs, _utcOffsetMs());
        public bool CanClaim => CalendarLength > 0 && _track.CanClaim(Today);

        /// <summary>Slot that today's claim pays (or paid, once claimed).</summary>
        public int TodayIndex => _track.NextIndex(Today);

        public DailyRewardEntry EntryAt(int index) =>
            index >= 0 && index < _content.DailyRewards.Count ? _content.DailyRewards[index] : null;

        /// <summary>Pays today's reward. False if already claimed today.</summary>
        public bool Claim()
        {
            if (!CanClaim) return false;
            int index = _track.Claim(Today);
            var entry = EntryAt(index);
            if (entry == null) return false;

            long coins = entry.coins;
            string extra = "";
            if (entry.material != null && entry.materialAmount > 0)
            {
                if (_inventory.TryAdd(entry.material.Id, entry.materialAmount))
                    extra = $"   +{entry.materialAmount} {entry.material.DisplayName}";
                else
                {
                    long instead = Math.Max(1, entry.material.SellPrice) * entry.materialAmount * 2L;
                    coins += instead;
                    extra = $"   (barn full: +{instead:N0} coins instead of {entry.material.DisplayName})";
                }
            }

            if (coins > 0) _wallet.AddCoins(coins);
            if (entry.gems > 0) _wallet.AddGems(entry.gems);

            string parts = coins > 0 ? $"+{coins:N0} coins" : "";
            if (entry.gems > 0) parts += (parts.Length > 0 ? "   " : "") + (entry.gems == 1 ? "+1 gem" : $"+{entry.gems} gems");
            Notification?.Invoke($"Day {index + 1} reward!   {parts}{extra}");
            Claimed?.Invoke(index);
            Modified?.Invoke();
            return true;
        }

        /// <summary>Developer mode: lets today's reward be claimed again (keeps the streak going).</summary>
        public void DevAllowClaimAgain()
        {
            if (_track.CanClaim(Today)) return;
            _track.Restore(Today - 1, _track.Streak);
            Modified?.Invoke();
        }
    }
}
