using System;
using System.Collections.Generic;
using Unity.U2D.AI.Editor.ImageManipulation;
using Unity.U2D.AI.Editor.Overlay;
using UnityEngine;

namespace Unity.U2D.AI.Editor
{
    record TextureData
    {
        public byte[] data;
        public int width;
        public int height;
    }

    [Serializable]
    record DoodleDataKey
    {
        public ImageControl imageControl;
        public EDoodlePadMode doodlePad;

        public DoodleDataKey(ImageControl imageControl, EDoodlePadMode doodlePad)
        {
            this.imageControl = imageControl;
            this.doodlePad = doodlePad;
        }
    }

    interface IOperatorDoodleStorage : IDisposable
    {
        TextureData GetClearTexture();
        TextureData GetTexture(DoodleDataKey type);
        TextureData originalTexture { get; }
        bool HasTexture(DoodleDataKey type);
        void SetTexture(DoodleDataKey type, byte[] data, bool doodled);
        void DeleteTexture(DoodleDataKey type);
        void SetSize(Vector2Int newSize);
        Vector2Int size { get; }
    }

    class OperatorDoodleStorage : IOperatorDoodleStorage
    {
        TextureData m_OriginalTexture;
        Dictionary<DoodleDataKey, (TextureData texture, bool doodled)> m_DoodleTextures = new ();

        Vector2Int m_Size;
        public Vector2Int size => m_Size;

        public OperatorDoodleStorage(TextureData originalTexture)
        {
            SetSize(new Vector2Int(originalTexture.width, originalTexture.height));
            SetOriginalTexture(originalTexture);
        }

        public void Dispose()
        {
            m_DoodleTextures.Clear();
        }

        public TextureData GetClearTexture()
        {
            var width = m_Size.x;
            var height = m_Size.y;

            var t = new Texture2D(width, height)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "com.unity.2d.enhancer SpriteFrameAIModuleController.GetClearTexture"
            };
            var pixelData = new Color32[width * height];
            Array.Fill(pixelData, Color.clear, 0, pixelData.Length);
            t.SetPixelData(pixelData, 0);
            t.Apply();
            var bytes = t.EncodeToPNG();
            t.SafeDestroy();
            return new TextureData
            {
                data = bytes,
                width = width,
                height = height
            };
        }

        public TextureData GetTexture(DoodleDataKey type)
        {
            if (type == null)
                return null;
            m_DoodleTextures.TryAdd(type, (null, false));

            if (m_DoodleTextures[type].texture == null)
            {
                var t = GetClearTexture();
                m_DoodleTextures[type] = (t, false);
            }

            return m_DoodleTextures[type].texture;
        }

        public TextureData originalTexture => m_OriginalTexture;

        public bool HasTexture(DoodleDataKey type)
        {
            if(type != null)
                return m_DoodleTextures.ContainsKey(type);
            return true;
        }

        public void DeleteTexture(DoodleDataKey type)
        {
            if(type != null)
                m_DoodleTextures.Remove(type);
        }

        public void SetSize(Vector2Int newSize)
        {
            if(newSize.x <= 0 || newSize.y <= 0)
                throw new ArgumentOutOfRangeException($"Invalid size {newSize}. Each dimention must be greater than 0.");
            m_Size = newSize;
        }

        public void SetTexture(DoodleDataKey type, byte[] data, bool doodled)
        {
            var validData = data != null && data.Length > 0;
            if (type == null)
            {
                if (validData)
                {
                    var t = new Texture2D(1, 1)
                    {
                        name = "com.unity.2d.enhancer SpriteFrameAIModuleController.SetTexture for original",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    t.LoadImage(data);
                    SetOriginalTexture(new TextureData()
                    {
                        data = data,
                        width = t.width,
                        height = t.height
                    });
                    t.SafeDestroy();
                }
                return;
            }
            if (!m_DoodleTextures.ContainsKey(type))
                m_DoodleTextures.Add(type, (null, doodled));

            if (m_DoodleTextures[type].texture == null)
            {
                if (validData)
                {
                    var t = new Texture2D(1, 1)
                    {
                        name = $"com.unity.2d.enhancer SpriteFrameAIModuleController.SetTexture Doodle for {type}",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    t.LoadImage(data);
                    m_DoodleTextures[type] = (new TextureData
                    {
                        data = data,
                        width = t.width,
                        height = t.height
                    }, doodled);
                    t.SafeDestroy();
                }
            }
            else
            {
                if(validData)
                    m_DoodleTextures[type].texture.data = data;
                else
                    m_DoodleTextures[type] = (null, false);
            }
        }

        void SetOriginalTexture(TextureData texture)
        {
            m_OriginalTexture = texture;
            List<(DoodleDataKey type, (TextureData texture, bool doodled) value)> toModified = new ();
            foreach(var (controlType, (tex, doodled)) in m_DoodleTextures)
            {
                if (!doodled)
                {
                    toModified.Add((controlType, (new TextureData()
                    {
                        data = texture.data,
                        width = texture.width,
                        height = texture.height
                    }, false)));
                }
            }
            for(int i = 0; i < toModified.Count; i++)
            {
                var data = toModified[i];
                m_DoodleTextures[data.type] = data.value;
            }
        }
    }
}
