using RewardClaims;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoinCatcher
{
    /// <summary>
    /// The shop panel's "Buy 1 life" offer: pays coins through the SDK wallet and adds a life. The
    /// button is only clickable when the purchase can go through.
    /// </summary>
    public class Shop : MonoBehaviour
    {
        private const int LivesPerPurchase = 1;

        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private int lifePriceInCoins = 50;
        [SerializeField] private Button buyLifeButton;
        [SerializeField] private TMP_Text buyLifeLabel;
        [Tooltip("Says why the button is disabled.")]
        [SerializeField] private TMP_Text hintLabel;

        private void Awake()
        {
            buyLifeButton.onClick.AddListener(OnBuyLifeClicked);
            buyLifeLabel.text = $"Buy 1 life: {lifePriceInCoins} coins";
        }

        // On this panel, so it runs on every open and reads the balances as they are now.
        private void OnEnable()
        {
            RewardClaimsSdk.Wallet.BalanceChanged += OnBalanceChanged;
            Refresh();
        }

        // The SDK outlives this panel, so a missing unsubscribe would keep calling a destroyed object.
        private void OnDisable()
        {
            if (RewardClaimsSdk.IsInitialized)
                RewardClaimsSdk.Wallet.BalanceChanged -= OnBalanceChanged;
        }

        private void OnBalanceChanged(string resourceId, int balance)
        {
            // Either balance changes what the button can do, e.g. a level reward arriving while the shop is open.
            Refresh();
        }

        private void OnBuyLifeClicked()
        {
            var wallet = RewardClaimsSdk.Wallet;

            // Check the cap BEFORE paying: Add clamps at max lives, so spending first and then finding
            // lives full would take the coins and give nothing. (The button is disabled then, but the
            // rule belongs to the purchase, not to the button.)
            if (wallet.GetBalance(ResourceIds.Lives) >= bootstrap.MaxLives)
                return;

            // Not enough coins is a normal situation, so TrySpend answers false instead of throwing.
            if (!wallet.TrySpend(ResourceIds.Coins, lifePriceInCoins))
                return;

            wallet.Add(ResourceIds.Lives, LivesPerPurchase);
        }

        private void Refresh()
        {
            var wallet = RewardClaimsSdk.Wallet;
            var livesFull = wallet.GetBalance(ResourceIds.Lives) >= bootstrap.MaxLives;
            var canPay = wallet.CanSpend(ResourceIds.Coins, lifePriceInCoins);

            buyLifeButton.interactable = !livesFull && canPay;

            if (livesFull)
                hintLabel.text = "Lives are full";
            else if (!canPay)
                hintLabel.text = $"Not enough coins (need {lifePriceInCoins})";
            else
                hintLabel.text = string.Empty;
        }
    }
}
