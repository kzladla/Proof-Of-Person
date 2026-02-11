using System;

namespace Unity.U2D.AI.Editor.Undo
{
    [Serializable]
    struct DoodleUndoData : IEquatable<DoodleUndoData>
    {
        public DoodleDataKey doodleDataKey;
        public byte[] doodleData;

        public bool Equals(DoodleUndoData other)
        {
            return Equals(doodleDataKey, other.doodleDataKey) && Equals(doodleData, other.doodleData);
        }

        public override bool Equals(object obj)
        {
            return obj is DoodleUndoData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(doodleDataKey, doodleData);
        }
    }

    class DoodleUndoObject : UndoObject<DoodleUndoData>
    {

    }
}
