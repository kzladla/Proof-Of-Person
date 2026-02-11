using System;
using Unity.U2D.AI.Editor.AIBridge;
using Unity.U2D.AI.Editor.EventBus;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class AIModeChangeEvent : BaseEvent
    {
        public EAIMode mode;
        public AIModeChangeEvent(IEventSender sender)
            : base(sender) { }
    }

    partial class UIEventBus
    {
        public void SendEvent(AIModeChangeEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => AIModeChangeEvent?.Invoke((AIModeChangeEvent)x), queue);
        }

        public event Action<AIModeChangeEvent> AIModeChangeEvent;
    }
}
