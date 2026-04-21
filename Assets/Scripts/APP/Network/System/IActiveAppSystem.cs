using System;
using QFramework;

namespace APP.Network.System
{
    public readonly struct ActiveAppSnapshot
    {
        public readonly string Name;
        public readonly string BundleId;
        public readonly byte[] IconPngBytes;

        public ActiveAppSnapshot(string name, string bundleId, byte[] iconPngBytes)
        {
            Name = name ?? string.Empty;
            BundleId = bundleId ?? string.Empty;
            IconPngBytes = iconPngBytes;
        }

        public static ActiveAppSnapshot Empty => new ActiveAppSnapshot(string.Empty, string.Empty, null);
    }

    public interface IActiveAppSystem : ISystem
    {
        void Tick(float deltaTime);
        ActiveAppSnapshot Current { get; }
        event Action<ActiveAppSnapshot> Changed;
    }
}
