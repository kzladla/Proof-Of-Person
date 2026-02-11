using System;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.ImageManipulation;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class ImageManipulationModeChangeEvent : BaseEvent
    {
        public EImageManipulationMode mode;
        public EDoodlePadMode doodleMode;
        public ImageManipulationModeChangeEvent(IEventSender sender)
            : base(sender) { }
    }

    partial class UIEventBus
    {
        public void SendEvent(ImageManipulationModeChangeEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => ImageManipulationToolChangeEvent?.Invoke((ImageManipulationModeChangeEvent)x), queue);
        }

        public event Action<ImageManipulationModeChangeEvent> ImageManipulationToolChangeEvent;
    }
}
