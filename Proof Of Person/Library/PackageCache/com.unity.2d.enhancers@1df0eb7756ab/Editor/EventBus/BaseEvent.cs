using UnityEngine.Assertions;

namespace Unity.U2D.AI.Editor.EventBus
{
    internal interface IEventSender
    { }

    internal interface IEvent
    { }

    internal abstract class BaseEvent : IEvent
    {
        readonly IEventSender m_Sender;

        protected BaseEvent(IEventSender sender)
        {
            m_Sender = sender;
        }

        public IEventSender sender => m_Sender;

    }
}