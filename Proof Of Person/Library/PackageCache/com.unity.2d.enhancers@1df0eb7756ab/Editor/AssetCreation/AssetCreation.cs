using System;
using System.Threading.Tasks;
using Unity.U2D.AI.Editor.AIBridge;
using UnityEditor;
using UnityEditor.U2D.Common;
using UnityEngine;

namespace Unity.U2D.AI.Editor.AssetCreation
{
    internal static class AssetCreation
    {
        public const string DefaultSpriteTexturePath = "Packages/com.unity.2d.enhancers/Editor/PackageResources/DefaultAssets/Textures/v1/New Sprite.png";

        [MenuItem("Assets/Create/2D/Sprites/Generate Sprite", false, -1000)]
        public static async void CreateEmptySprite()
        {
            try
            {
                TextureAssetCreationMonitor.createdAssetPath = null;
                TextureAssetCreationMonitor.expectingNewAsset = true;

                AssetCreationUtility.CreateAssetObjectFromTemplate<Texture2D>(DefaultSpriteTexturePath);
                while (TextureAssetCreationMonitor.expectingNewAsset)
                    await Task.Yield();

                var createdAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureAssetCreationMonitor.createdAssetPath);
                if (createdAsset == null)
                    return;

                AssetHelpers.OpenGeneratorWindow(TextureAssetCreationMonitor.createdAssetPath);
            }
            catch
            {
                //ignored
            }
            finally
            {
                TextureAssetCreationMonitor.expectingNewAsset = false;
            }
        }

        class TextureAssetCreationMonitor : AssetPostprocessor
        {
            public static bool expectingNewAsset = false;
            public static string createdAssetPath = null;

            static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                if (!expectingNewAsset)
                    return;
                foreach (var path in importedAssets)
                {
                    if (!path.EndsWith(".png"))
                        continue;
                    createdAssetPath = path;
                    expectingNewAsset = false;
                    break;
                }
            }
        }
    }
}
