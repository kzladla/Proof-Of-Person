using Unity.AI.Image.Interfaces;

namespace Unity.U2D.AI.Editor.AIBridge
{
    interface IAIAssetBridge
    {
        string GetGeneratedAssetsPath(string assetGUID);
        void OpenGeneratorWindow(string createdAssetPath);
        string GetUnityAIGeneratedLabel();
    }

    internal class AIAssetBridge : IAIAssetBridge
    {
        public string GetGeneratedAssetsPath(string assetGUID)
        {
            return SpriteEditor.GetGeneratedAssetsPath(assetGUID);
        }

        public void OpenGeneratorWindow(string createdAssetPath)
        {
            SpriteEditor.OpenGeneratorWindow(createdAssetPath);
        }

        public string GetUnityAIGeneratedLabel()
        {
            return SpriteEditor.UnityAIGeneratedLabel;
        }
    }

    internal static class AssetHelpers
    {
        static IAIAssetBridge s_Bridge;

        static IAIAssetBridge instance
        {
            get
            {
                if (GetBridge() == null)
                    SetBridge(new AIAssetBridge());
                return s_Bridge;
            }
        }

        public static void SetBridge(IAIAssetBridge bridge)
        {
            s_Bridge = bridge;
        }

        public static IAIAssetBridge GetBridge()
        {
            return s_Bridge;
        }

        public static string GetGeneratedAssetsPath(string assetGUID)
        {
            return instance.GetGeneratedAssetsPath(assetGUID);
        }

        public static void OpenGeneratorWindow(string createdAssetPath)
        {
            instance.OpenGeneratorWindow(createdAssetPath);
        }

        public static string GetUnityAIGeneratedLabel()
        {
            return instance.GetUnityAIGeneratedLabel();
        }
    }
}
