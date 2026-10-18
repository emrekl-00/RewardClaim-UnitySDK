using System;
using NUnit.Framework;

namespace RewardClaims.Tests
{
    public class ResourceWalletTests
    {
        private ResourceWallet wallet;

        // coins: no max. lives: capped at 5, the shape Coin Catcher will use.
        [SetUp]
        public void SetUp()
        {
            wallet = TestWallets.Create(
                ResourceDefinition.Create("coins", startingBalance: 100),
                ResourceDefinition.Create("lives", startingBalance: 3, maxBalance: 5));
        }

        [Test]
        public void Constructor_NewWallet_StartsAtStartingBalances()
        {
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
            Assert.That(wallet.GetBalance("lives"), Is.EqualTo(3));
        }

        [Test]
        public void Constructor_DuplicateIds_Throws()
        {
            var resources = new[]
            {
                ResourceDefinition.Create("coins", startingBalance: 100),
                ResourceDefinition.Create("coins", startingBalance: 50),
            };

            Assert.Throws<ArgumentException>(() => TestWallets.Create(resources));
        }

        [Test]
        public void HasResource_KnownAndUnknownIds_ReturnsTrueOnlyForKnown()
        {
            Assert.That(wallet.HasResource("coins"), Is.True);
            Assert.That(wallet.HasResource("gems"), Is.False);
            Assert.That(wallet.HasResource(null), Is.False);
        }

        [TestCase(1, true)]
        [TestCase(100, true)]
        [TestCase(101, false)]
        public void CanSpend_ComparedToBalance_ReturnsExpected(int amount, bool expected)
        {
            Assert.That(wallet.CanSpend("coins", amount), Is.EqualTo(expected));
        }

        [Test]
        public void TrySpend_EnoughBalance_ReturnsTrueAndDeducts()
        {
            // Arrange: SetUp gives coins = 100.
            // Act
            var spent = wallet.TrySpend("coins", 30);

            // Assert
            Assert.That(spent, Is.True);
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(70));
        }

        [Test]
        public void TrySpend_NotEnoughBalance_ReturnsFalseAndKeepsBalance()
        {
            var spent = wallet.TrySpend("coins", 101);

            Assert.That(spent, Is.False);
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
        }

        [Test]
        public void TrySpend_ExactBalance_LeavesZero()
        {
            var spent = wallet.TrySpend("lives", 3);

            Assert.That(spent, Is.True);
            Assert.That(wallet.GetBalance("lives"), Is.EqualTo(0));
        }

        [Test]
        public void Add_WithoutMax_AddsFullAmount()
        {
            var added = wallet.Add("coins", 250);

            Assert.That(added, Is.EqualTo(250));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(350));
        }

        [Test]
        public void Add_BelowMax_AddsFullAmount()
        {
            var added = wallet.Add("lives", 2);

            Assert.That(added, Is.EqualTo(2));
            Assert.That(wallet.GetBalance("lives"), Is.EqualTo(5));
        }

        [Test]
        public void Add_BeyondMax_ClampsAndReturnsAmountAdded()
        {
            // Arrange: lives is 3 of max 5, so only 2 fit.
            // Act
            var added = wallet.Add("lives", 10);

            // Assert
            Assert.That(added, Is.EqualTo(2));
            Assert.That(wallet.GetBalance("lives"), Is.EqualTo(5));
        }

        [Test]
        public void Add_AlreadyAtMax_ReturnsZero()
        {
            wallet.Add("lives", 2);

            var added = wallet.Add("lives", 1);

            Assert.That(added, Is.EqualTo(0));
            Assert.That(wallet.GetBalance("lives"), Is.EqualTo(5));
        }

        [Test]
        public void Add_WithoutMaxNearIntMax_ClampsInsteadOfOverflowing()
        {
            var added = wallet.Add("coins", int.MaxValue);

            Assert.That(added, Is.EqualTo(int.MaxValue - 100));
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void AllMethods_UnknownId_Throw()
        {
            Assert.Throws<ArgumentException>(() => wallet.GetBalance("gems"));
            Assert.Throws<ArgumentException>(() => wallet.CanSpend("gems", 1));
            Assert.Throws<ArgumentException>(() => wallet.TrySpend("gems", 1));
            Assert.Throws<ArgumentException>(() => wallet.Add("gems", 1));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void AmountMethods_AmountNotPositive_Throw(int amount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.CanSpend("coins", amount));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend("coins", amount));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add("coins", amount));
        }

        [Test]
        public void TrySpend_InvalidAmount_KeepsBalance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend("coins", -10));

            // A negative spend must never act as a hidden "add".
            Assert.That(wallet.GetBalance("coins"), Is.EqualTo(100));
        }
    }
}
