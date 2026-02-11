using System;
using Unity.U2D.AI.Editor.EventBus;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class ResultSelectionChangedEvent : BaseEvent
    {
        public Uri uri { get; set; }
        public ResultSelectionChangedEvent(IEventSender sender)
            : base(sender) { }
    }

    partial class UIEventBus
    {
        public void SendEvent(ResultSelectionChangedEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => OnResultSelectionChangedEvent?.Invoke((ResultSelectionChangedEvent)x), queue);
        }

        public event Action<ResultSelectionChangedEvent> OnResultSelectionChangedEvent;
    }
}
