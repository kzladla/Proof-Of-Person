using System;
using Unity.U2D.AI.Editor.Prefs;
using UnityEngine;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    internal struct BrushSettingsData : IEquatable<BrushSettingsData>
    {
        public readonly bool canModifyColor;
        public readonly bool canModifySize;

        public float size;
        public float minSize;
        public float maxSize;
        public Color color;

        public BrushSettingsData(bool canModifySize, bool canModifyColor, float size, float minSize, float maxSize, Color color)
        {
            this.canModifySize = canModifySize;
            this.canModifyColor = canModifyColor;

            this.minSize = Mathf.Max(0.0f, Mathf.Min(size, minSize, maxSize));
            this.maxSize = Mathf.Max(size, minSize, maxSize);
            this.size = Mathf.Clamp(size, this.minSize, this.maxSize);
            this.color = color;
        }

        public bool Equals(BrushSettingsData other)
        {
            return size.Equals(other.size) && minSize.Equals(other.minSize) && maxSize.Equals(other.maxSize) && color.Equals(other.color);
        }

        public override bool Equals(object obj)
        {
            return obj is BrushSettingsData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(size, minSize, maxSize, color);
        }
    }

    internal class BrushSettings
    {
        /// <summary>
        /// Reference pixel size of the canvas.
        /// </summary>
        /// <remarks>
        /// Size of the brush is expressed in pixels for a canvas of this size.
        /// </remarks>
        public const int referenceCanvasSize = 1024;

        public readonly string name;

        public BrushSettingsData data;

        float sizeIncrement => (data.maxSize - data.minSize) / k_SizeIncrementCount;

        public static readonly Color DefaultBaseColor = Color.white;
        public static readonly Color DefaultOverlayColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);

        public const float defaultMinSize = 1;
        public const float defaultMaxSize = 100;
        public const float defaultSize = 30;

        const int k_SizeIncrementCount = 10;

        public BrushSettings(string name, float size, float minSize, float maxSize, Color color, bool canModifySize = true, bool canModifyColor = true)
        {
            this.name = name;
            data = new BrushSettingsData(canModifySize, canModifyColor, size, minSize, maxSize, color);
        }

        public BrushSettings(string name, Color color, bool canModifySize = true, bool canModifyColor = true)
            : this(name, defaultSize, defaultMinSize, defaultMaxSize, color,
                canModifySize, canModifyColor) { }

        public float GetAdjustedSize(Vector2Int canvasSize)
        {
            var maxDimension = Math.Max(canvasSize.x, canvasSize.y);
            if (maxDimension <= 0)
                return 0;

            return data.size * maxDimension / referenceCanvasSize;
        }

        public void IncreaseSize() => data.size = Math.Clamp(data.size + sizeIncrement, data.minSize, data.maxSize);

        public void DecreaseSize() => data.size = Math.Clamp(data.size - sizeIncrement, data.minSize, data.maxSize);

        public void TryLoadFromEditorPrefs()
        {
            if (ModulePreferences.GetBrushSize(GetKey(), out var size, out var minSize, out var maxSize))
            {
                data.size = size;
                data.minSize = minSize;
                data.maxSize = maxSize;
            }

            data.color = ModulePreferences.GetBrushColor(GetKey(), out var color) ? color : DefaultBaseColor;
        }

        public void SaveToEditorPrefs()
        {
            ModulePreferences.SetBrushSize(GetKey(), data.size, data.minSize, data.maxSize);
            ModulePreferences.SetBrushColor(GetKey(), data.color);
        }

        string GetKey() => $"{nameof(BrushSettings)}.{name}";
    }
}
