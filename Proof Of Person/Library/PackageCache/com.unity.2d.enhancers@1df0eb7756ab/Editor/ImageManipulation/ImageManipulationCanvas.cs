using System;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Events.UI;
using UnityEditor.U2D.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    enum EDoodlePadMode
    {
        BaseImage,
        Overlay,
        Count
    }

    interface IImageManipulationCanvas : IDisposable
    {
        event Action<Vector2> OnMove;
        event Action<float> OnWheel;

        Vector2Int size { get; }
        EDoodlePadMode doodlePadMode { get; }
        void SetOriginalTexture(Texture2D t);
        public Texture2D OriginalTexture { get; }
        void UpdateTransform(Vector2 scrollPosition, float zoom);
        void SetDoodleData(byte[] data, EDoodlePadMode mode);
        byte[] GetDoodleData(EDoodlePadMode mode);
        void StopDoodling(EDoodlePadMode mode);
        void ClearDoodleData(EDoodlePadMode mode);
        VisualElement visualElement { get; }
    }

    class ImageManipulationCanvas : VisualElement, IEventSender, IImageManipulationCanvas
    {
        public event Action<Vector2> OnMove;
        public event Action<float> OnWheel;

        public Vector2Int size { get; private set; }
        public Texture2D OriginalTexture { get; private set; }

        SpriteRectElement m_CanvasHighlightElement;
        UIEventBus m_EventBus;
        IDoodlePadsContainer m_DoodlePads;

        SpriteImageManipulationContext m_ShortcutContext;

        BrushSettings m_BrushSettings;

        public ImageManipulationCanvas(UIEventBus eventBus, Texture2D originalTexture, IDoodlePadsContainer doodlePadsContainer)
        {
            style.position = Position.Absolute;
            style.overflow = Overflow.Hidden;
            style.top = style.bottom = style.left = style.right = 0;
            pickingMode = PickingMode.Ignore;
            m_CanvasHighlightElement = new SpriteRectElement(Vector2.one * 2, Vector2Int.one * 2)
            {
                pickingMode = PickingMode.Ignore,
                name = "canvas-highlight",
                style =
                {
                    flexGrow = 0,
                    flexShrink = 0,
                    overflow = Overflow.Hidden
                }
            };
            m_CanvasHighlightElement.selected = true;
            Add(m_CanvasHighlightElement);

            m_DoodlePads = doodlePadsContainer;
            m_DoodlePads.Initialize(m_CanvasHighlightElement, m_CanvasHighlightElement.size);

            m_DoodlePads[EDoodlePadMode.BaseImage].onDoodleEnd += OnBaseDoodleChanged;
            m_DoodlePads[EDoodlePadMode.BaseImage].SetOutline(false);

            m_DoodlePads[EDoodlePadMode.Overlay].onDoodleEnd += OnDoodleChanged;
            m_DoodlePads[EDoodlePadMode.Overlay].SetOutline(true);

            m_EventBus = eventBus;
            m_EventBus.ImageManipulationToolChangeEvent += OnImageManipulationToolChangeEvent;
            m_EventBus.BrushSettingsChangeEvent += OnBrushSettingsChange;
            m_EventBus.DoodleUpdateEvent += OnDoodleUpdate;
            m_EventBus.ShowSpriteChangeEvent += OnShowSpriteChangeEvent;

            SetOriginalTexture(originalTexture);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<WheelEvent>(OnWheelValueChanged);

            m_ShortcutContext = new SpriteImageManipulationContext();
            InternalEditorBridge.RegisterShortcutContext(m_ShortcutContext);
            Shortcuts.OnBrushSizeIncrease += OnBrushSizeIncrease;
            Shortcuts.OnBrushSizeDecrease += OnBrushSizeDecrease;
        }

        public void Dispose()
        {
            m_EventBus.ImageManipulationToolChangeEvent -= OnImageManipulationToolChangeEvent;
            m_EventBus.ShowSpriteChangeEvent -= OnShowSpriteChangeEvent;

            InternalEditorBridge.UnregisterShortcutContext(m_ShortcutContext);
            Shortcuts.OnBrushSizeIncrease -= OnBrushSizeIncrease;
            Shortcuts.OnBrushSizeDecrease -= OnBrushSizeDecrease;
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            bool panMode = (!evt.altKey && (evt.pressedButtons & 1 << 1) > 0 || evt.altKey && (evt.pressedButtons & 1) > 0);
            if (!panMode)
                return;

            OnMove?.Invoke(evt.deltaPosition);
        }

        void OnWheelValueChanged(WheelEvent evt)
        {
            OnWheel?.Invoke(evt.delta.y * 0.05f);
        }

        void OnShowSpriteChangeEvent(ShowSpriteChangeEvent evt)
        {
            SetBackgroundAlpha(evt.backgroundAlpha);
            ShowBackground(evt.showBackground);
        }

        void OnDoodleUpdate(DoodleUpdateEvent evt)
        {
            if (evt.sender == this)
                return;

            m_DoodlePads[evt.doodleMode].SetDoodle(evt.textureData);
        }

        void OnBrushSettingsChange(BrushSettingsChangeEvent evt)
        {
            var newBrushSettings = evt.brushSettings;

            m_BrushSettings.data = newBrushSettings;
            m_DoodlePads.doodlePad.SetBrushSize(m_BrushSettings.GetAdjustedSize(size));
            m_DoodlePads.doodlePad.SetBrushColor(m_BrushSettings.data.color);
        }

        public void SetOriginalTexture(Texture2D t)
        {
            OriginalTexture = t;
            size = new Vector2Int(t.width, t.height);
            m_CanvasHighlightElement.UpdateSize(new Rect(Vector2.zero, size));
            var doodlePad = m_DoodlePads[EDoodlePadMode.BaseImage].visualElement;
            doodlePad.style.backgroundImage = OriginalTexture;

            m_BrushSettings = new BrushSettings("Default", BrushSettings.DefaultBaseColor);
            for (var i = 0; i < m_DoodlePads.Count; ++i)
            {
                m_DoodlePads[i].SetDoodleSize(size);
                m_DoodlePads[i].SetBrushSize(m_BrushSettings.GetAdjustedSize(size));
            }
        }

        public void SetDoodleData(byte[] data, EDoodlePadMode mode)
        {
            m_DoodlePads[mode].SetValueWithoutNotify(data);
        }

        public byte[] GetDoodleData(EDoodlePadMode mode)
        {
            return m_DoodlePads[mode]?.value;
        }

        public VisualElement visualElement => this;

        public void ClearDoodleData(EDoodlePadMode mode)
        {
            m_DoodlePads[mode].SetDoodle(null);
        }

        public void StopDoodling(EDoodlePadMode mode)
        {
            var doodlePad = m_DoodlePads[mode];
            doodlePad.SetNone();
            doodlePad.visualElement.MarkDirtyRepaint();
        }

        void OnDoodleChanged()
        {
            m_EventBus.SendEvent(new DoodleUpdateEvent(this)
            {
                textureData = m_DoodlePads[EDoodlePadMode.Overlay].value,
                doodleMode = EDoodlePadMode.Overlay
            });
        }

        void OnBaseDoodleChanged()
        {
            m_EventBus.SendEvent(new DoodleUpdateEvent(this)
            {
                textureData = m_DoodlePads[EDoodlePadMode.BaseImage].value,
                doodleMode = EDoodlePadMode.BaseImage
            });
        }

        public EDoodlePadMode doodlePadMode => m_DoodlePads.doodleMode;

        void SetBackgroundAlpha(float alpha)
        {
            var doodlePad = m_DoodlePads[EDoodlePadMode.BaseImage].visualElement;
            doodlePad.style.unityBackgroundImageTintColor = new Color(1, 1, 1, alpha);
        }

        void ShowBackground(bool show)
        {
            var doodlePad = m_DoodlePads[EDoodlePadMode.BaseImage].visualElement;
            doodlePad.style.backgroundImage = show ? OriginalTexture : null;
        }

        void OnImageManipulationToolChangeEvent(ImageManipulationModeChangeEvent evt)
        {
            if (evt.mode != EImageManipulationMode.None)
                m_DoodlePads.doodleMode = evt.doodleMode;

            if (evt.mode == EImageManipulationMode.Doodle)
                m_DoodlePads[EDoodlePadMode.BaseImage].SetBrush();
            else if (evt.mode == EImageManipulationMode.InpaintMask)
                m_DoodlePads[EDoodlePadMode.Overlay].SetMask();
            else if (evt.mode == EImageManipulationMode.Eraser)
                m_DoodlePads[EDoodlePadMode.BaseImage].SetEraser();
            else if (evt.mode == EImageManipulationMode.InpaintEraser)
                m_DoodlePads[EDoodlePadMode.Overlay].SetEraser();
            else
                m_DoodlePads[evt.doodleMode].SetNone();

            SetBrushSettings(evt.mode, evt.doodleMode);
        }

        public void UpdateTransform(Vector2 offset, float scale)
        {
            var center = contentRect.center;
            m_CanvasHighlightElement.UpdatePositionAndScale(center, offset, scale);
        }

        void OnBrushSizeIncrease()
        {
            if (m_BrushSettings == null)
                return;

            m_BrushSettings.IncreaseSize();
            m_EventBus.SendEvent(new BrushSettingsChangeEvent(this) { brushSettings = m_BrushSettings.data });
        }

        void OnBrushSizeDecrease()
        {
            if (m_BrushSettings == null)
                return;

            m_BrushSettings.DecreaseSize();
            m_EventBus.SendEvent(new BrushSettingsChangeEvent(this) { brushSettings = m_BrushSettings.data });
        }

        void SetBrushSettings(EImageManipulationMode mode, EDoodlePadMode doodleMode)
        {
            var canHaveColor = mode
                is EImageManipulationMode.Doodle;
            var canHaveBrushSize = mode
                is EImageManipulationMode.Doodle
                or EImageManipulationMode.Eraser
                // or EImageManipulationMode.Inpaint
                or EImageManipulationMode.InpaintEraser;

            var brushName = $"{mode}.{doodleMode}";
            var newBrushSettings = new BrushSettings(
                brushName,
                doodleMode == EDoodlePadMode.BaseImage ? BrushSettings.DefaultBaseColor : BrushSettings.DefaultOverlayColor,
                canModifySize: canHaveBrushSize,
                canModifyColor: canHaveColor);
            newBrushSettings.TryLoadFromEditorPrefs();

            m_BrushSettings?.SaveToEditorPrefs();

            m_BrushSettings = newBrushSettings;

            if (m_BrushSettings != null)
                m_EventBus.SendEvent(new BrushSettingsChangeEvent(this) { brushSettings = m_BrushSettings.data });
        }
    }
}
