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

        /// <summary>The config the SDK was started with, e.g. to read a resource's max balance.</summary>
        public RewardClaimsConfig Config => config;

        private void Awake()
        {
            // The SDK is static and lives for the whole app run, so a second load of this scene
            // must not initialize it again (that throws on purpose).
            if (!RewardClaimsSdk.IsInitialized)
                RewardClaimsSdk.Initialize(config);
        }
    }
}
