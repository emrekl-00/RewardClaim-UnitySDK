using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RewardClaims.Tests
{
    // The facade is static, so every test resets it before and after: one test's Initialize must
    // never leak into the next.
    public class RewardClaimsSdkTests
    {
        private const string RewardId = "level-10-first-completion";

        private RewardClaimsConfig config;
        private InMemoryStorage storage;
        private FakeRewardApi api;

        [SetUp]
        public void SetUp()
        {
            RewardClaimsSdk.ResetForTests();

            config = ScriptableObject.CreateInstance<RewardClaimsConfig>();
            config.PlayerId = TestWallets.PlayerId;
            config.Resources = new[]
            {
                ResourceDefinition.Create("coins", startingBalance: 0),
                ResourceDefinition.Create("lives", startingBalance: 5, maxBalance: 5),
            };

            storage = new InMemoryStorage();
            api = new FakeRewardApi();
        }

        [TearDown]
        public void TearDown()
        {
            api.AnswerAllHeld(ApiResult.Failure(ClaimError.Timeout));
            RewardClaimsSdk.ResetForTests();
            UnityEngine.Object.DestroyImmediate(config);
        }

        // Leaves a Failed claim in the storage: what a previous app session that was offline saves.
        private void SaveFailedClaimFromPreviousSession(int amount)
        {
            var store = new ProgressStore(storage, TestWallets.PlayerId);
            var wallet = new ResourceWallet(config.Resources, store);
            var offlineApi = new FakeRewardApi { Result = ApiResult.Failure(ClaimError.NoConnection) };
            var manager = new ClaimManager(store, wallet, offlineApi, TestWallets.PlayerId);

            var claim = manager.ClaimAsync(RewardId, "coins", amount).Result;
            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Failed), "test setup");
        }

        // --- Before Initialize ---

        [Test]
        public void BeforeInitialize_IsNotInitialized()
        {
            Assert.That(RewardClaimsSdk.IsInitialized, Is.False);
        }

        [Test]
        public void Wallet_BeforeInitialize_ThrowsWithHint()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => _ = RewardClaimsSdk.Wallet);
            Assert.That(exception.Message, Does.Contain("Call RewardClaimsSdk.Initialize(config) first"));
        }

        [Test]
        public void Claims_BeforeInitialize_ThrowsWithHint()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => _ = RewardClaimsSdk.Claims);
            Assert.That(exception.Message, Does.Contain("Call RewardClaimsSdk.Initialize(config) first"));
        }

        // --- Bad arguments: throw, and leave the SDK uninitialized ---

        [Test]
        public void Initialize_NullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => RewardClaimsSdk.Initialize(null));
            Assert.Throws<ArgumentNullException>(() => RewardClaimsSdk.Initialize(null, storage, api));
            Assert.That(RewardClaimsSdk.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_NullStorageOrApi_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => RewardClaimsSdk.Initialize(config, null, api));
            Assert.Throws<ArgumentNullException>(() => RewardClaimsSdk.Initialize(config, storage, null));
            Assert.That(RewardClaimsSdk.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_InvalidConfig_ThrowsWithEveryValidationMessage()
        {
            config.PlayerId = "";
            config.RequestTimeoutSeconds = 0;

            var exception = Assert.Throws<ArgumentException>(() => RewardClaimsSdk.Initialize(config, storage, api));

            Assert.That(exception.Message, Does.Contain("Player id is empty."));
            Assert.That(exception.Message, Does.Contain("Request timeout must be greater than 0 seconds (was 0)."));
            Assert.That(RewardClaimsSdk.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_DefaultOverload_InvalidConfig_ReportsValidationErrors()
        {
            // An empty url would also break the httpbin client; the designer must see the config's message.
            config.ApiUrl = "";

            var exception = Assert.Throws<ArgumentException>(() => RewardClaimsSdk.Initialize(config));

            Assert.That(exception.Message, Does.Contain("API url is empty."));
            Assert.That(RewardClaimsSdk.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_DefaultOverload_MalformedUrl_ThrowsAndStaysUninitialized()
        {
            // Not empty, so Validate accepts it; the httpbin client is the one that refuses it.
            config.ApiUrl = "not a url";

            Assert.Throws<ArgumentException>(() => RewardClaimsSdk.Initialize(config));
            Assert.That(RewardClaimsSdk.IsInitialized, Is.False);
        }

        [Test]
        public void Initialize_Twice_ThrowsAndKeepsFirstSdk()
        {
            RewardClaimsSdk.Initialize(config, storage, api);
            var firstWallet = RewardClaimsSdk.Wallet;

            var exception = Assert.Throws<InvalidOperationException>(
                () => RewardClaimsSdk.Initialize(config, new InMemoryStorage(), new FakeRewardApi()));

            Assert.That(exception.Message, Does.Contain("already initialized"));
            Assert.That(RewardClaimsSdk.Wallet, Is.SameAs(firstWallet));
        }

        // --- After Initialize ---

        [Test]
        public void Initialize_WalletHasStartingBalances()
        {
            RewardClaimsSdk.Initialize(config, storage, api);

            Assert.That(RewardClaimsSdk.IsInitialized, Is.True);
            Assert.That(RewardClaimsSdk.Wallet.GetBalance("coins"), Is.EqualTo(0));
            Assert.That(RewardClaimsSdk.Wallet.GetBalance("lives"), Is.EqualTo(5));
        }

        [Test]
        public async Task ClaimThroughFacade_GrantsRewardOnce()
        {
            RewardClaimsSdk.Initialize(config, storage, api);

            var first = await RewardClaimsSdk.Claims.ClaimAsync(RewardId, "coins", 100);
            var second = await RewardClaimsSdk.Claims.ClaimAsync(RewardId, "coins", 100);

            Assert.That(first.Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(second, Is.SameAs(first));
            Assert.That(RewardClaimsSdk.Wallet.GetBalance("coins"), Is.EqualTo(100));
            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(api.ReceivedPayloads[0].PlayerId, Is.EqualTo(TestWallets.PlayerId));
        }

        [Test]
        public async Task WalletAndClaims_ShareOneSave()
        {
            RewardClaimsSdk.Initialize(config, storage, api);

            // A spend and then a claim: with two separate stores the claim's save would bring the life back.
            Assert.That(RewardClaimsSdk.Wallet.TrySpend("lives", 1), Is.True);
            await RewardClaimsSdk.Claims.ClaimAsync(RewardId, "coins", 100);

            // Next launch, on the same storage.
            RewardClaimsSdk.ResetForTests();
            RewardClaimsSdk.Initialize(config, storage, new FakeRewardApi());

            Assert.That(RewardClaimsSdk.Wallet.GetBalance("lives"), Is.EqualTo(4));
            Assert.That(RewardClaimsSdk.Wallet.GetBalance("coins"), Is.EqualTo(100));
            Assert.That(RewardClaimsSdk.Claims.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Succeeded));
        }

        [Test]
        public void Initialize_RetriesFailedClaimFromPreviousSession()
        {
            SaveFailedClaimFromPreviousSession(amount: 100);

            // The fake answers synchronously, so the retry Initialize starts has finished when it returns.
            RewardClaimsSdk.Initialize(config, storage, api);

            Assert.That(api.ReceivedPayloads, Has.Count.EqualTo(1));
            Assert.That(api.ReceivedPayloads[0].RewardId, Is.EqualTo(RewardId));
            Assert.That(RewardClaimsSdk.Claims.GetClaim(RewardId).Status, Is.EqualTo(ClaimStatus.Succeeded));
            Assert.That(RewardClaimsSdk.Wallet.GetBalance("coins"), Is.EqualTo(100));
        }

        [Test]
        public async Task BackgroundRetry_ThatFails_IsLoggedNotLost()
        {
            SaveFailedClaimFromPreviousSession(amount: 100);
            api.HoldRequests = true;
            RewardClaimsSdk.Initialize(config, storage, api);

            // A buggy game listener throws while the retried reward is granted. Nobody awaits the
            // startup retry, so the facade must log the exception rather than let it vanish.
            RewardClaimsSdk.Wallet.BalanceChanged += (_, __) => throw new InvalidOperationException("listener bug");
            var retry = RewardClaimsSdk.Claims.ClaimAsync(RewardId, "coins", 100); // the same in-flight task
            LogAssert.Expect(LogType.Exception, new Regex("listener bug"));

            api.AnswerHeld(ApiResult.Success());

            // Awaited, not Assert.ThrowsAsync: that blocks the main thread, which must stay free to run
            // the claim's continuation.
            try
            {
                await retry;
                Assert.Fail("The listener's exception should fault the claim task.");
            }
            catch (InvalidOperationException)
            {
            }

            // The facade's logging continuation runs on a later editor tick.
            for (var tick = 0; tick < 10; tick++)
                await Task.Yield();

            Assert.That(RewardClaimsSdk.Wallet.GetBalance("coins"), Is.EqualTo(100));
        }

        [Test]
        public void Initialize_InEditMode_CreatesNoLifecycleObject()
        {
            RewardClaimsSdk.Initialize(config, storage, api);

            // FindObjectsOfTypeAll also finds hidden objects.
            Assert.That(Resources.FindObjectsOfTypeAll<RewardClaimsLifecycle>(), Is.Empty);
        }

        [Test]
        public void ResetForTests_MakesSdkUninitializedAgain()
        {
            RewardClaimsSdk.Initialize(config, storage, api);

            RewardClaimsSdk.ResetForTests();

            Assert.That(RewardClaimsSdk.IsInitialized, Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = RewardClaimsSdk.Wallet);
            Assert.DoesNotThrow(() => RewardClaimsSdk.Initialize(config, storage, api));
        }
    }

    // The default overload writes to the real Editor PlayerPrefs, so it gets a fixture of its own
    // with a unique player id whose key is deleted afterwards.
    public class RewardClaimsSdkDefaultOverloadTests
    {
        private RewardClaimsConfig config;
        private string progressKey;

        [SetUp]
        public void SetUp()
        {
            RewardClaimsSdk.ResetForTests();

            var playerId = "sdk-tests-" + Guid.NewGuid();
            progressKey = "RewardClaims.Progress." + playerId;

            config = ScriptableObject.CreateInstance<RewardClaimsConfig>();
            config.PlayerId = playerId;
            config.Resources = new[] { ResourceDefinition.Create("lives", startingBalance: 5, maxBalance: 5) };
        }

        [TearDown]
        public void TearDown()
        {
            RewardClaimsSdk.ResetForTests();
            PlayerPrefs.DeleteKey(progressKey);
            PlayerPrefs.Save();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void Initialize_UsesPlayerPrefs()
        {
            // No saved claims, so the startup retry sends nothing and this test needs no network.
            RewardClaimsSdk.Initialize(config);
            RewardClaimsSdk.Wallet.TrySpend("lives", 1);

            Assert.That(PlayerPrefs.GetString(progressKey), Does.Contain("\"balance\":4"));
        }
    }
}
