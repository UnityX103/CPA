using QFramework;

namespace APP.Utility
{
    public interface IStorageUtility : IUtility
    {
        string LoadString(string key, string fallback = "");
        void SaveString(string key, string value);
        int LoadInt(string key, int fallback = 0);
        void SaveInt(string key, int value);
        void DeleteKey(string key);
        void Flush();
    }
}
