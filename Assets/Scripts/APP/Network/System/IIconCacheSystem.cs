using QFramework;
using UnityEngine;

namespace APP.Network.System
{
    public interface IIconCacheSystem : ISystem
    {
        bool HasIconFor(string bundleId);
        Texture2D GetTexture(string bundleId);
        void StoreFromBase64(string bundleId, string base64);
        string EncodeBase64FromPngBytes(byte[] pngBytes);
    }
}
