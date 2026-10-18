using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RewardClaims.Tests
{
    public class ProgressStoreTests
    {
        private const string Key = "RewardClaims.Progress.player-1";

        private InMemoryStorage storage;

        [SetUp]
        public void SetUp()
        {
            storage = new InMemoryStorage();
        }

        [Test]
        public void Constructor_NothingSaved_StartsWithEmptyProgress()
        {
            var store = new ProgressStore(storage, "player-1");

            Assert.That(store.Data.balances, Is.Empty);
        }

        [Test]
        public void Save_WritesReadableJsonUnderPlayerKey()
        {
            var store = new ProgressStore(storage, "player-1");
            store.Data.balances.Add(new ResourceBalance { resourceId = "coins", balance = 70 });

            store.Save();

            // This is exactly what lands in PlayerPrefs: field names become JSON keys.
            Assert.That(storage.Load(Key), Is.EqualTo("{\"balances\":[{\"resourceId\":\"coins\",\"balance\":70}]}"));
        }

        [Test]
        public void Save_ThenNewStoreOnSameStorage_LoadsSameProgress()
        {
            var store = new ProgressStore(storage, "player-1");
            store.Data.balances.Add(new ResourceBalance { resourceId = "coins", balance = 70 });
            store.Data.balances.Add(new ResourceBalance { resourceId = "lives", balance = 2 });
            store.Save();

            var reloaded = new ProgressStore(storage, "player-1");

            Assert.That(reloaded.Data.balances.Count, Is.EqualTo(2));
            Assert.That(reloaded.Data.balances[0].resourceId, Is.EqualTo("coins"));
            Assert.That(reloaded.Data.balances[0].balance, Is.EqualTo(70));
            Assert.That(reloaded.Data.balances[1].resourceId, Is.EqualTo("lives"));
            Assert.That(reloaded.Data.balances[1].balance, Is.EqualTo(2));
        }

        // "not json" is garbage; the truncated one is what a write cut off half-way would leave.
        [TestCase("not json")]
        [TestCase("{\"balances\":[{\"resourceId\":\"coi")]
        [TestCase("")]
        public void Constructor_CorruptJson_LogsWarningAndStartsEmpty(string savedJson)
        {
            storage.Save(Key, savedJson);
            LogAssert.Expect(LogType.Warning, new Regex("could not be read"));

            var store = new ProgressStore(storage, "player-1");

            Assert.That(store.Data.balances, Is.Empty);
        }

        [Test]
        public void Constructor_JsonWithoutBalances_StartsEmpty()
        {
            storage.Save(Key, "{}");

            var store = new ProgressStore(storage, "player-1");

            Assert.That(store.Data.balances, Is.Not.Null.And.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        public void Constructor_InvalidPlayerId_Throws(string playerId)
        {
            Assert.Throws<ArgumentException>(() => new ProgressStore(storage, playerId));
        }

        [Test]
        public void Constructor_NullStorage_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgressStore(null, "player-1"));
        }
    }
}
