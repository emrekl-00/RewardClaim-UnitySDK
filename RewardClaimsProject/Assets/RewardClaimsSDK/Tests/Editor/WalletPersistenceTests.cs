using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RewardClaims.Tests
{
    // A "restart" is a new ProgressStore + ResourceWallet on the SAME storage, like the next app launch.
    public class WalletPersistenceTests
    {
        private static readonly ResourceDefinition Coins = ResourceDefinition.Create("coins", startingBalance: 100);
        private static readonly ResourceDefinition Lives = ResourceDefinition.Create("lives", startingBalance: 3, maxBalance: 5);

        private CountingStorage storage;

        [SetUp]
        public void SetUp()
        {
            storage = new CountingStorage();
        }

        [Test]
        public void Restart_AfterSpend_KeepsBalance()
        {
            TestWallets.Create(storage, Coins, Lives).TrySpend("coins", 30);

            var restarted = TestWallets.Create(storage, Coins, Lives);

            Assert.That(restarted.GetBalance("coins"), Is.EqualTo(70));
            Assert.That(restarted.GetBalance("lives"), Is.EqualTo(3));
        }

        [Test]
        public void Restart_AfterAdd_KeepsBalance()
        {
            TestWallets.Create(storage, Coins, Lives).Add("lives", 1);

            var restarted = TestWallets.Create(storage, Coins, Lives);

            Assert.That(restarted.GetBalance("lives"), Is.EqualTo(4));
        }

        [Test]
        public void Restart_NewResourceInConfig_GetsStartingBalance()
        {
            // Arrange: version 1 of the game only had coins.
            TestWallets.Create(storage, Coins).TrySpend("coins", 30);

            // Act: version 2 adds lives.
            var updated = TestWallets.Create(storage, Coins, Lives);

            // Assert
            Assert.That(updated.GetBalance("coins"), Is.EqualTo(70));
            Assert.That(updated.GetBalance("lives"), Is.EqualTo(3));
        }

        [Test]
        public void Restart_MaxLowered_ClampsSavedBalanceAndSavesIt()
        {
            // Arrange: lives reaches 5 under the old max of 5.
            TestWallets.Create(storage, Coins, Lives).Add("lives", 2);
            var lowerMax = ResourceDefinition.Create("lives", startingBalance: 3, maxBalance: 4);

            // Act: an update lowers the max to 4.
            var updated = TestWallets.Create(storage, Coins, lowerMax);

            // Assert: clamped now, and the clamped value is what the next launch reads.
            Assert.That(updated.GetBalance("lives"), Is.EqualTo(4));
            Assert.That(TestWallets.Create(storage, Coins, Lives).GetBalance("lives"), Is.EqualTo(4));
        }

        [Test]
        public void Restart_ResourceRemovedFromConfig_KeepsItsSavedBalance()
        {
            TestWallets.Create(storage, Coins, Lives).Add("lives", 1);

            // An update drops lives, then a later one brings it back.
            var withoutLives = TestWallets.Create(storage, Coins);
            var livesBack = TestWallets.Create(storage, Coins, Lives);

            Assert.That(withoutLives.HasResource("lives"), Is.False);
            Assert.That(livesBack.GetBalance("lives"), Is.EqualTo(4));
        }

        [Test]
        public void Restart_CorruptSave_FallsBackToStartingBalances()
        {
            storage.Save(TestWallets.ProgressKey, "{ this is not json");
            LogAssert.Expect(LogType.Warning, new Regex("could not be read"));

            var wallet = TestWallets.Create(storage, Coins, Lives);

            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
            Assert.That(wallet.GetBalance("lives"), Is.EqualTo(3));
        }

        [Test]
        public void DifferentPlayers_DoNotShareProgress()
        {
            var resources = new[] { Coins, Lives };
            new ResourceWallet(resources, new ProgressStore(storage, "player-1")).TrySpend("coins", 30);

            var otherPlayer = new ResourceWallet(resources, new ProgressStore(storage, "player-2"));

            Assert.That(otherPlayer.GetBalance("coins"), Is.EqualTo(100));
        }

        [Test]
        public void Constructor_FirstLaunch_SavesStartingBalancesOnce()
        {
            TestWallets.Create(storage, Coins, Lives);

            Assert.That(storage.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_SaveAlreadyMatchesConfig_DoesNotSave()
        {
            TestWallets.Create(storage, Coins, Lives);
            storage.SaveCount = 0;

            TestWallets.Create(storage, Coins, Lives);

            Assert.That(storage.SaveCount, Is.EqualTo(0));
        }

        [Test]
        public void TrySpend_NotEnoughBalance_DoesNotSave()
        {
            var wallet = TestWallets.Create(storage, Coins, Lives);
            storage.SaveCount = 0;

            wallet.TrySpend("coins", 101);

            Assert.That(storage.SaveCount, Is.EqualTo(0));
        }

        [Test]
        public void Add_AlreadyAtMax_DoesNotSave()
        {
            var wallet = TestWallets.Create(storage, Coins, Lives);
            wallet.Add("lives", 2);
            storage.SaveCount = 0;

            wallet.Add("lives", 1);

            Assert.That(storage.SaveCount, Is.EqualTo(0));
        }

        [Test]
        public void TrySpend_Succeeds_IsSavedBeforeBalanceChangedIsRaised()
        {
            var wallet = TestWallets.Create(storage, Coins, Lives);
            var balanceOnDiskSeenByListener = -1;
            wallet.BalanceChanged += (_, __) =>
                balanceOnDiskSeenByListener = TestWallets.Create(storage, Coins, Lives).GetBalance("coins");

            wallet.TrySpend("coins", 30);

            Assert.That(balanceOnDiskSeenByListener, Is.EqualTo(70));
        }

        // InMemoryStorage that counts writes, to check when the wallet saves.
        private class CountingStorage : IStorage
        {
            private readonly InMemoryStorage inner = new InMemoryStorage();

            public int SaveCount { get; set; }

            public string Load(string key) => inner.Load(key);

            public void Save(string key, string value)
            {
                SaveCount++;
                inner.Save(key, value);
            }
        }
    }
}
