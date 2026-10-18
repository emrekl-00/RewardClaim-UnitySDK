using NUnit.Framework;
using UnityEngine;

namespace RewardClaims.Tests
{
    public class RewardClaimsConfigTests
    {
        private RewardClaimsConfig config;

        // Every test starts from a valid config and breaks exactly one thing.
        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<RewardClaimsConfig>();
            config.Resources = new[]
            {
                ResourceDefinition.Create("coins", startingBalance: 100),
                ResourceDefinition.Create("lives", startingBalance: 3, maxBalance: 5),
            };
        }

        // CreateInstance objects aren't garbage collected like plain C# objects; destroy them explicitly.
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void NewConfig_HasDefaultValues()
        {
            Assert.That(config.PlayerId, Is.EqualTo("test-player"));
            Assert.That(config.ApiUrl, Is.EqualTo("https://httpbin.org/anything"));
            Assert.That(config.RequestTimeoutSeconds, Is.EqualTo(10));
        }

        [Test]
        public void Validate_ValidConfig_ReturnsNoErrors()
        {
            Assert.That(config.Validate(), Is.Empty);
        }

        [Test]
        public void Validate_NoResources_ReturnsNoErrors()
        {
            config.Resources = new ResourceDefinition[0];

            Assert.That(config.Validate(), Is.Empty);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Validate_EmptyPlayerId_ReturnsError(string playerId)
        {
            config.PlayerId = playerId;

            AssertSingleError("Player id");
        }

        [Test]
        public void Validate_EmptyApiUrl_ReturnsError()
        {
            config.ApiUrl = "";

            AssertSingleError("API url");
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void Validate_TimeoutNotPositive_ReturnsError(int timeout)
        {
            config.RequestTimeoutSeconds = timeout;

            AssertSingleError("timeout");
        }

        [Test]
        public void Validate_EmptyResourceId_ReturnsErrorWithIndex()
        {
            config.Resources = new[]
            {
                ResourceDefinition.Create("coins", startingBalance: 100),
                ResourceDefinition.Create("", startingBalance: 0),
            };

            AssertSingleError("index 1");
        }

        [Test]
        public void Validate_DuplicateResourceId_ReturnsOneErrorPerId()
        {
            config.Resources = new[]
            {
                ResourceDefinition.Create("coins", startingBalance: 100),
                ResourceDefinition.Create("coins", startingBalance: 50),
                ResourceDefinition.Create("coins", startingBalance: 10),
            };

            AssertSingleError("'coins' is used more than once");
        }

        [Test]
        public void Validate_NegativeStartingBalance_ReturnsError()
        {
            config.Resources = new[] { ResourceDefinition.Create("coins", startingBalance: -1) };

            AssertSingleError("negative starting balance");
        }

        [Test]
        public void Validate_MaxBelowStartingBalance_ReturnsError()
        {
            config.Resources = new[] { ResourceDefinition.Create("lives", startingBalance: 5, maxBalance: 3) };

            AssertSingleError("lower than its starting balance");
        }

        [Test]
        public void Validate_MaxEqualToStartingBalance_ReturnsNoErrors()
        {
            config.Resources = new[] { ResourceDefinition.Create("lives", startingBalance: 5, maxBalance: 5) };

            Assert.That(config.Validate(), Is.Empty);
        }

        [Test]
        public void Validate_SeveralProblems_ReturnsAllErrors()
        {
            config.PlayerId = "";
            config.RequestTimeoutSeconds = 0;
            config.Resources = new[] { ResourceDefinition.Create("coins", startingBalance: -1) };

            Assert.That(config.Validate(), Has.Count.EqualTo(3));
        }

        private void AssertSingleError(string expectedFragment)
        {
            var errors = config.Validate();

            Assert.That(errors, Has.Count.EqualTo(1), string.Join("\n", errors));
            Assert.That(errors[0], Does.Contain(expectedFragment));
        }
    }
}
