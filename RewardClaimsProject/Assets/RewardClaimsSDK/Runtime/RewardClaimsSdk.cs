using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace RewardClaims
{
    /// <summary>The SDK's entry point: call <see cref="Initialize(RewardClaimsConfig)"/> once, then use <see cref="Wallet"/> and <see cref="Claims"/>.</summary>
    /// <remarks>
    /// A facade: the game sees one static class, while the store, wallet and claim manager behind it
    /// stay plain classes that tests build by hand. Main thread only, like the rest of the SDK.
    /// </remarks>
    public static class RewardClaimsSdk
    {
        private static ResourceWallet wallet;
        private static ClaimManager claims;
        private static RewardClaimsLifecycle lifecycle;

        /// <summary>True after a successful <see cref="Initialize(RewardClaimsConfig)"/>.</summary>
        public static bool IsInitialized => claims != null;

        /// <summary>The player's resource balances.</summary>
        /// <exception cref="InvalidOperationException">The SDK is not initialized yet.</exception>
        public static ResourceWallet Wallet
        {
            get
            {
                EnsureInitialized();
                return wallet;
            }
        }

        /// <summary>Sends reward claims and tracks their status.</summary>
        /// <exception cref="InvalidOperationException">The SDK is not initialized yet.</exception>
        public static ClaimManager Claims
        {
            get
            {
                EnsureInitialized();
                return claims;
            }
        }

        /// <summary>Starts the SDK with PlayerPrefs storage and the httpbin API from <paramref name="config"/>.</summary>
        /// <remarks>Also sends again every claim a previous session left failed or unfinished.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        /// <exception cref="ArgumentException">The config has errors (all of them are listed) or a malformed API url.</exception>
        /// <exception cref="InvalidOperationException">The SDK is already initialized.</exception>
        public static void Initialize(RewardClaimsConfig config)
        {
            // Checked before building the API, so a broken config reports its validation errors
            // rather than whatever the API constructor happens to trip over first.
            EnsureCanInitialize(config);
            Initialize(config, new PlayerPrefsStorage(), new HttpBinRewardApi(config.ApiUrl, config.RequestTimeoutSeconds));
        }

        /// <summary>Starts the SDK with your own storage and reward API, e.g. fakes in tests or a custom backend.</summary>
        /// <remarks>Also sends again every claim a previous session left failed or unfinished.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="config"/>, <paramref name="storage"/> or <paramref name="api"/> is null.</exception>
        /// <exception cref="ArgumentException">The config has errors; all of them are listed in the message.</exception>
        /// <exception cref="InvalidOperationException">The SDK is already initialized.</exception>
        public static void Initialize(RewardClaimsConfig config, IStorage storage, IRewardApi api)
        {
            EnsureCanInitialize(config);
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));
            if (api == null)
                throw new ArgumentNullException(nameof(api));

            // ONE store for both: balances and claims are one saved document. With a store each, every
            // Save() would write that store's stale copy of the other half and undo the other's changes,
            // e.g. a claim's save would bring back a life the player had just spent.
            var store = new ProgressStore(storage, config.PlayerId);
            var newWallet = new ResourceWallet(config.Resources, store);
            var newClaims = new ClaimManager(store, newWallet, api, config.PlayerId);

            // Assigned only once everything was built, so a constructor that throws leaves the SDK
            // uninitialized instead of half set up.
            wallet = newWallet;
            claims = newClaims;

            // DontDestroyOnLoad only works in Play mode, and EditMode tests have no app to pause, so
            // the lifecycle object exists only in a playing game (and in PlayMode tests).
            if (Application.isPlaying)
                lifecycle = RewardClaimsLifecycle.Create();

            // Claims the last session couldn't finish (offline, killed mid-request) are sent now.
            RetryUnfinishedClaimsInBackground();
        }

        // Fire-and-forget for startup and app resume: nobody waits for the retries, the game sees
        // their results through ClaimStatusChanged.
        internal static void RetryUnfinishedClaimsInBackground()
        {
            if (!IsInitialized)
                return;

            // Discarded on purpose; LogFailuresAsync observes every exception, so none goes unnoticed.
            _ = LogFailuresAsync(claims.RetryUnfinishedClaimsAsync());
        }

        // An async Task instead of async void: an exception escaping async void is rethrown on the
        // main thread's context and can't be caught by anyone. Here it is caught and logged instead.
        // (Network problems never get here; they end as Failed claims. This is for bugs, e.g. a game
        // BalanceChanged listener that throws while a retried reward is granted.)
        private static async Task LogFailuresAsync(Task retries)
        {
            try
            {
                await retries;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void EnsureCanInitialize(RewardClaimsConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // A second Initialize would build a second wallet next to the first; objects the game already
            // holds would then show balances that nothing updates any more.
            if (IsInitialized)
                throw new InvalidOperationException(
                    "RewardClaimsSdk is already initialized. Call Initialize once, e.g. from a bootstrap object, " +
                    "and check RewardClaimsSdk.IsInitialized if that code can run again.");

            var errors = config.Validate();
            if (errors.Count > 0)
                throw new ArgumentException(
                    $"RewardClaimsConfig '{config.name}' is invalid:\n" + string.Join("\n", errors.Select(error => "- " + error)),
                    nameof(config));
        }

        private static void EnsureInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "RewardClaimsSdk is not initialized. Call RewardClaimsSdk.Initialize(config) first, " +
                    "e.g. in Awake or Start of your bootstrap object.");
        }

        // With "Enter Play Mode Options" set to skip the domain reload, static fields keep their values
        // from the previous Play session, so the SDK would look initialized with last session's objects
        // and the next Initialize would throw. SubsystemRegistration runs at the start of every Play
        // session, before any scene loads, so we start from a clean state either way.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            // Unity's == null is also true for an object Unity already destroyed (e.g. at the end of
            // the last Play session), so this only destroys a still-living one.
            if (lifecycle != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(lifecycle.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(lifecycle.gameObject);
            }

            lifecycle = null;
            wallet = null;
            claims = null;
        }

        // EditMode tests share one static SDK, so each test resets it in SetUp and TearDown.
        internal static void ResetForTests()
        {
            ResetStaticState();
        }
    }
}
