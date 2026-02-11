using Unity.U2D.AI.Editor.ImageManipulation;
using Unity.U2D.AI.Editor.Overlay;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor
{
    internal class AIModuleMainCanvas : VisualElement
    {
        internal const float widthOffset = 16f;
        internal const float heightOffset = 16f;

        public AIModuleMainCanvas(params VisualElement[] elements)
        {
            pickingMode = PickingMode.Ignore;

            style.position = Position.Absolute;
            style.top = style.left = 0;
            style.right = widthOffset;
            style.bottom = heightOffset;
            style.flexDirection = FlexDirection.Row;
            style.overflow = Overflow.Hidden;

            foreach (VisualElement element in elements)
                Add(element);
        }
    }
}
