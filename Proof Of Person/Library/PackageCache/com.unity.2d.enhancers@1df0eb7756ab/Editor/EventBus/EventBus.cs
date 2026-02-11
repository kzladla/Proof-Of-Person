using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace Unity.U2D.AI.Editor.EventBus
{
    record EventCallback
    {
        public IEvent evt;
        public Action<IEvent> callback;
    }

    internal class EventBus : IDisposable
    {
        Queue<EventCallback> m_QueueEventCallback = new ();
        int m_SendEventCount = 0;

        public void SendEvent<T>(T arg, Action<IEvent> sendEventCall, bool queue = false) where T : IEvent
        {
            if (m_SendEventCount > 0 && queue)
            {
                m_QueueEventCallback.Enqueue(new EventCallback()
                {
                    evt = arg,
                    callback = sendEventCall
                });
                return;
            }

            m_SendEventCount++;
            sendEventCall?.Invoke(arg);
            m_SendEventCount--;

            ClearQueue();
        }

        void ClearQueue()
        {
            if (m_SendEventCount == 0 && m_QueueEventCallback.Count > 0)
            {
                var evt = m_QueueEventCallback.Dequeue();
                SendEvent(evt.evt, evt.callback);
            }
        }

        public void Dispose()
        {
            m_QueueEventCallback.Clear();
        }
    }
}