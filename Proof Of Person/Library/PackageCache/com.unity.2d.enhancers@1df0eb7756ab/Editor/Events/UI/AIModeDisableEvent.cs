using System;
using Unity.U2D.AI.Editor.AIBridge;
using Unity.U2D.AI.Editor.EventBus;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class AIModeEnableEvent : BaseEvent
    {
        public EAIMode mode;

        public AIModeEnableEvent(IEventSender sender)
            : base(sender) { }

        public bool enable { get; set; }
    }

    partial class UIEventBus
    {
        public void SendEvent(AIModeEnableEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => AIModeDisableEvent?.Invoke((AIModeEnableEvent)x), queue);
        }

        public event Action<AIModeEnableEvent> AIModeDisableEvent;
    }
}
