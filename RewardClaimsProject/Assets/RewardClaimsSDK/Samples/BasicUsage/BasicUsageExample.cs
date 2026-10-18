using RewardClaims;
using UnityEngine;

// The smallest integration of the Reward Claims SDK. Put this on a GameObject, assign a config asset
// (Create > Reward Claims > Config) and press Play; everything it does is printed to the Console.
//
// No asmdef in this folder on purpose: the script compiles into the game's default Assembly-CSharp,
// which sees RewardClaims.Runtime automatically (its asmdef is "Auto Referenced"), just like a game's
// own scripts will.
public class BasicUsageExample : MonoBehaviour
{
    private const string RewardId = "level-10-first-completion";

    [SerializeField] private RewardClaimsConfig config;

    // async void is acceptable only in Unity event methods like this one, where Unity is the caller.
    private async void Start()
    {
        // 1. Initialize once per app run. The guard matters when this scene is loaded a second time.
        if (!RewardClaimsSdk.IsInitialized)
            RewardClaimsSdk.Initialize(config);

        var wallet = RewardClaimsSdk.Wallet;
        Debug.Log($"[BasicUsage] coins: {wallet.GetBalance("coins")}, lives: {wallet.GetBalance("lives")}");

        // 2. Listen for changes, e.g. to redraw the HUD.
        wallet.BalanceChanged += OnBalanceChanged;
        RewardClaimsSdk.Claims.ClaimStatusChanged += OnClaimStatusChanged;

        // 3. Spend: returns false (never throws) when the balance is too low.
        var spent = wallet.TrySpend("lives", 1);
        Debug.Log($"[BasicUsage] spend 1 life: {(spent ? "ok" : "not enough lives")}");

        // 4. Claim a reward. Safe to call every time: a reward already granted is not sent again.
        var before = RewardClaimsSdk.Claims.GetClaim(RewardId);
        if (before != null && before.Status == ClaimStatus.Succeeded)
            Debug.Log($"[BasicUsage] '{RewardId}' was already claimed; the SDK won't send it again.");

        var claim = await RewardClaimsSdk.Claims.ClaimAsync(RewardId, "coins", 100);
        Debug.Log($"[BasicUsage] claim finished: {claim.Status} (attempts: {claim.AttemptCount}, " +
                  $"last error: {claim.LastError}), coins now {wallet.GetBalance("coins")}");
    }

    private void OnDestroy()
    {
        // The SDK outlives this object, so unsubscribe or it would keep calling a destroyed object.
        if (!RewardClaimsSdk.IsInitialized)
            return;

        RewardClaimsSdk.Wallet.BalanceChanged -= OnBalanceChanged;
        RewardClaimsSdk.Claims.ClaimStatusChanged -= OnClaimStatusChanged;
    }

    private void OnBalanceChanged(string resourceId, int balance)
    {
        Debug.Log($"[BasicUsage] {resourceId} -> {balance}");
    }

    private void OnClaimStatusChanged(ClaimRecord claim)
    {
        Debug.Log($"[BasicUsage] claim '{claim.RewardId}' is now {claim.Status}");
    }
}
