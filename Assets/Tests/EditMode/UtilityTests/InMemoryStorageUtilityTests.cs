using APP.Utility;
using NUnit.Framework;

namespace APP.Utility.Tests
{
    [TestFixture]
    public sealed class InMemoryStorageUtilityTests
    {
        [Test]
        public void SaveString_ThenLoadString_ReturnsSavedValue()
        {
            var storage = new InMemoryStorageUtility();
            storage.SaveString("k", "v");
            Assert.That(storage.LoadString("k", "fallback"), Is.EqualTo("v"));
        }

        [Test]
        public void LoadString_WhenKeyMissing_ReturnsFallback()
        {
            var storage = new InMemoryStorageUtility();
            Assert.That(storage.LoadString("missing", "fb"), Is.EqualTo("fb"));
        }

        [Test]
        public void SaveInt_ThenLoadInt_ReturnsSavedValue()
        {
            var storage = new InMemoryStorageUtility();
            storage.SaveInt("n", 42);
            Assert.That(storage.LoadInt("n", 0), Is.EqualTo(42));
        }

        [Test]
        public void DeleteKey_RemovesValue()
        {
            var storage = new InMemoryStorageUtility();
            storage.SaveString("k", "v");
            storage.DeleteKey("k");
            Assert.That(storage.LoadString("k", "fb"), Is.EqualTo("fb"));
        }

        [Test]
        public void Clear_RemovesAllValues()
        {
            var storage = new InMemoryStorageUtility();
            storage.SaveString("k1", "v1");
            storage.SaveInt("k2", 2);
            storage.Clear();
            Assert.That(storage.LoadString("k1", "fb"), Is.EqualTo("fb"));
            Assert.That(storage.LoadInt("k2", 0), Is.EqualTo(0));
        }
    }
}
