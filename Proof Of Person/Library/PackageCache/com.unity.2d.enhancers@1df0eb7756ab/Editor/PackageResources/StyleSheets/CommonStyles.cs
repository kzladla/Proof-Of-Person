using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor
{
    internal static class CommonStyles
    {
        public const string shortLabelClass = "short-label";

        public static StyleSheet GetCommonStyleSheet()
        {
            return Utilities.LoadPackageResource<StyleSheet>("StyleSheets/CommonStyles.uss");
        }
    }
}
