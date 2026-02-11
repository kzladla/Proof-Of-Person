using System;

namespace Unity.U2D.AI.Editor
{
    enum EImageManipulationMode
    {
        Doodle,
        Eraser,
        Clear,
        InpaintEraser,
        InpaintMask,
        None,
    }


    // For Undo
    [Serializable]
    struct EquaptableEImageManipulationMode: IEquatable<EquaptableEImageManipulationMode>
    {
        public EImageManipulationMode mode;

        public bool Equals(EquaptableEImageManipulationMode other)
        {
            return mode == other.mode;
        }
        public static implicit operator EImageManipulationMode(EquaptableEImageManipulationMode mode) => mode.mode;
        public static implicit operator EquaptableEImageManipulationMode(EImageManipulationMode mode) => new EquaptableEImageManipulationMode(){mode = mode};
    }
}
