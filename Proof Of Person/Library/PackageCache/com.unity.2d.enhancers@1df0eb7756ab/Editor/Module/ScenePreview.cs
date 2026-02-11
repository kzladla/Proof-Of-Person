using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Unity.U2D.AI.Editor
{
    interface IScenePreview
    {
        void Activate();
        void Deactivate();
        void SetSpriteRect(Texture2D texture, SpriteRect sr);
    }

    class ScenePreview : IScenePreview
    {
        class SceneSprite
        {
            public readonly SpriteRenderer spriteRenderer;
            public readonly Sprite originalSprite;
            public readonly Sprite overrideSprite;

            public SceneSprite(SpriteRenderer spriteRenderer, Sprite originalSprite, Sprite overrideSprite)
            {
                this.spriteRenderer = spriteRenderer;
                this.originalSprite = originalSprite;
                this.overrideSprite = overrideSprite;
            }
        }

        ISpriteEditor m_SpriteEditor;
        List<SceneSprite> m_SceneViewSpriteRenderers = new List<SceneSprite>();
        GameObject[] m_GameObjects;
        SpriteRect m_SpriteRect;
        Texture2D m_Texture;

        public ScenePreview(ISpriteEditor spriteEditor)
        {
            m_SpriteEditor = spriteEditor;
        }

        public void Activate()
        {
            m_SpriteEditor.SetScenePreviewCallback(OnScenePreview);
        }

        public void Deactivate()
        {
            m_SpriteEditor.SetScenePreviewCallback(null);
        }

        public void SetSpriteRect(Texture2D texture, SpriteRect sr)
        {
            m_Texture = texture;
            m_SpriteRect = sr;
            PreviewSelected();
        }

        void OnScenePreview(GameObject[] newGameObjects)
        {
            m_GameObjects = newGameObjects;
            PreviewSelected();
        }

        void RestoreSceneViewSpriteRendererSprite()
        {
            if (m_SceneViewSpriteRenderers != null)
            {
                foreach (var sceneSprite in m_SceneViewSpriteRenderers)
                {
                    if (sceneSprite.spriteRenderer == null)
                        continue;

                    var currentSprite = sceneSprite.spriteRenderer.sprite;
                    if (currentSprite != sceneSprite.originalSprite && currentSprite == sceneSprite.overrideSprite)
                        sceneSprite.spriteRenderer.sprite = sceneSprite.originalSprite;
                    if (sceneSprite.overrideSprite != null)
                        sceneSprite.overrideSprite.SafeDestroy();
                }

                m_SceneViewSpriteRenderers.Clear();
            }
        }

        void PreviewSelected()
        {
            RestoreSceneViewSpriteRendererSprite();

            var selectedSprite = m_SpriteRect;
            if (selectedSprite == null)
            {
                //Debug.Log("No SpriteRect is selected. Nothing to preview.");
                return;
            }

            if (m_GameObjects == null || m_GameObjects.Length == 0)
            {
                //Debug.Log("No GameObject is selected. Nothing to preview.");
                return;
            }

            var spriteRenderer = m_GameObjects[0].GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.Log("No GameObject with SpriteRenderer is selected. Nothing to preview.");
                return;
            }

            if (spriteRenderer.sprite == null)
            {
                //Debug.Log("SpriteRenderer has no Sprite. Nothing to preview.");
                return;
            }


            var originalSprite = spriteRenderer.sprite;
            // TODO there is an optimzation here where we cna reuse the same Sprite by overriding the geometry only.
            var overrideTexture = m_Texture;
            if (overrideTexture == null)
            {
                RestoreSceneViewSpriteRendererSprite();
                return;
            }

            // handle rect when selected rect is bigger than original texture
            var scale = 1;//overrideTexture.width / (float)m_Controller.imageSize.x;
            var rect = selectedSprite.rect;
            rect = new Rect(rect.x * scale, rect.y * scale, rect.width * scale, rect.height * scale);

            var overrideSprite = Sprite.Create(overrideTexture, rect,
                new Vector2(originalSprite.pivot.x / originalSprite.rect.width, originalSprite.pivot.y / originalSprite.rect.height),
                // (selectedSprite.rect.width/originalSprite.rect.width) *
                originalSprite.pixelsPerUnit * scale);

            overrideSprite.name = $"2D-SpritePreview {GUID.Generate().ToString()}";
            m_SceneViewSpriteRenderers.Add(new SceneSprite(spriteRenderer, originalSprite, overrideSprite));
            spriteRenderer.sprite = overrideSprite;
        }
    }
}
