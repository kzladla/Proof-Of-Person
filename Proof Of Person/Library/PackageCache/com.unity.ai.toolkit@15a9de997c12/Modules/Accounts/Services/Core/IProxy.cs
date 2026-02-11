using System;

namespace Unity.AI.Toolkit.Accounts.Services.Core
{
    interface IProxy<T>
    {
        public T Value { get; set; }
    }
}
