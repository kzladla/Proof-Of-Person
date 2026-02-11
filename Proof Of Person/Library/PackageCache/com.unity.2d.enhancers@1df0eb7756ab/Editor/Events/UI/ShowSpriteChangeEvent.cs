using System;
using Unity.U2D.AI.Editor.EventBus;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class ShowSpriteChangeEvent : BaseEvent
    {
        public bool showBackground;
        public float backgroundAlpha;
        public ShowSpriteChangeEvent(IEventSender sender)
            : base(sender) { }
    }

    partial class UIEventBus
    {
        public void SendEvent(ShowSpriteChangeEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => ShowSpriteChangeEvent?.Invoke((ShowSpriteChangeEvent)x), queue);
        }

        public event Action<ShowSpriteChangeEvent> ShowSpriteChangeEvent;
    }
}
