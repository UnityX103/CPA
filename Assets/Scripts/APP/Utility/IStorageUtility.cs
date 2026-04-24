using QFramework;

namespace APP.Utility
{
    public interface IStorageUtility : IUtility
    {
        string LoadString(string key, string fallback = "");
        void SaveString(string key, string value);
        int LoadInt(string key, int fallback = 0);
        void SaveInt(string key, int value);
        float LoadFloat(string key, float fallback = 0f);
        void SaveFloat(string key, float value);
        void DeleteKey(string key);
        void Flush();
    }
}
