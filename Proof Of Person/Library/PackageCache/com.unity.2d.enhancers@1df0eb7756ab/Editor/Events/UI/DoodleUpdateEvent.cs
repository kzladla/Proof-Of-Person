using System;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.ImageManipulation;
using UnityEngine;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class DoodleUpdateEvent : BaseEvent
    {
        public byte[] textureData;
        public EDoodlePadMode doodleMode;
        public DoodleUpdateEvent(IEventSender sender)
            : base(sender) { }
    }

    partial class UIEventBus
    {
        public void SendEvent(DoodleUpdateEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => DoodleUpdateEvent?.Invoke((DoodleUpdateEvent)x), queue);
        }

        public event Action<DoodleUpdateEvent> DoodleUpdateEvent;
    }
}
