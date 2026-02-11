using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.Overlay
{
    class ObjectField : UnityEditor.UIElements.ObjectField
    {
        public ObjectField()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.2d.enhancers/Editor/Overlays/AIOverlay/ObjectField/ObjectField.uss"));
            objectType = typeof(Texture2D);
        }
    }
}