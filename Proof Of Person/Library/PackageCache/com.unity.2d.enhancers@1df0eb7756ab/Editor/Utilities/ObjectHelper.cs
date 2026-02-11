using UnityEditor;
using UnityEngine;

namespace Unity.U2D.AI.Editor
{
    internal static class ObjectHelper
    {
        public static void SafeDestroy(this Object @object)
        {
            if(@object == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(@object);
            else if(!EditorUtility.IsPersistent(@object))
                Object.DestroyImmediate(@object);
        }
    }
}