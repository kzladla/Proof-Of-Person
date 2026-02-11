using System;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks
{
    class ResponseBlockModel : IMessageBlockModel, IEquatable<ResponseBlockModel>
    {
        public string Content;
        public bool IsComplete;

        public bool Equals(ResponseBlockModel other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Content == other.Content &&  IsComplete == other.IsComplete;
        }

        public override bool Equals(object obj) => obj is ResponseBlockModel other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Content, IsComplete);
    }
}