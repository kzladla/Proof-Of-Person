using System;
using Unity.U2D.AI.Editor.EventBus;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class GenerateEvent : BaseEvent
    {
        public GenerateEvent(IEventSender sender)
            : base(sender) { }
    }

    partial class UIEventBus
    {
        public void SendEvent(GenerateEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => GenerateEvent?.Invoke((GenerateEvent)x), queue);
        }

        public event Action<GenerateEvent> GenerateEvent;
    }
}
