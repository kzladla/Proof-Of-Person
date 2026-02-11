using System;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Overlay;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class ImageReferenceTypeChangedEvent : BaseEvent
    {
        public ImageControl imageControl;

        public ImageReferenceTypeChangedEvent(IEventSender sender)
            : base(sender) { }
    }

    partial class UIEventBus
    {
        public void SendEvent(ImageReferenceTypeChangedEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => ImageReferenceTypeChangedEvent?.Invoke((ImageReferenceTypeChangedEvent)x), queue);
        }

        public event Action<ImageReferenceTypeChangedEvent> ImageReferenceTypeChangedEvent;
    }
}
