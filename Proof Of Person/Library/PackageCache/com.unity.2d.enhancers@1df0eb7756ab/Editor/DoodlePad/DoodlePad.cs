using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor
{
    internal enum DoodleModifierState
    {
        None,
        Brush,
        Erase,
        BucketFill,
        Mask
    }

    internal class DoodleCursorOverlay : VisualElement
    {
        const float k_LineWidth = 1.0f;
        const float k_SegmentLength = 10.0f;
        static readonly Color k_LineColor = Color.white;

        public DoodleCursorOverlay()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += GenerateVisualContent;
        }

        void GenerateVisualContent(MeshGenerationContext context)
        {
            var width = contentRect.width;
            var height = contentRect.height;
            var painter = context.painter2D;
            painter.lineWidth = k_LineWidth;
            painter.lineCap = LineCap.Butt;

            painter.strokeColor = k_LineColor;

            var radius = Mathf.Max(width, height) * 0.5f;
            var circumference = 2 * Mathf.PI * radius;
            var segmentCount = (int)(circumference / k_SegmentLength);
            var segmentAngle = 360f / segmentCount;
            var dashedPercentage = 0.7f;
            var currentAngle = 0f;
            for (var i = 0; i < segmentCount; i++)
            {
                painter.BeginPath();
                painter.Arc(new Vector2(width * 0.5f, height * 0.5f), width * 0.5f, currentAngle, currentAngle + segmentAngle * dashedPercentage);
                painter.Stroke();
                currentAngle += segmentAngle;
            }
        }
    }

    internal class DoodleLineOverlay : VisualElement
    {
        public Color lineColor = Color.white;
        public float lineWidth = 0.7f;

        IList<Vector2> m_Points = Array.Empty<Vector2>();

        public DoodleLineOverlay()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += GenerateVisualContent;
        }

        public void SetPoints(IList<Vector2> newPoints)
        {
            m_Points = newPoints;
        }

        void GenerateVisualContent(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.lineWidth = lineWidth;
            painter.lineCap = LineCap.Butt;

            painter.strokeColor = lineColor;
            painter.BeginPath();
            foreach (var point in m_Points)
                painter.LineTo(point);
            painter.ClosePath();
            painter.Stroke();
        }
    }

    internal interface IDoodlePad
    {
        event Action onDoodleStart;
        event Action onDoodleUpdate;
        event Action onDoodleEnd;

        bool IsActive { get; }
        DoodleModifierState modifierState { get; set; }
        Color brushColor { get; }
        float brushRadius { get; }
        Painter painter { get; }
        bool isClear { get; set; }
        byte[] value { get; set; }
        VisualElement visualElement { get; }
        void Activate(bool active);
        void SetNone();
        void SetBrush();
        void SetEraser();
        void SetMask();
        void SetBrushSize(float newBrushRadius);
        void SetDoodle(byte[] doodle);
        void SetBrushColor(Color newColor);
        void SetOutline(bool hasOutline);
        void SetDoodleSize(Vector2Int size);
        Vector2Int GetDoodleSize();
        void SetValueWithoutNotify(byte[] data);
    }

    internal class DoodlePad : VisualElement, INotifyValueChanged<byte[]>, IDisposable, IDoodlePad
    {
        public const string baseStyleName = "doodle-pad";
        public const string doodleCanvasStyleName = baseStyleName + "-canvas";
        public const string cursorStyleName = baseStyleName + "-cursor";
        public const string lineStyleName = baseStyleName + "-line";

        public event Action onDoodleStart;
        public event Action onDoodleUpdate;
        public event Action onDoodleEnd;

        public Action<DoodleModifierState> onModifierStateChanged;

        DoodleImage m_Image;

        DoodleModifierState m_ModifierState;

        bool m_IsActive;

        public bool IsActive => m_IsActive;

        public DoodleModifierState modifierState
        {
            get => m_ModifierState;
            set
            {
                m_ModifierState = value;
                UpdateDoodleCursorStyle();

                onModifierStateChanged?.Invoke(m_ModifierState);
            }
        }

        Color m_Color;
        public Color brushColor => m_Color;

        float m_BrushRadius;
        public float brushRadius => m_BrushRadius;

        Vector2Int m_Size;

        Vector2 m_CurrentDoodlePosition;
        List<Vector2> m_DoodlePositions = new List<Vector2>();

        DoodleCursorOverlay m_DoodleCursorOverlay;
        DoodleLineOverlay m_DoodleLineOverlay;
        bool m_IsPainting;

        Painter m_Painter;

        public Painter painter => m_Painter;

        const float k_MinDistanceToPaint = 5f;

        public bool isClear
        {
            get => m_Painter.isClear;
            set => m_Painter.SetClear(value);
        }

        public DoodlePad()
            : this(512, 512) { }

        public DoodlePad(int width, int height)
        {
            // styleSheets.Add(Resources.Load<StyleSheet>("uss/DoodlePad"));
            // AddToClassList(baseStyleName);

            m_Size = new Vector2Int(width, height);

            pickingMode = PickingMode.Ignore;

            m_Painter = new Painter(m_Size);
            m_Painter.paintColor = brushColor;

            m_Image = new DoodleImage(true)
            {
                image = m_Painter.texture,
                style =
                {
                    flexGrow = 1,
                    overflow = Overflow.Hidden
                }
            };
            m_Image.AddToClassList(doodleCanvasStyleName);
            m_Image.RegisterCallback<MouseDownEvent>(OnDoodleStart);
            m_Image.RegisterCallback<MouseUpEvent>(OnDoodleStop);
            m_Image.RegisterCallback<PointerLeaveEvent>(OnMousePointerLeave);
            m_Image.RegisterCallback<PointerCancelEvent>(OnMousePointerCancel);
            m_Image.RegisterCallback<PointerMoveEvent>(OnDoodleMove);

            Add(m_Image);

            m_DoodleCursorOverlay = new DoodleCursorOverlay
            {
                style = { display = DisplayStyle.None }
            };
            m_DoodleCursorOverlay.AddToClassList(cursorStyleName);
            Add(m_DoodleCursorOverlay);

            m_DoodleLineOverlay = new DoodleLineOverlay();
            m_DoodleLineOverlay.style.position = Position.Absolute;
            m_DoodleLineOverlay.StretchToParentSize();
            m_DoodleLineOverlay.AddToClassList(lineStyleName);
            Add(m_DoodleLineOverlay);
        }

        public VisualElement visualElement => this;

        public void Activate(bool active)
        {
            m_IsActive = active;

            m_Image.pickingMode = m_IsActive ? PickingMode.Position : PickingMode.Ignore;
            modifierState = m_IsActive ? modifierState : DoodleModifierState.None;
            pickingMode = m_IsActive ? PickingMode.Position : PickingMode.Ignore;
        }

        public DoodlePad(int width, int height, float opacity)
            : this(width, height)
        {
            m_Image.style.opacity = Mathf.Clamp01(opacity);
        }

        public void SetNone() => modifierState = DoodleModifierState.None;
        public void SetBrush() => modifierState = DoodleModifierState.Brush;
        public void SetEraser() => modifierState = DoodleModifierState.Erase;
        public void SetMask() => modifierState = DoodleModifierState.Mask;
        public void SetBucketFill() => modifierState = DoodleModifierState.BucketFill;

        public void SetBrushSize(float newBrushRadius)
        {
            m_BrushRadius = newBrushRadius;
            m_Painter.brushRadius = m_BrushRadius;
            UpdateDoodleCursorStyle();
        }

        public void SetDoodle(byte[] doodle)
        {
            SetValueWithoutNotify(doodle);

            SendValueChangedEvent();
        }

        public void SetBrushColor(Color newColor)
        {
            m_Color = newColor;
            m_Painter.paintColor = m_Color;
        }

        public void SetOutline(bool hasOutline)
        {
            m_Image.isOutlined = hasOutline;
        }

        public void InitializeWithData(byte[] data)
        {
            m_Painter.InitializeWithData(data);

            m_Image.image = m_Painter.texture;
            m_Image.MarkDirtyRepaint();
        }

        void OnMousePointerLeave(PointerLeaveEvent evt)
        {
            if (m_ModifierState == DoodleModifierState.None)
                return;
            m_DoodleCursorOverlay.style.display = DisplayStyle.None;
        }

        void OnMousePointerCancel(PointerCancelEvent evt)
        {
            if (m_ModifierState == DoodleModifierState.None)
                return;
            m_DoodleCursorOverlay.style.display = DisplayStyle.None;
        }

        void OnDoodleStart(MouseDownEvent evt)
        {
            if (m_ModifierState == DoodleModifierState.None)
                return;

            if (evt.altKey)
                return;

            m_CurrentDoodlePosition = evt.localMousePosition;
            m_DoodlePositions.Clear();
            m_DoodlePositions.Add(m_CurrentDoodlePosition);

            var currentPosition = GetPosition(m_CurrentDoodlePosition);

            switch (m_ModifierState)
            {
                case DoodleModifierState.Brush:
                case DoodleModifierState.Erase:
                case DoodleModifierState.Mask:
                    if (evt.button == 0)
                    {
                        m_IsPainting = true;
                        MouseCaptureController.CaptureMouse(m_Image);
                        if (m_ModifierState is DoodleModifierState.Brush)
                            m_Painter.Paint(currentPosition, currentPosition);
                        else if (m_ModifierState == DoodleModifierState.Erase)
                            m_Painter.Erase(currentPosition, currentPosition);
                        onDoodleStart?.Invoke();
                    }
                    break;
            }

            m_Image.MarkDirtyRepaint();

            // evt.StopPropagation();
        }

        void OnDoodleStop(MouseUpEvent evt)
        {
            if (m_ModifierState == DoodleModifierState.None)
                return;

            if (evt.button != 0)
                return;

            m_IsPainting = false;

            m_CurrentDoodlePosition = evt.localMousePosition;
            m_DoodlePositions.Add(m_CurrentDoodlePosition);
            m_DoodlePositions.Add(m_DoodlePositions[0]);

            m_Painter.UpdateTextureData();
            schedule.Execute(SendValueChangedEvent);

            if (m_ModifierState == DoodleModifierState.Mask)
            {
                for (var i = 0; i < m_DoodlePositions.Count; i++)
                    m_DoodlePositions[i] = GetPosition(m_DoodlePositions[i]);

                m_Painter.DoodleArea(m_DoodlePositions.ToArray());
                m_Painter.UpdateTextureData();
            }

            m_DoodlePositions.Clear();

            UpdateLineStyle();

            MouseCaptureController.ReleaseMouse(m_Image);
            onDoodleEnd?.Invoke();

            // evt.StopPropagation();
        }

        void SendValueChangedEvent()
        {
            using var evt = ChangeEvent<byte[]>.GetPooled(null, m_Painter.GetTextureData().EncodeToPNG());
            evt.target = this;
            SendEvent(evt);
        }

        void OnDoodleMove(PointerMoveEvent evt)
        {
            if (m_ModifierState == DoodleModifierState.None)
                return;

            m_CurrentDoodlePosition = evt.localPosition;
            UpdateDoodleCursorStyle();
            if (!m_IsPainting)
                return;

            if (m_ModifierState == DoodleModifierState.Mask)
            {
                m_DoodleLineOverlay.SetPoints(m_DoodlePositions);
                UpdateLineStyle();
            }

            var lastDoodlePosition = m_DoodlePositions[^1];
            var currentPosition = (Vector3)GetPosition(m_CurrentDoodlePosition);
            var previousPosition = (Vector3)GetPosition(lastDoodlePosition);
            if (Vector2.Distance(currentPosition, previousPosition) < k_MinDistanceToPaint)
                return;

            switch (m_ModifierState)
            {
                case DoodleModifierState.Brush:
                    m_Painter.Paint(previousPosition, currentPosition);
                    break;
                case DoodleModifierState.Erase:
                    m_Painter.Erase(previousPosition, currentPosition);
                    break;
            }
            m_DoodlePositions.Add(m_CurrentDoodlePosition);

            onDoodleUpdate?.Invoke();

            // evt.StopPropagation();
        }


        float GetBrushSize(float brushSize)
        {
            var size = m_Image.contentRect.size;
            var parentAspectRatio = size.x / size.y;
            var imageAspectRatio = (float)m_Size.x / m_Size.y;
            if (imageAspectRatio > parentAspectRatio)
            {
                // width match
                brushSize *= size.x / m_Size.x;
            }
            else
            {
                // height match
                brushSize *= size.y / m_Size.y;
            }

            return brushSize;
        }

        Vector2 GetPosition(Vector3 evtLocalPosition)
        {
            evtLocalPosition.y = (contentRect.height - evtLocalPosition.y) / contentRect.height;
            evtLocalPosition.x /= contentRect.width;
            return new Vector2(m_Size.x * evtLocalPosition.x, m_Size.y * evtLocalPosition.y);
        }

        void UpdateDoodleCursorStyle()
        {
            var isVisible = m_ModifierState != DoodleModifierState.None;
            var currentPos = (Vector3)GetPosition(m_CurrentDoodlePosition);
            var withinDoodlingArea = currentPos.x >= 0 && currentPos.x < m_Size.x && currentPos.y >= 0 && currentPos.y < m_Size.y;
            if (withinDoodlingArea)
            {
                var brushSize = m_ModifierState == DoodleModifierState.Mask ? 5f : m_BrushRadius;
                var doodleImageRadius = GetBrushSize(brushSize);

                m_DoodleCursorOverlay.style.position = Position.Absolute;
                m_DoodleCursorOverlay.style.top = m_CurrentDoodlePosition.y - doodleImageRadius + m_Image.resolvedStyle.paddingTop;
                m_DoodleCursorOverlay.style.left = m_CurrentDoodlePosition.x - doodleImageRadius;
                m_DoodleCursorOverlay.style.width = m_DoodleCursorOverlay.style.height = doodleImageRadius * 2;
                m_DoodleCursorOverlay.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else
                m_DoodleCursorOverlay.style.display = DisplayStyle.None;

            m_DoodleCursorOverlay.MarkDirtyRepaint();
        }

        void UpdateLineStyle()
        {
            var isVisible = m_IsPainting && m_ModifierState == DoodleModifierState.Mask;
            m_DoodleLineOverlay.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            m_DoodleLineOverlay.lineColor = m_Color;
            m_DoodleLineOverlay.MarkDirtyRepaint();
        }

        public void SetValueWithoutNotify(byte[] newValue)
        {
            InitializeWithData(newValue);
        }

        public byte[] value
        {
            get => m_Painter.GetTextureData().EncodeToPNG();
            set => SetDoodle(value);
        }

        public Texture2D texture
        {
            get => m_Painter.GetTextureData();
        }

        public Func<byte[], bool> validateValue { get; set; }

        public void SetDoodleSize(Vector2Int newSize)
        {
            m_Size = newSize;

            m_Painter.Resize(m_Size);

            m_Image.image = m_Painter.texture;
            m_Image.MarkDirtyRepaint();
        }

        public Vector2Int GetDoodleSize()
        {
            return m_Size;
        }

        public void Dispose()
        {
            m_Painter?.Dispose();
        }
    }
}
