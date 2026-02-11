using Unity.U2D.AI.Editor.AIBridge;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.Overlay
{
    internal class ImageControl
    {
        public readonly EControlType controlType;
        public readonly VisualElement ctx;

        public ImageControl(EControlType controlType, VisualElement ctx)
        {
            this.controlType = controlType;
            this.ctx = ctx;
        }
    }
}
