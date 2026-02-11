using UnityEditor;
using UnityEngine;

namespace Unity.U2D.AI.Editor.Prefs
{
    internal interface IModulePreferencesProvider
    {
        void SetBool(string key, bool value);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
        void SetString(string key, string value);

        bool GetBool(string key);
        float GetFloat(string key);
        int GetInt(string key);
        string GetString(string key);

        bool GetBool(string key, bool defaultValue);
        float GetFloat(string key, float defaultValue);
        int GetInt(string key, int defaultValue);
        string GetString(string key, string defaultValue);

        bool HasKey(string key);
    }

    internal class DefaultEditorPrefs : IModulePreferencesProvider
    {
        public void SetBool(string key, bool value)
        {
            EditorPrefs.SetBool(key, value);
        }

        public void SetFloat(string key, float value)
        {
            EditorPrefs.SetFloat(key, value);
        }

        public void SetInt(string key, int value)
        {
            EditorPrefs.SetInt(key, value);
        }

        public void SetString(string key, string value)
        {
            EditorPrefs.SetString(key, value);
        }

        public bool GetBool(string key)
        {
            return EditorPrefs.GetBool(key);
        }

        public float GetFloat(string key)
        {
            return EditorPrefs.GetFloat(key);
        }

        public int GetInt(string key)
        {
            return EditorPrefs.GetInt(key);
        }

        public string GetString(string key)
        {
            return EditorPrefs.GetString(key);
        }

        public bool GetBool(string key, bool defaultValue)
        {
            return EditorPrefs.GetBool(key, defaultValue);
        }

        public float GetFloat(string key, float defaultValue)
        {
            return EditorPrefs.GetFloat(key, defaultValue);
        }

        public int GetInt(string key, int defaultValue)
        {
            return EditorPrefs.GetInt(key, defaultValue);
        }

        public string GetString(string key, string defaultValue)
        {
            return EditorPrefs.GetString(key, defaultValue);
        }

        public bool HasKey(string key)
        {
            return EditorPrefs.HasKey(key);
        }
    }

    internal static class ModulePreferences
    {
        internal static readonly string ShowSpriteKey = $"{nameof(ModulePreferences)}.ShowSprite";
        internal static readonly string ShowSpriteOpacityKey = $"{nameof(ModulePreferences)}.ShowSpriteOpacity";

        static IModulePreferencesProvider s_Provider;

        static IModulePreferencesProvider provider
        {
            get
            {
                if (s_Provider == null)
                    SetProvider(new DefaultEditorPrefs());
                return s_Provider;
            }
        }

        public static void SetProvider(IModulePreferencesProvider newProvider)
        {
            s_Provider = newProvider;
        }

        public static IModulePreferencesProvider GetProvider()
        {
            return provider;
        }

        public static bool ShowSprite
        {
            get => provider.GetBool(ShowSpriteKey, true);
            set => provider.SetBool(ShowSpriteKey, value);
        }

        public static float ShowSpriteOpacity
        {
            get => provider.GetFloat(ShowSpriteOpacityKey, 1.0f);
            set => provider.SetFloat(ShowSpriteOpacityKey, value);
        }

        public static void SetBrushSize(string key, float size, float minSize, float maxSize)
        {
            provider.SetFloat($"{key}.{nameof(size)}", size);
            provider.SetFloat($"{key}.{nameof(minSize)}", minSize);
            provider.SetFloat($"{key}.{nameof(maxSize)}", maxSize);
        }

        public static void SetBrushColor(string key, Color color)
        {
            var colorString = "#" + ColorUtility.ToHtmlStringRGBA(color);
            provider.SetString($"{key}.{nameof(color)}", colorString);
        }

        public static bool GetBrushSize(string key, out float size, out float minSize, out float maxSize)
        {
            var sizeKey = $"{key}.{nameof(size)}";
            var minSizeKey = $"{key}.{nameof(minSize)}";
            var maxSizeKey = $"{key}.{nameof(maxSize)}";

            if (!provider.HasKey(sizeKey) || !provider.HasKey(minSizeKey) || !provider.HasKey(maxSizeKey))
            {
                size = minSize = maxSize = 0.0f;
                return false;
            }

            size = provider.GetFloat(sizeKey);
            minSize = provider.GetFloat(minSizeKey);
            maxSize = provider.GetFloat(maxSizeKey);

            return true;
        }

        public static bool GetBrushColor(string key, out Color color)
        {
            var colorKey = $"{key}.{nameof(color)}";
            if (!provider.HasKey(colorKey))
            {
                color = Color.white;
                return false;
            }

            if(!ColorUtility.TryParseHtmlString(provider.GetString(colorKey), out color))
            {
                color = Color.white;
                return false;
            }

            return true;
        }
    }
}
