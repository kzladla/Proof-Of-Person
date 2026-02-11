using System.Collections.Generic;
using Unity.U2D.AI.Editor.AIBridge;
using UnityEditor;
using UnityEngine;

namespace Unity.U2D.AI.Editor.AssetPostProcessor
{
    class TagAssetPostProcessor : AssetPostprocessor
    {
        static List<string> s_AssetToTag = new();

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            for (var i = 0; i < s_AssetToTag.Count; ++i)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(s_AssetToTag[i]);
                for (var j = 0; j < assets.Length; ++j)
                    EnableGenerationLabel(assets[j]);
            }

            s_AssetToTag.Clear();
        }

        public static void AddAssetPathToTag(string path)
        {
            s_AssetToTag.Add(path);
        }

        static void EnableGenerationLabel(Object asset)
        {
            var labelList = new List<string>(AssetDatabase.GetLabels(asset));
            var legalLabel = AssetHelpers.GetUnityAIGeneratedLabel();
            if (!labelList.Contains(legalLabel))
            {
                labelList.Add(legalLabel);
                AssetDatabase.SetLabels(asset, labelList.ToArray());
            }
        }
    }
}
