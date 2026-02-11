using System;
using Unity.U2D.AI.Editor.Events.UI;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    internal interface IToolToggle
    {
        void RegisterEventBus(UIEventBus eventBus);
        void SetToggleValue(bool value);
        EImageManipulationMode toolMode { get; }
    }
}