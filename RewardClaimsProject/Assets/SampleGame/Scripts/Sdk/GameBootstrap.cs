using System.Linq;
using RewardClaims;
using UnityEngine;

namespace CoinCatcher
{
    /// <summary>
    /// The game's single SDK init point: starts the Reward Claims SDK with the game's config before
    /// any other script needs it.
    /// </summary>
    // Unity calls Awake and then OnEnable on one object before it moves to the next, and the order
    // between objects is not defined. So without help, the ResourceHud's OnEnable could read the
    // wallet before this Awake has created it. DefaultExecutionOrder puts this script ahead of every
    // script with the default order (0) for each lifecycle message: its Awake runs before their
    // Awake/OnEnable, and so on for Start and Update.
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private RewardClaimsConfig config;

        // Static like the SDK it belongs to: a second load of this scene doesn't initialize the SDK
        // again, and its bootstrap must still hand out the API the SDK is actually using.
        private static ToggleableRewardApi rewardApi;

        /// <summary>The config the SDK was started with, e.g. to read a resource's max balance.</summary>
        public RewardClaimsConfig Config => config;

        /// <summary>The reward API the SDK sends claims through; the debug "Simulate offline" switch flips it.</summary>
        public ToggleableRewardApi RewardApi => rewardApi;

        private void Awake()
        {
            // The SDK is static and lives for the whole app run, so a second load of this scene
            // must not initialize it again (that throws on purpose).
            if (RewardClaimsSdk.IsInitialized)
                return;

            // The same storage and API that Initialize(config) would build, with the API wrapped so
            // the demo can switch it "offline". The SDK only sees an IRewardApi, so no SDK code changes.
            rewardApi = new ToggleableRewardApi(new HttpBinRewardApi(config.ApiUrl, config.RequestTimeoutSeconds));
            RewardClaimsSdk.Initialize(config, new PlayerPrefsStorage(), rewardApi);
        }

        // The wallet only reports balances; the cap is a config rule, so it is read from the same
        // asset the SDK was started with.
        /// <summary>The most lives a player can hold (CoinCatcherConfig caps lives).</summary>
        public int MaxLives => config.Resources.First(resource => resource.Id == ResourceIds.Lives).MaxBalance;
    }
}
