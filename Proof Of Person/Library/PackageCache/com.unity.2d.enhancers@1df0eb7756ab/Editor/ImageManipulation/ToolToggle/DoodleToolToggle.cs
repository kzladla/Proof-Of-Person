using System;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Events.UI;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    internal class DoodleToolToggle : EditorToolbarToggle, IToolToggle, IEventSender
    {
        UIEventBus m_EventBus;

        public DoodleToolToggle()
        {
            icon = Utilities.LoadIcon("Packages/com.unity.2d.enhancers/Editor/PackageResources/icons","Doodle@32.png");
            onIcon = Utilities.LoadIcon("Packages/com.unity.2d.enhancers/Editor/PackageResources/icons","Doodle On@32.png");
            tooltip = "Doodle";
            this.RegisterValueChangedCallback(OnToolActivte);
        }

        void OnToolActivte(ChangeEvent<bool> evt)
        {
            if (!evt.newValue)
            {
                SetValueWithoutNotify(true);
            }
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

        public EImageManipulationMode toolMode => EImageManipulationMode.Doodle;
    }
}
