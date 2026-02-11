using System;
using Unity.U2D.AI.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    enum EImageReferenceType
    {
        PromptImageReference,
        CompositionImageReference,
        LineArtImageReference,
        InpaintMaskImageReference,
        None
    }

    static class ImageReferenceType
    {
        // public static  (EImageReferenceType type, string elementClassName, Action<VisualElement, Texture2D> setImage, Func<VisualElement, byte[]> getDoodleImageData)[] k_ImageReferenceType =
        // {
        //     (EImageReferenceType.PromptImageReference, "prompt-image-reference", SpriteEditor.SetPromptImage, SpriteEditor.GetPromptImageReferenceDoodleData),
        //     (EImageReferenceType.CompositionImageReference, "composition-image-reference", SpriteEditor.SetCompositionImage, SpriteEditor.GetCompositionImageDoodleData),
        //     (EImageReferenceType.LineArtImageReference, "line-art-image-reference", SpriteEditor.SetLineArtImage, SpriteEditor.GetLineArtImageDoodleData),
        //     (EImageReferenceType.InpaintMaskImageReference, "inpaint-mask-image-reference", SpriteEditor.SetInpaintMaskImage, SpriteEditor.GetInPaintMaskImageDoodleData),
        // };

        public const string k_DoodleReferenceElementClassName = "doodle-reference-field";
        public const string k_DoodleReferenceImageElementClassName = "doodle-reference-image";

        // public static void SetImageReference(string type, VisualElement ve, Texture2D texture)
        // {
        //     foreach (var imageReferenceType in k_ImageReferenceType)
        //     {
        //         if (imageReferenceType.type == type)
        //         {
        //             var t = Utilities.GetReadableTexture(texture);
        //             imageReferenceType.setImage?.Invoke(ve, t);
        //             if(t != texture)
        //                 t.SafeDestroy();
        //             break;
        //         }
        //     }
        // }
        //
        // public static byte[]  GetDoodleImageData(EImageReferenceType type, VisualElement ve)
        // {
        //     foreach (var imageReferenceType in k_ImageReferenceType)
        //     {
        //         if (imageReferenceType.type == type)
        //             return imageReferenceType.getDoodleImageData?.Invoke(ve);
        //     }
        //
        //     return null;
        // }

    }
}
