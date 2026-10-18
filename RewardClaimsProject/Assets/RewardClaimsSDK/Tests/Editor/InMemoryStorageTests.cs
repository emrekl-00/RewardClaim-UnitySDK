using System;
using NUnit.Framework;

namespace RewardClaims.Tests
{
    public class InMemoryStorageTests
    {
        private InMemoryStorage storage;

        [SetUp]
        public void SetUp()
        {
            storage = new InMemoryStorage();
        }

        [Test]
        public void Load_MissingKey_ReturnsNull()
        {
            Assert.That(storage.Load("progress"), Is.Null);
        }

        [Test]
        public void Load_AfterSave_ReturnsSavedValue()
        {
            storage.Save("progress", "{\"coins\":100}");

            Assert.That(storage.Load("progress"), Is.EqualTo("{\"coins\":100}"));
        }

        [Test]
        public void Save_ExistingKey_OverwritesValue()
        {
            storage.Save("progress", "old");
            storage.Save("progress", "new");

            Assert.That(storage.Load("progress"), Is.EqualTo("new"));
        }

        [Test]
        public void Save_EmptyString_IsKeptAndNotTreatedAsMissing()
        {
            storage.Save("progress", "");

            Assert.That(storage.Load("progress"), Is.EqualTo(""));
        }

        [Test]
        public void Save_DifferentKeys_AreIndependent()
        {
            storage.Save("player-1", "a");
            storage.Save("player-2", "b");

            Assert.That(storage.Load("player-1"), Is.EqualTo("a"));
            Assert.That(storage.Load("player-2"), Is.EqualTo("b"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Load_InvalidKey_Throws(string key)
        {
            Assert.Throws<ArgumentException>(() => storage.Load(key));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Save_InvalidKey_Throws(string key)
        {
            Assert.Throws<ArgumentException>(() => storage.Save(key, "value"));
        }

        [Test]
        public void Save_NullValue_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => storage.Save("progress", null));
        }
    }
}
