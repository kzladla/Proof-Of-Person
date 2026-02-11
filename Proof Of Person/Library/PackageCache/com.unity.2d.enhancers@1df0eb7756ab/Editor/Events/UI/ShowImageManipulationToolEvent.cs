using System;
using Unity.U2D.AI.Editor.AIBridge;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Overlay;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class ShowImageManipulationToolEvent : BaseEvent
    {
        public EAIMode mode;
        public ImageControl imageControl;
        public ShowImageManipulationToolEvent(IEventSender sender)
            : base(sender) { }
    }

    partial class UIEventBus
    {
        public void SendEvent(ShowImageManipulationToolEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => ShowImageManipulationToolEvent?.Invoke((ShowImageManipulationToolEvent)x), queue);
        }

        public event Action<ShowImageManipulationToolEvent> ShowImageManipulationToolEvent;
    }
}
