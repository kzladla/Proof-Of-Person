using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor
{
    [UxmlElement]
    partial class OutlinedImage : ImmediateModeElement
    {
        static readonly int k_MainTex = Shader.PropertyToID("_MainTex");
        static readonly int k_FillColor = Shader.PropertyToID("_FillColor");
        static readonly int k_OutlineBrightColor = Shader.PropertyToID("_OutlinerBrightColo");
        static readonly int k_OutlineDarkColor = Shader.PropertyToID("_OutlineDarkColor");
        static readonly int k_Thickness = Shader.PropertyToID("_Thickness");
        static readonly int k_DashSpacing = Shader.PropertyToID("_DashSpacing");
        static readonly int k_Speed = Shader.PropertyToID("_Speed");
        static readonly int k_Time = Shader.PropertyToID("_EditorTime");

        const long k_RefreshTimeMs = 32;

        Material m_OutlineMaterial;

        Material OutlineMaterial
        {
            get
            {
                if (m_OutlineMaterial == null)
                    m_OutlineMaterial = new Material(Utilities.LoadPackageResource<Shader>("shaders/Outline.shader"))
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };

                return m_OutlineMaterial;
            }
        }

        [UxmlAttribute]
        public Texture texture
        {
            get => OutlineMaterial.GetTexture(k_MainTex);
            set => OutlineMaterial.SetTexture(k_MainTex, value);
        }

        [UxmlAttribute]
        public Color fillColor
        {
            get => OutlineMaterial.GetColor(k_FillColor);
            set => OutlineMaterial.SetColor(k_FillColor, value);
        }

        [UxmlAttribute]
        public Color outlineColorBright
        {
            get => OutlineMaterial.GetColor(k_OutlineBrightColor);
            set => OutlineMaterial.SetColor(k_OutlineBrightColor, value);
        }

        [UxmlAttribute]
        public Color outlineColorDark
        {
            get => OutlineMaterial.GetColor(k_OutlineDarkColor);
            set => OutlineMaterial.SetColor(k_OutlineDarkColor, value);
        }

        [UxmlAttribute]
        public float outlineWidth
        {
            get => OutlineMaterial.GetFloat(k_Thickness);
            set => OutlineMaterial.SetFloat(k_Thickness, value);
        }

        [UxmlAttribute]
        public float spacing
        {
            get => OutlineMaterial.GetFloat(k_DashSpacing);
            set => OutlineMaterial.SetFloat(k_DashSpacing, value);
        }

        [UxmlAttribute]
        public float speed
        {
            get => OutlineMaterial.GetFloat(k_Speed);
            set => OutlineMaterial.SetFloat(k_Speed, value);
        }

        public OutlinedImage()
        {
            schedule.Execute(UpdateTime).Every(k_RefreshTimeMs);
        }

        void UpdateTime()
        {
            OutlineMaterial.SetFloat(k_Time, (float)EditorApplication.timeSinceStartup);
            MarkDirtyRepaint();
        }

        protected override void ImmediateRepaint()
        {
            var min = contentRect.min;
            var max = contentRect.max;

            GL.PushMatrix();
            OutlineMaterial.SetPass(0);

            GL.Begin(GL.QUADS);
            GL.Color(Color.white);

            GL.TexCoord2(0, 1f);
            GL.Vertex(min);

            GL.TexCoord2(0, 0);
            GL.Vertex(new Vector3(min.x, max.y));

            GL.TexCoord2(1, 0);
            GL.Vertex(max);

            GL.TexCoord2(1, 1f);
            GL.Vertex(new Vector3(max.x, min.y));

            GL.End();
            GL.PopMatrix();
        }
    }

    internal class DoodleImage : VisualElement
    {
        OutlinedImage m_OutlinedImage;
        Image m_Image;

        Texture m_Texture;
        bool m_IsOutlined;

        public Texture image
        {
            get => m_Texture;
            set
            {
                if (value != m_Texture)
                {
                    m_Texture = value;
                    if (m_IsOutlined)
                        m_OutlinedImage.texture = m_Texture;
                    else
                        m_Image.image = m_Texture;
                }
            }
        }

        public bool isOutlined
        {
            get => m_IsOutlined;
            set
            {
                if (value != m_IsOutlined)
                {
                    m_IsOutlined = value;
                    if (m_IsOutlined)
                    {
                        m_Image.RemoveFromHierarchy();
                        m_OutlinedImage.texture = m_Texture;
                        Add(m_OutlinedImage);
                    }
                    else
                    {
                        m_OutlinedImage.RemoveFromHierarchy();
                        m_Image.image = m_Texture;
                        Add(m_Image);
                    }
                }
            }
        }

        public DoodleImage(bool outlined = false)
        {
            m_Image = new Image { pickingMode = PickingMode.Ignore };
            m_Image.StretchToParentSize();
            m_OutlinedImage = new OutlinedImage
            {
                pickingMode = PickingMode.Ignore,
                spacing = 200,
                speed = 5,
                outlineWidth = 0.03f
            };
            m_OutlinedImage.StretchToParentSize();
            isOutlined = outlined;
        }
    }
}
