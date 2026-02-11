using UnityEditor.ShortcutManagement;
using UnityEngine;
using System;
using UnityEditor.U2D.Common;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    internal class SpriteImageManipulationContext : InternalEditorBridge.ShortcutContext { }

    internal static class Shortcuts
    {
        public static event Action OnBrushSizeIncrease;
        public static event Action OnBrushSizeDecrease;

        [Shortcut("Sprite Editor/AI/Increase Brush Size", typeof(SpriteImageManipulationContext),  KeyCode.RightBracket)]
        static void IncreaseBrushSize()
        {
            OnBrushSizeIncrease?.Invoke();
        }
        [Shortcut("Sprite Editor/AI/Decrease Brush Size", typeof(SpriteImageManipulationContext), KeyCode.LeftBracket)]
        static void DecreaseBrushSize()
        {
            OnBrushSizeDecrease?.Invoke();
        }
    }
}
