using System;
using System.IO;
using Unity.U2D.AI.Editor.AIBridge;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace Unity.U2D.AI.Editor
{
    static class Utilities
    {
        const string k_PackageResourcePath = "Packages/com.unity.2d.enhancers/Editor/PackageResources";

        public static byte[] GetBytesFromTexture2D(Texture2D texture)
        {
            if (texture == null)
                return null;
            byte[] result = null;
            var t = GetReadableTexture(texture);
            result = t.EncodeToPNG();
            if (t != texture)
                t.SafeDestroy();

            return result;
        }

        public static Texture2D CombineTexture(Texture2D[] textures, int width, int height, TextureFormat format = TextureFormat.RGBA32)
        {
            if (textures == null || textures.Length == 0)
                return null;
            var active = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.2d.enhancers/Editor/PackageResources/material/CombineTexture.mat");
            for (var i = 0; i < textures.Length; i++)
            {
                Graphics.Blit(textures[i], temporary, material);
            }
            RenderTexture.active = temporary;
            var combined = new Texture2D(width, height, format, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "2DMuse-CombinedTexture"
            };
            combined.ReadPixels(new Rect(0.0f, 0.0f, (float)width, (float)height), 0, 0);
            combined.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(temporary);
            return combined;
        }

        public static Texture2D CreateTemporaryDuplicate(Texture2D original, string name, int width, int height, TextureFormat format = TextureFormat.RGBA32)
        {
            //if (!ShaderUtil.hardwareSupportsRectRenderTexture || !(bool)(UnityEngine.Object)original)
            if (original == null)
                return null;
            var active = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(original, temporary);
            RenderTexture.active = temporary;
            var flag = width >= SystemInfo.maxTextureSize || height >= SystemInfo.maxTextureSize;
            var temporaryDuplicate = new Texture2D(width, height, format, original.mipmapCount > 1 || flag)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = name ?? $"2DMuse-TemporaryDuplicateFor-{original.name}"
            };
            temporaryDuplicate.ReadPixels(new Rect(0.0f, 0.0f, (float)width, (float)height), 0, 0);
            temporaryDuplicate.Apply();
#if UNITY_EDITOR
            temporaryDuplicate.alphaIsTransparency = original.alphaIsTransparency;
#endif
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(temporary);
            return temporaryDuplicate;
        }

        public static T LoadPackageResource<T>(string path) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            var combinePath = Path.Combine(k_PackageResourcePath, path);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(combinePath);
#else
            return null;
#endif
        }

        internal static Texture2D LoadIcon(string path, string name)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorGUIUtility.isProSkin ? LoadIconDark(path, name) : LoadIconLight(path, name);
#else
            return null;
#endif
        }

        internal static Texture2D LoadIconLight(string path, string name)
        {
#if UNITY_EDITOR
            var combinePath = Path.Combine(path, name);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(combinePath);
#else
            return null;
#endif
        }

        internal static Texture2D LoadIconDark(string path, string name)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorGUIUtility.isProSkin)
                name = $"d_{name}";
            var combinePath = Path.Combine(path, name);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(combinePath);
#else
            return null;
#endif
        }

        public static Texture2D GetReadableTexture(Texture2D texture)
        {
            if (texture.isReadable && !GraphicsFormatUtility.IsCompressedFormat(texture.format))
                return texture;
            var t = CreateTemporaryDuplicate(texture, $"{texture.name} readable", texture.width, texture.height);
            return t;
        }

        public static bool TryGetUriFromAsset(Object asset, out Uri uri)
        {
            uri = null;
            if (asset == null)
                return false;

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            var absolutePath = Path.GetFullPath(assetPath);
            uri = new Uri(absolutePath);
            return true;
        }

        public static bool TryGetUriFromGeneratedAsset(Object asset, out Uri uri)
        {
            uri = null;
            if (asset == null)
                return false;

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            var assetGUID = AssetDatabase.AssetPathToGUID(assetPath);
            var absolutePath = Path.GetFullPath(Path.Combine(AssetHelpers.GetGeneratedAssetsPath(assetGUID), Path.GetFileName(assetPath)));
            uri = new Uri(absolutePath);
            return true;
        }
    }
}
