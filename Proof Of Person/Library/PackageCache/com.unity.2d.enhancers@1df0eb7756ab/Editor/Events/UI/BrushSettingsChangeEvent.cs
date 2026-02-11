using System;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.ImageManipulation;
using UnityEngine;

namespace Unity.U2D.AI.Editor.Events.UI
{
    class BrushSettingsChangeEvent : BaseEvent
    {
        public BrushSettingsData brushSettings;
        public BrushSettingsChangeEvent(IEventSender sender)
            : base(sender) { }
    }

    partial class UIEventBus
    {
        public void SendEvent(BrushSettingsChangeEvent evt, bool queue = false)
        {
            m_EventBus.SendEvent(evt, x => BrushSettingsChangeEvent?.Invoke((BrushSettingsChangeEvent)x), queue);
        }

        public event Action<BrushSettingsChangeEvent> BrushSettingsChangeEvent;
    }
}
