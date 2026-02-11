using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Events.UI;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    internal class InpaintMaskTool : EditorToolbarToggle, IToolToggle, IEventSender
    {
        public InpaintMaskTool()
        {
            icon = Utilities.LoadIcon("Packages/com.unity.2d.enhancers/Editor/PackageResources/icons","Mask@2x.png");
            onIcon = Utilities.LoadIcon("Packages/com.unity.2d.enhancers/Editor/PackageResources/icons","Mask On@2x.png");
            tooltip = "Mask";
            this.RegisterValueChangedCallback(OnToolActivte);
        }

        UIEventBus m_EventBus;

        void OnToolActivte(ChangeEvent<bool> evt)
        {
            if(!evt.newValue)
                SetValueWithoutNotify(true);
            else
            {
                m_EventBus.SendEvent(new ImageManipulationModeChangeEvent(this)
                {
                    mode = toolMode,
                    doodleMode = EDoodlePadMode.Overlay
                });
            }
        }

        public void RegisterEventBus(UIEventBus eventBus)
        {
            m_EventBus = eventBus;
        }

        public void SetToggleValue(bool newValue)
        {
            SetValueWithoutNotify(newValue);
        }

        public EImageManipulationMode toolMode => EImageManipulationMode.InpaintMask;
    }
}
