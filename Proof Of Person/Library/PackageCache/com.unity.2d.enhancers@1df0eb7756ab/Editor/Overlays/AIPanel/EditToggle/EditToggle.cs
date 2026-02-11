using Unity.U2D.AI.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.Overlay
{
    class EditToggle : Toggle
    {
        public EditToggle()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.2d.enhancers/Editor/Overlays/AIOverlay/EditToggle/EditToggle.uss"));
            if (EditorGUIUtility.isProSkin)
                styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.2d.enhancers/Editor/Overlays/AIOverlay/EditToggle/dark.uss"));
            else
                styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.2d.enhancers/Editor/Overlays/AIOverlay/EditToggle/light.uss"));
        }
    }
}