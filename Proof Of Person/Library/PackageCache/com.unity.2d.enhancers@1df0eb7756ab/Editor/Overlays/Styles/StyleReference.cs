using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.Styles
{
    internal static class StyleReference
    {
        public const string spriteEditorOverlayUssClassName = "sprite-editor-overlay";
        public const string backgroundLightUssClassName = "bg-light";

        public static void AddSpriteEditorOverlayStyle(VisualElement element)
        {
            element.styleSheets.Add(spriteOverlayStyleSheet);
            element.AddToClassList(spriteEditorOverlayUssClassName);
            element.AddToClassList(backgroundLightUssClassName);
        }

        static StyleSheet s_SpriteOverlayStyleSheet;
        public static StyleSheet spriteOverlayStyleSheet
        {
            get
            {
                if (s_SpriteOverlayStyleSheet == null)
                    s_SpriteOverlayStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.2d.enhancers/Editor/Overlays/Styles/SpriteEditorOverlay.uss");

                return s_SpriteOverlayStyleSheet;
            }
        }
    }
}
