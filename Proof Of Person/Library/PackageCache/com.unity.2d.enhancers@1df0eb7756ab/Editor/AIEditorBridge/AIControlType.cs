using System;
using Unity.AI.Image.Interfaces;
using UnityEngine;

namespace Unity.U2D.AI.Editor.AIBridge
{
    internal enum EControlType
    {
        CompositionImage,
        DepthImage,
        FeatureImage,
        LineArtImage,
        InPaintMaskImage,
        PaletteImage,
        PoseImage,
        PromptImage,
        StyleImage
    }

    internal static class ControlTypeHelpers
    {
        public static EControlType ToEControlType(this ControlType controlType)
        {
            switch (controlType)
            {
                case ControlType.CompositionImage:
                    return EControlType.CompositionImage;
                case ControlType.DepthImage:
                    return EControlType.DepthImage;
                case ControlType.FeatureImage:
                    return EControlType.FeatureImage;
                case ControlType.LineArtImage:
                    return EControlType.LineArtImage;
                case ControlType.InPaintMaskImage:
                    return EControlType.InPaintMaskImage;
                case ControlType.PaletteImage:
                    return EControlType.PaletteImage;
                case ControlType.PoseImage:
                    return EControlType.PoseImage;
                case ControlType.PromptImage:
                    return EControlType.PromptImage;
                case ControlType.StyleImage:
                    return EControlType.StyleImage;
            }

            throw new NotImplementedException();
        }

        public static ControlType ToControlType(this EControlType controlType)
        {
            switch (controlType)
            {
                case EControlType.CompositionImage:
                    return ControlType.CompositionImage;
                case EControlType.DepthImage:
                    return ControlType.DepthImage;
                case EControlType.FeatureImage:
                    return ControlType.FeatureImage;
                case EControlType.LineArtImage:
                    return ControlType.LineArtImage;
                case EControlType.InPaintMaskImage:
                    return ControlType.InPaintMaskImage;
                case EControlType.PaletteImage:
                    return ControlType.PaletteImage;
                case EControlType.PoseImage:
                    return ControlType.PoseImage;
                case EControlType.PromptImage:
                    return ControlType.PromptImage;
                case EControlType.StyleImage:
                    return ControlType.StyleImage;
            }

            throw new NotImplementedException();
        }
    }

    [Serializable]
    internal class EquatableControlType : IEquatable<EquatableControlType>
    {
        [SerializeField]
        EControlType m_ControlType;

        public bool Equals(EquatableControlType other)
        {
            return other != null && m_ControlType == other.m_ControlType;
        }

        public static implicit operator EControlType(EquatableControlType mode) => mode.m_ControlType;
        public static implicit operator EquatableControlType(EControlType controlType) => new() { m_ControlType = controlType };
        public static implicit operator EquatableControlType(ControlType controlType) => controlType.ToEControlType();
    }
}
