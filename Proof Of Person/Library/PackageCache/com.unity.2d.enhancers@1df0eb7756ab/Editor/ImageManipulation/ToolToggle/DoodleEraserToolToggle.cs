using System;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Events.UI;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    internal class DoodleEraserToolToggle : EditorToolbarToggle, IToolToggle, IEventSender
    {
        public DoodleEraserToolToggle()
        {
            icon = Utilities.LoadIcon("Packages/com.unity.2d.enhancers/Editor/PackageResources/icons","Eraser@32.png");
            onIcon = Utilities.LoadIcon("Packages/com.unity.2d.enhancers/Editor/PackageResources/icons","Eraser On@32.png");
            tooltip = "Eraser";
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
                    doodleMode = EDoodlePadMode.BaseImage
                });
            }
        }

        public void RegisterEventBus(UIEventBus eventBus)
        {
            m_EventBus = eventBus;
        }

        public void SetToggleValue(bool value)
        {
            SetValueWithoutNotify(value);
        }

        public EImageManipulationMode toolMode => EImageManipulationMode.Eraser;
    }
}
