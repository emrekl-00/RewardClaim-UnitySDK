using System;
using NUnit.Framework;
using UnityEngine;

namespace RewardClaims.Tests
{
    // These touch the real Editor PlayerPrefs on this machine, so every test uses a fresh,
    // clearly named key and deletes it afterwards to leave the developer's prefs untouched.
    public class PlayerPrefsStorageTests
    {
        private PlayerPrefsStorage storage;
        private string key;

        [SetUp]
        public void SetUp()
        {
            storage = new PlayerPrefsStorage();
            key = "RewardClaims.Tests." + Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        [Test]
        public void Load_MissingKey_ReturnsNull()
        {
            Assert.That(storage.Load(key), Is.Null);
        }

        [Test]
        public void Load_AfterSave_ReturnsSavedValue()
        {
            storage.Save(key, "{\"coins\":100}");

            Assert.That(storage.Load(key), Is.EqualTo("{\"coins\":100}"));
        }

        [Test]
        public void Save_ExistingKey_OverwritesValue()
        {
            storage.Save(key, "old");
            storage.Save(key, "new");

            Assert.That(storage.Load(key), Is.EqualTo("new"));
        }
    }
}
