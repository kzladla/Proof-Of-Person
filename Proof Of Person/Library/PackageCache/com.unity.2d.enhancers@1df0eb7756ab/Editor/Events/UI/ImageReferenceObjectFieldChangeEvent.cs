using System;
using Unity.U2D.AI.Editor.ImageManipulation;
using Unity.U2D.AI.Editor.EventBus;
using UnityEngine;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class ImageReferenceObjectFieldChangeEvent: BaseEvent
    {
        public Texture2D texture;
        public EImageReferenceType imageReferenceType;
        public ImageReferenceObjectFieldChangeEvent(IEventSender sender)
            : base(sender) { }
    }

    partial class UIEventBus
    {
        public void SendEvent(ImageReferenceObjectFieldChangeEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => ImageReferenceObjectFieldChangeEvent?.Invoke((ImageReferenceObjectFieldChangeEvent)x), queue);
        }

        public event Action<ImageReferenceObjectFieldChangeEvent> ImageReferenceObjectFieldChangeEvent;
    }
}
