using System;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Editor.Backend.Socket.Tools;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Generators.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.UI.Editor
{
    abstract class AssetFunctionCallElementBase : DefaultFunctionCallRenderer
    {
        public AssetFunctionCallElementBase()
        {
            var scrollView = parent;
            if (scrollView is ScrollView)
            {
                // we manage scrolling ourselves
                var newContentParent = new VisualElement();
                newContentParent.AddToClassList("function-call-content-scroll");
                scrollView.parent.Insert(scrollView.parent.IndexOf(scrollView), newContentParent);
                newContentParent.Add(this);
                scrollView.RemoveFromHierarchy();
            }
        }

        protected abstract VisualElement CreatePreviewElement(Object assetObject);
        
        public override void OnCallSuccess(string functionId, Guid callId, IFunctionCaller.CallResult result)
        {
            var typedResult = result.GetTypedResult<AssetOutputBase>();

            if (string.IsNullOrEmpty(typedResult.AssetGuid) && string.IsNullOrEmpty(typedResult.AssetPath))
            {
                Add(FunctionCallUtils.CreateContentLabel("Asset generation failed. No GUID or asset path returned."));
                return;
            }

            Object assetObject = null;
            var assetPath = string.Empty;

            if (!string.IsNullOrEmpty(typedResult.AssetGuid))
            {
                assetPath = AssetDatabase.GUIDToAssetPath(typedResult.AssetGuid);
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = typedResult.AssetPath;
            }

            if (!string.IsNullOrEmpty(assetPath))
            {
                assetObject = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            }

            if (assetObject == null)
            {
                Add(FunctionCallUtils.CreateContentLabel("Asset generation failed. Could not find asset from GUID or asset path."));
                return;
            }

            var preview = CreatePreviewElement(assetObject);
            Add(preview);
            Expanded = true;
        }
    }

    [FunctionCallRenderer(ConvertAssetTool.k_ConvertToMaterialFunctionId)]
    class ConvertMaterialFunctionCallElement : GenerateAssetFunctionCallElement { }

    [FunctionCallRenderer(ConvertAssetTool.k_ConvertToTerrainLayerFunctionId)]
    class ConvertTerrainLayerFunctionCallElement : GenerateAssetFunctionCallElement { }

    [FunctionCallRenderer(EditAudioClipTool.k_FunctionId)]
    class TrimAudioFunctionCallElement : GenerateAssetFunctionCallElement { }

    [FunctionCallRenderer(EditAnimationClipTool.k_FunctionId)]
    class TrimAnimationFunctionCallElement : GenerateAssetFunctionCallElement { }

    [FunctionCallRenderer(GenerateAssetTool.k_FunctionId)]
    class GenerateAssetFunctionCallElement : AssetFunctionCallElementBase
    {
        protected override VisualElement CreatePreviewElement(Object assetObject)
            => AssetPreviewFactory.CreatePreview(assetObject);
    }
}
