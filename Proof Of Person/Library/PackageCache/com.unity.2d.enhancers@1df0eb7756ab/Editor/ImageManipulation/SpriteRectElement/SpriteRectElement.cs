using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    internal class SpriteRectElement : VisualElement
    {
        public const string spriteRectUssClassName = "sprite-rect";
        public const string ussElementBorderClassName = spriteRectUssClassName + "_border";

        public Vector2Int size { get; private set; }
        public Vector2 position { get; private set; }

        bool m_Selected;

        public bool selected
        {
            get => m_Selected;
            set
            {
                m_Selected = value;
                if(m_Selected)
                    this.AddToClassList(ussElementBorderClassName);
                else
                    this.RemoveFromClassList(ussElementBorderClassName);
            }
        }

        public SpriteRectElement(Vector2 position, Vector2Int size)
        {
            this.position = position;
            this.size = size;

            AddToClassList(spriteRectUssClassName);

            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.2d.enhancers/Editor/ImageManipulation/SpriteRectElement/SpriteRectElement.uss"));
        }

        public void UpdatePositionAndScale(Vector2 center, Vector2 offset, float scale)
        {
            style.width = size.x * scale;
            style.height = size.y * scale;

            style.left = center.x - offset.x + position.x * scale;
            style.bottom = center.y + offset.y + position.y * scale;
        }

        internal void UpdateSize(Rect rect)
        {
            position = new Vector2(rect.position.x - rect.width * 0.5f, rect.position.y - rect.height * 0.5f);
            size = new Vector2Int(Mathf.RoundToInt(rect.size.x), Mathf.RoundToInt(rect.size.y));
        }
    }
}
