using System;
using System.Globalization;
using System.Threading.Tasks;
using RewardClaims;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoinCatcher
{
    /// <summary>
    /// A free life once a day, claimed through the SDK as reward "daily-life-yyyy-MM-dd". The date in
    /// the id is the whole "once a day" rule: the SDK grants a reward id at most once, so a second
    /// tap the same day gets the saved record back and sends nothing.
    /// </summary>
    // Disabled at max lives: the SDK would still mark the claim Succeeded, but the wallet clamps at the
    // max, so the day's only bonus would be used up for nothing. (It can still clamp if lives fill up
    // while the request is in flight; the SDK then saves it as Succeeded with nothing added.)
    public class DailyBonus : MonoBehaviour
    {
        private const int LivesPerBonus = 1;

        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text statusLabel;

        // True when today's bonus had already succeeded before the latest tap (or before the panel
        // opened), so the text says "already claimed" instead of "+1 life".
        private bool claimedBefore;

        private void Awake()
        {
            claimButton.onClick.AddListener(OnClaimClicked);
        }

        // On the shop panel, so it runs on every open: the day may have changed, or a startup retry
        // may have finished today's claim while the panel was closed.
        private void OnEnable()
        {
            RewardClaimsSdk.Wallet.BalanceChanged += OnBalanceChanged;
            RewardClaimsSdk.Claims.ClaimStatusChanged += OnClaimStatusChanged;

            var today = RewardClaimsSdk.Claims.GetClaim(TodayRewardId());
            claimedBefore = today != null && today.Status == ClaimStatus.Succeeded;
            Show(today);
        }

        // The SDK outlives this panel, so a missing unsubscribe would keep calling a destroyed object.
        private void OnDisable()
        {
            if (!RewardClaimsSdk.IsInitialized)
                return;

            RewardClaimsSdk.Wallet.BalanceChanged -= OnBalanceChanged;
            RewardClaimsSdk.Claims.ClaimStatusChanged -= OnClaimStatusChanged;
        }

        private void OnClaimClicked()
        {
            var rewardId = TodayRewardId();
            var existing = RewardClaimsSdk.Claims.GetClaim(rewardId);
            claimedBefore = existing != null && existing.Status == ClaimStatus.Succeeded;

            // Called again on every tap on purpose: the SDK, not this button, decides "already claimed".
            _ = ClaimAndShowAsync(rewardId);
        }

        // An async Task, not async void, so every exception is caught and logged here. (Network
        // problems never throw; they end as a Failed record. This is for bugs.)
        private async Task ClaimAndShowAsync(string rewardId)
        {
            try
            {
                var claiming = RewardClaimsSdk.Claims.ClaimAsync(rewardId, ResourceIds.Lives, LivesPerBonus);
                Show(RewardClaimsSdk.Claims.GetClaim(rewardId));

                // An already succeeded claim comes back without a status change, so no event shows it.
                var claim = await claiming;

                // The panel may have been closed (or destroyed) while the request ran; the next open
                // reads the record again anyway.
                if (this != null && isActiveAndEnabled)
                    Show(claim);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnClaimStatusChanged(ClaimRecord claim)
        {
            // The event reports every claim; only today's bonus belongs on this panel.
            if (claim.RewardId == TodayRewardId())
                Show(claim);
        }

        private void OnBalanceChanged(string resourceId, int balance)
        {
            if (resourceId == ResourceIds.Lives)
                Show(RewardClaimsSdk.Claims.GetClaim(TodayRewardId()));
        }

        private void Show(ClaimRecord today)
        {
            var livesFull = RewardClaimsSdk.Wallet.GetBalance(ResourceIds.Lives) >= bootstrap.MaxLives;
            claimButton.interactable = !livesFull;
            statusLabel.text = StatusText(today, livesFull);
        }

        private string StatusText(ClaimRecord today, bool livesFull)
        {
            if (today == null)
                return livesFull ? "Lives are full. Save today's free life for later." : "One free life every day.";

            switch (today.Status)
            {
                case ClaimStatus.Pending:
                    return "Claiming…";
                case ClaimStatus.Succeeded:
                    return claimedBefore ? "Already claimed today. Come back tomorrow!" : $"+{today.Amount} life!";
                case ClaimStatus.Failed:
                    return "Couldn't reach the server. We'll retry automatically.";
                case ClaimStatus.Rejected:
                    return "Today's free life isn't available.";
                default:
                    return string.Empty;
            }
        }

        // The player's local date, so "a day" matches the player's own midnight. Invariant culture keeps
        // the id the same on every device language (some calendars would format a different year).
        private static string TodayRewardId()
        {
            return "daily-life-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
