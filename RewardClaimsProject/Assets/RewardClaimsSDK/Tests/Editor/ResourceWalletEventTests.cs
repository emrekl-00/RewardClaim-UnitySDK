using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace RewardClaims.Tests
{
    public class ResourceWalletEventTests
    {
        private ResourceWallet wallet;
        private List<(string Id, int Balance)> received;

        // Same wallet as ResourceWalletTests: coins 100 with no max, lives 3 of max 5.
        [SetUp]
        public void SetUp()
        {
            wallet = new ResourceWallet(new[]
            {
                ResourceDefinition.Create("coins", startingBalance: 100),
                ResourceDefinition.Create("lives", startingBalance: 3, maxBalance: 5),
            });

            received = new List<(string Id, int Balance)>();
            wallet.BalanceChanged += Record;
        }

        private void Record(string resourceId, int newBalance)
        {
            received.Add((resourceId, newBalance));
        }

        [Test]
        public void TrySpend_Succeeds_RaisesWithIdAndNewBalance()
        {
            wallet.TrySpend("coins", 30);

            Assert.That(received, Is.EqualTo(new[] { ("coins", 70) }));
        }

        [Test]
        public void Add_Succeeds_RaisesWithNewBalance()
        {
            wallet.Add("coins", 25);

            Assert.That(received, Is.EqualTo(new[] { ("coins", 125) }));
        }

        [Test]
        public void Add_PartlyClamped_RaisesWithClampedBalance()
        {
            // Arrange: lives is 3 of max 5, so only 2 of the 10 fit.
            // Act
            wallet.Add("lives", 10);

            // Assert
            Assert.That(received, Is.EqualTo(new[] { ("lives", 5) }));
        }

        [Test]
        public void BalanceChanged_ListenerReadsBalance_SeesNewValue()
        {
            var balanceSeenByListener = -1;
            wallet.BalanceChanged += (id, _) => balanceSeenByListener = wallet.GetBalance(id);

            wallet.TrySpend("coins", 40);

            Assert.That(balanceSeenByListener, Is.EqualTo(60));
        }

        [Test]
        public void TrySpend_NotEnoughBalance_DoesNotRaise()
        {
            wallet.TrySpend("coins", 101);

            Assert.That(received, Is.Empty);
        }

        [Test]
        public void Add_AlreadyAtMax_DoesNotRaise()
        {
            wallet.Add("lives", 2);
            received.Clear();

            wallet.Add("lives", 1);

            Assert.That(received, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void AmountMethods_AmountNotPositive_DoNotRaise(int amount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend("coins", amount));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add("coins", amount));

            Assert.That(received, Is.Empty);
        }

        [Test]
        public void AmountMethods_UnknownId_DoNotRaise()
        {
            Assert.Throws<ArgumentException>(() => wallet.TrySpend("gems", 1));
            Assert.Throws<ArgumentException>(() => wallet.Add("gems", 1));

            Assert.That(received, Is.Empty);
        }

        [Test]
        public void BalanceChanged_AfterUnsubscribe_StopsReceiving()
        {
            wallet.TrySpend("coins", 10);

            // What a UI object does in OnDestroy, so the wallet stops calling into it.
            wallet.BalanceChanged -= Record;
            wallet.TrySpend("coins", 10);

            Assert.That(received, Is.EqualTo(new[] { ("coins", 90) }));
        }
    }
}
