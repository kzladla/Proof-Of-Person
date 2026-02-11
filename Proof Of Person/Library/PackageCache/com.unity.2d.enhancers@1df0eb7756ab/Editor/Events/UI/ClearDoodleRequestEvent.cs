using System;
using Unity.U2D.AI.Editor.EventBus;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class ClearDoodleRequestEvent: BaseEvent
    {
        public ClearDoodleRequestEvent(IEventSender sender)
            : base(sender) { }
    }

    partial class UIEventBus
    {
        public void SendEvent(ClearDoodleRequestEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => ClearDoodleRequstEvent?.Invoke((ClearDoodleRequestEvent)x), queue);
        }

        public event Action<ClearDoodleRequestEvent> ClearDoodleRequstEvent;
    }
}