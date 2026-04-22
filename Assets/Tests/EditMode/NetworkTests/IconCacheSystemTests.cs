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
        public void StoreFromBase64_MakesBundleAvailable()
        {
            _sys.StoreFromBase64("bundle.x", OnePixelPngBase64());
            Assert.That(_sys.HasIconFor("bundle.x"), Is.True);
            // 注：GetTexture 的 Texture2D.LoadImage 在 EditMode 里不可用，
            //    贴图落地由 PlayMode E2E 测试 Case 3 覆盖。
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
            byte[] bytes = global::System.Convert.FromBase64String(OnePixelPngBase64());
            string roundtripped = _sys.EncodeBase64FromPngBytes(bytes);
            Assert.That(roundtripped, Is.EqualTo(OnePixelPngBase64()));
        }
    }
}
