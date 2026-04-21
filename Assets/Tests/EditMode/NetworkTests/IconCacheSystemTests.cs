using APP.Network.System;
using NUnit.Framework;

namespace APP.Network.Tests
{
    [TestFixture]
    public sealed class IconCacheSystemTests
    {
        private IconCacheSystem _sys;

        [SetUp]
        public void SetUp()
        {
            _sys = new IconCacheSystem(maxEntries: 3);
        }

        private static string OnePixelPngBase64()
        {
            return "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwAB/1EF5YwAAAAASUVORK5CYII=";
        }

        [Test]
        public void StoreAndFetch_RoundTripsTexture()
        {
            _sys.StoreFromBase64("bundle.x", OnePixelPngBase64());
            Assert.That(_sys.HasIconFor("bundle.x"), Is.True);
            Assert.That(_sys.GetTexture("bundle.x"), Is.Not.Null);
        }

        [Test]
        public void LRU_EvictsOldestWhenOverCap()
        {
            _sys.StoreFromBase64("a", OnePixelPngBase64());
            _sys.StoreFromBase64("b", OnePixelPngBase64());
            _sys.StoreFromBase64("c", OnePixelPngBase64());
            _sys.StoreFromBase64("d", OnePixelPngBase64());
            Assert.That(_sys.HasIconFor("a"), Is.False);
            Assert.That(_sys.HasIconFor("d"), Is.True);
        }

        [Test]
        public void EncodeBase64FromPngBytes_RoundTrips()
        {
            byte[] bytes = System.Convert.FromBase64String(OnePixelPngBase64());
            string roundtripped = _sys.EncodeBase64FromPngBytes(bytes);
            Assert.That(roundtripped, Is.EqualTo(OnePixelPngBase64()));
        }
    }
}
