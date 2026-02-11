using System;

namespace Unity.AI.Toolkit.Accounts.Services.Core
{
    class Proxy<T> : IProxy<T>
    {
        readonly Func<T> m_Get;
        readonly Action<T> m_Set;

        public T Value { get => m_Get(); set => m_Set(value); }

        public Proxy(Func<T> get, Action<T> set)
        {
            m_Get = get;
            m_Set = set;
        }
    }
}
