using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    internal interface IDoodlePadsContainer
    {
        IDoodlePad this[EDoodlePadMode index] { get; }
        IDoodlePad this[int index] { get; set; }
        EDoodlePadMode doodleMode { get; set; }
        IDoodlePad doodlePad { get; }
        int Count { get; }
        void Initialize(VisualElement parent, Vector2Int size);
    }

    internal class DoodlePadsContainer : IDoodlePadsContainer
    {
        IDoodlePad[] m_DoodlePads = new IDoodlePad[(int)EDoodlePadMode.Count];
        EDoodlePadMode m_DoodleMode = EDoodlePadMode.Overlay;

        public IDoodlePad this[EDoodlePadMode index] => m_DoodlePads[(int)index];

        public IDoodlePad this[int index]
        {
            get => m_DoodlePads[index];
            set => m_DoodlePads[index] = value;
        }

        public EDoodlePadMode doodleMode
        {
            get => m_DoodleMode;
            set
            {
                m_DoodleMode = value;
                for (var i = 0; i < m_DoodlePads.Length; ++i)
                {
                    m_DoodlePads[i].Activate(m_DoodleMode == (EDoodlePadMode)i);
                }
            }
        }

        public IDoodlePad doodlePad => m_DoodlePads[(int)m_DoodleMode];
        public int Count => m_DoodlePads.Length;

        public void Initialize(VisualElement parent, Vector2Int size)
        {
            for (var i = 0; i < m_DoodlePads.Length; ++i)
            {
                var newPad = (VisualElement)(m_DoodlePads[i] = new DoodlePad(size.x, size.y)
                {
                    name = ((EDoodlePadMode)i).ToString()
                });
                parent.Add(newPad);
                newPad.StretchToParentSize();
            }
        }
    }
}
