using RewardClaims;
using TMPro;
using UnityEngine;

namespace CoinCatcher
{
    /// <summary>
    /// Shows the coin and life balances at the top of the screen, and "No lives left" after a run
    /// was refused. It never keeps its own copy of a balance: it reads the SDK and redraws on
    /// <see cref="ResourceWallet.BalanceChanged"/>.
    /// </summary>
    public class ResourceHud : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private RunGate runGate;
        [SerializeField] private TMP_Text coinsLabel;
        [SerializeField] private TMP_Text livesLabel;
        [SerializeField] private GameObject noLivesMessage;

        private int maxLives;

        // OnEnable, not Start: the HUD should also be right when it is re-enabled later, and it must
        // pick up the balances as they are NOW, since they may have changed while it was hidden.
        // GameBootstrap's earlier execution order guarantees the SDK exists by this point.
        private void OnEnable()
        {
            var wallet = RewardClaimsSdk.Wallet;
            maxLives = bootstrap.MaxLives;

            wallet.BalanceChanged += OnBalanceChanged;
            runGate.NoLivesLeft += OnNoLivesLeft;

            noLivesMessage.SetActive(false);
            ShowCoins(wallet.GetBalance(ResourceIds.Coins));
            ShowLives(wallet.GetBalance(ResourceIds.Lives));
        }

        // The SDK outlives this object, so a missing unsubscribe would keep calling a destroyed HUD.
        private void OnDisable()
        {
            if (RewardClaimsSdk.IsInitialized)
                RewardClaimsSdk.Wallet.BalanceChanged -= OnBalanceChanged;
            runGate.NoLivesLeft -= OnNoLivesLeft;
        }

        private void OnBalanceChanged(string resourceId, int balance)
        {
            if (resourceId == ResourceIds.Coins)
                ShowCoins(balance);
            else if (resourceId == ResourceIds.Lives)
                ShowLives(balance);
        }

        private void OnNoLivesLeft()
        {
            noLivesMessage.SetActive(true);
        }

        private void ShowCoins(int balance)
        {
            // The coin icon next to the label already says what the number is.
            coinsLabel.text = balance.ToString();
        }

        private void ShowLives(int balance)
        {
            // \u2665 is the heart symbol, written as an escape so the file's text encoding can't break it.
            livesLabel.text = $"<color=#FF5A6E>\u2665</color> {balance}/{maxLives}";

            // Once the player has a life again (e.g. bought in the shop), the warning is out of date.
            if (balance > 0)
                noLivesMessage.SetActive(false);
        }
    }
}
