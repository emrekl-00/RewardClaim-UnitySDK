using System;
using System.Threading.Tasks;
using RewardClaims;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoinCatcher
{
    /// <summary>
    /// Claims the first-completion reward of each level through the SDK and shows the claim's status
    /// on the Level complete panel. It never adds coins itself: the SDK grants them, and the coins
    /// HUD redraws on <see cref="ResourceWallet.BalanceChanged"/>.
    /// </summary>
    public class LevelRewards : MonoBehaviour
    {
        [SerializeField] private GameManager game;
        [Tooltip("Coins granted the first time a level is completed.")]
        [SerializeField] private int coinsPerLevel = 100;

        [Header("Level complete panel")]
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button retryButton;

        // The reward the panel is showing. Null while there is nothing to show, e.g. after OnDisable.
        private string currentRewardId;

        // True when the current reward had already succeeded before this completion (a replay).
        private bool claimedBefore;

        private void Awake()
        {
            retryButton.onClick.AddListener(OnRetryClicked);
        }

        // GameBootstrap's earlier execution order guarantees the SDK exists by this point.
        private void OnEnable()
        {
            game.LevelCompleted += OnLevelCompleted;
            RewardClaimsSdk.Claims.ClaimStatusChanged += OnClaimStatusChanged;
        }

        // The SDK outlives this object, so a missing unsubscribe would keep calling a destroyed panel.
        private void OnDisable()
        {
            game.LevelCompleted -= OnLevelCompleted;
            if (RewardClaimsSdk.IsInitialized)
                RewardClaimsSdk.Claims.ClaimStatusChanged -= OnClaimStatusChanged;

            // A claim still in flight finishes in the SDK anyway (and its coins reach the HUD through
            // BalanceChanged); clearing the id makes its late result skip this object's UI.
            currentRewardId = null;
        }

        private void OnLevelCompleted(int levelIndex)
        {
            currentRewardId = RewardIdFor(levelIndex);

            // ClaimAsync hands back the same Succeeded record whether the reward was granted just now
            // or on an earlier completion, so the record alone can't tell "+100" from "already
            // claimed". Only its status BEFORE this call can.
            var existing = RewardClaimsSdk.Claims.GetClaim(currentRewardId);
            claimedBefore = existing != null && existing.Status == ClaimStatus.Succeeded;

            // Called on every completion, not only the first: the SDK is idempotent by reward id, so
            // for a reward it already granted it returns the saved record without sending anything.
            _ = ClaimAndShowAsync(currentRewardId);
        }

        private void OnRetryClicked()
        {
            if (currentRewardId != null)
                _ = ClaimAndShowAsync(currentRewardId);
        }

        // An async Task, not async void: an exception escaping async void can't be caught by anyone.
        // The handlers above discard the task, so every exception is caught and logged here.
        // (Network problems never throw; they end as a Failed record. This is for bugs.)
        private async Task ClaimAndShowAsync(string rewardId)
        {
            try
            {
                var claiming = RewardClaimsSdk.Claims.ClaimAsync(rewardId, ResourceIds.Coins, coinsPerLevel);

                // Show the record as it is right now. A new request already raised Pending, but joining
                // a request that was in flight before (e.g. a startup retry) raises nothing new, and
                // the panel would still show the last level's text.
                ShowIfCurrent(RewardClaimsSdk.Claims.GetClaim(rewardId));

                // ClaimStatusChanged shows the outcome of a request as soon as it answers. The awaited
                // record still matters: for a reward that already succeeded (or was rejected) the SDK
                // returns it without a status change, so no event is raised.
                ShowIfCurrent(await claiming);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnClaimStatusChanged(ClaimRecord claim)
        {
            // The event reports every claim, e.g. startup retries of other levels' rewards.
            ShowIfCurrent(claim);
        }

        private void ShowIfCurrent(ClaimRecord claim)
        {
            // By the time a request answers, the player may have finished another level (or this
            // object was disabled); an old reward's result must not overwrite the panel. Null means
            // the claim was refused as invalid; that exception is logged by ClaimAndShowAsync.
            if (claim == null || claim.RewardId != currentRewardId)
                return;

            // The text comes only from the record's status, never from local "did I claim it" flags,
            // so the panel can't disagree with what the SDK saved.
            retryButton.gameObject.SetActive(claim.Status == ClaimStatus.Failed);
            statusLabel.text = StatusText(claim);
        }

        private string StatusText(ClaimRecord claim)
        {
            switch (claim.Status)
            {
                case ClaimStatus.Pending:
                    // … is the ellipsis character, written as an escape so the file's encoding can't break it.
                    return "Claiming reward\u2026";
                case ClaimStatus.Succeeded:
                    // The record's amount, not coinsPerLevel: the first claim's values are the ones granted.
                    return claimedBefore ? "Reward already claimed" : $"+{claim.Amount} coins!";
                case ClaimStatus.Failed:
                    // True: the SDK retries unfinished claims at startup and when the app comes back.
                    return "Couldn't reach the server. We'll retry automatically.";
                case ClaimStatus.Rejected:
                    return "Reward not available";
                default:
                    return string.Empty;
            }
        }

        // Players count levels from 1, so the id does too: level index 0 is "level-1-first-completion".
        private static string RewardIdFor(int levelIndex)
        {
            return $"level-{levelIndex + 1}-first-completion";
        }
    }
}
