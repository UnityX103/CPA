using UnityEngine;
using QFramework;

namespace APP.Utility
{
    public sealed class PlayerPrefsStorageUtility : IStorageUtility
    {
        public string LoadString(string key, string fallback = "") => PlayerPrefs.GetString(key, fallback ?? string.Empty);
        public void SaveString(string key, string value) => PlayerPrefs.SetString(key, value ?? string.Empty);
        public int LoadInt(string key, int fallback = 0) => PlayerPrefs.GetInt(key, fallback);
        public void SaveInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Flush() => PlayerPrefs.Save();
    }
}
