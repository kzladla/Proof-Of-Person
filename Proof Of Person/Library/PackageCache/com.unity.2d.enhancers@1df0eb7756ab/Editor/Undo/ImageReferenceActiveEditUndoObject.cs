using System;
using Unity.U2D.AI.Editor.AIBridge;
using Unity.U2D.AI.Editor.Overlay;
using UnityEngine;

namespace Unity.U2D.AI.Editor.Undo
{
    [Serializable]
    struct ImageReferenceActiveEditUndoData : IEquatable<ImageReferenceActiveEditUndoData>
    {
        [SerializeField]
        EControlType m_ControlType;
        [SerializeField]
        bool m_ValidControlType;
        [SerializeField]
        bool m_IsEditting;

        public ImageControl imageControl
        {
            set
            {
                m_ValidControlType = value != null;
                if(m_ValidControlType)
                    m_ControlType = value.controlType;
            }
        }

        public bool haveValidControlType => m_ValidControlType;

        public EControlType controlType => m_ControlType;

        public bool isEditting
        {
            get => m_IsEditting;
            set => m_IsEditting = value;
        }

        public bool Equals(ImageReferenceActiveEditUndoData other)
        {
            var same = m_ValidControlType == other.m_ValidControlType && m_IsEditting == other.m_IsEditting;
            if(same && m_ValidControlType) // only check control type if it is valid
                return m_ControlType == other.m_ControlType;
            return same;
        }

        public override bool Equals(object obj)
        {
            return obj is ImageReferenceActiveEditUndoData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)m_ControlType, m_ValidControlType, m_IsEditting);
        }
    }

    class ImageReferenceActiveEditUndoObject : UndoObject<ImageReferenceActiveEditUndoData>
    { }
}
