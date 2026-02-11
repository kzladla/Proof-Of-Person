using System;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Events.UI;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    internal class ClearToolToggle : EditorToolbarButton, IToolToggle, IEventSender
    {
        public ClearToolToggle()
        {
            icon = Utilities.LoadIcon("Packages/com.unity.2d.enhancers/Editor/PackageResources/icons","Clear@2x.png");
            tooltip = "Clear Doodle";
            clicked += OnToolActivte;
        }

        UIEventBus m_EventBus;

        void OnToolActivte()
        {
            m_EventBus?.SendEvent(new ClearDoodleRequestEvent(this));
        }

        public void RegisterEventBus(UIEventBus eventBus)
        {
            m_EventBus = eventBus;
        }

        public void SetToggleValue(bool value)
        { }

        public EImageManipulationMode toolMode => EImageManipulationMode.Clear;
    }
}
