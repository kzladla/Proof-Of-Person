using System;
using Unity.AI.Image.Interfaces;
using UnityEngine;

namespace Unity.U2D.AI.Editor.AIBridge
{
    internal enum EAIMode
    {
        Generation,
        Inpaint,
        Upscale,
        RemoveBackground,
        Pixelate,
        Recolor,
        None
    }

    internal static class AIModeHelpers
    {
        public static EAIMode ToEAIMode(this AIMode mode)
        {
            switch (mode)
            {
                case AIMode.Generation:
                    return EAIMode.Generation;
                case AIMode.RemoveBackground:
                    return EAIMode.RemoveBackground;
                case AIMode.Pixelate:
                    return EAIMode.Pixelate;
                case AIMode.Recolor:
                    return EAIMode.Recolor;
                case AIMode.Upscale:
                    return EAIMode.Upscale;
                case AIMode.Inpaint:
                    return EAIMode.Inpaint;
            }

            throw new NotImplementedException(mode.ToString());
        }

        public static AIMode ToAIMode(this EAIMode mode)
        {
            switch (mode)
            {
                case EAIMode.Generation:
                    return AIMode.Generation;
                case EAIMode.RemoveBackground:
                    return AIMode.RemoveBackground;
                case EAIMode.Pixelate:
                    return AIMode.Pixelate;
                case EAIMode.Recolor:
                    return AIMode.Recolor;
                case EAIMode.Upscale:
                    return AIMode.Upscale;
                case EAIMode.Inpaint:
                    return AIMode.Inpaint;
            }

            throw new NotImplementedException(mode.ToString());
        }
    }

    // For Undo
    [Serializable]
    struct EquaptableAIMode : IEquatable<EquaptableAIMode>
    {
        [SerializeField]
        EAIMode m_Mode;

        public bool Equals(EquaptableAIMode other)
        {
            return m_Mode == other.m_Mode;
        }

        public static implicit operator EAIMode(EquaptableAIMode mode) => mode.m_Mode;
        public static implicit operator EquaptableAIMode(EAIMode mode) => new() { m_Mode = mode };
        public static implicit operator AIMode(EquaptableAIMode mode) => mode.m_Mode.ToAIMode();
        public static implicit operator EquaptableAIMode(AIMode mode) => mode.ToEAIMode();
    }
}
