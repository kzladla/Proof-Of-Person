using System;
using System.IO;
using Unity.U2D.AI.Editor.AIBridge;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Events.UI;
using Unity.U2D.AI.Editor.ImageManipulation;
using Unity.U2D.AI.Editor.Overlay;
using Unity.U2D.AI.Editor.Prefs;
using Unity.U2D.AI.Editor.Undo;
using UnityEngine;

namespace Unity.U2D.AI.Editor
{
    interface IAIModuleController : IEventSender, IDisposable
    {
        event Action<bool> OnEnableEnhancers;
        event Action<Texture2D> OnSelectedImage;
        void SetOriginalTexture(Texture2D texture, Uri textureUri);
        Uri OriginalTexture { get; }
        Uri OverrideTexture { get; }
        EAIMode CurrentMode { get; }
        bool ShowOverlays { get; set; }
        void SelectOriginal();
        void UpdateImageReferenceDoodles();
    }

    internal class AIModuleController : IAIModuleController
    {
        public event Action<bool> OnEnableEnhancers;
        public event Action<Texture2D> OnSelectedImage;

        readonly IAIPanel m_AIPanel;
        readonly IGenerationResultOverlay m_GenerationResultOverlay;
        readonly IBrushToolSettingsOverlay m_BrushToolSettingsOverlay;
        readonly IImageManipulationCanvas m_ImageManipulationCanvas;
        readonly UIEventBus m_EventBus;
        readonly ISpriteAIUndo m_Undo;
        readonly IOperatorDoodleStorage m_OperatorDataStorage;

        ImageControl m_CurrentImageReferenceType = null;
        EAIMode m_CurrentMode = EAIMode.None;
        Uri m_OverrideTexture;
        Uri m_OriginalAssetUri;

        Texture2D m_OriginalTexture;
        Texture2D m_BaseTexture;
        bool m_ShowOverlays;

        public EAIMode CurrentMode => m_CurrentMode;
        public Uri OriginalTexture => m_OriginalAssetUri;
        public Uri OverrideTexture => m_OverrideTexture;

        public AIModuleController(
            IAIPanel aiPanel,
            IGenerationResultOverlay generationResultOverlay,
            IBrushToolSettingsOverlay brushToolSettings,
            IImageManipulationCanvas imageManipulationCanvas,
            UIEventBus eventBus,
            ISpriteAIUndo undo,
            IOperatorDoodleStorage operatorDataStorage)
        {
            m_AIPanel = aiPanel;
            m_GenerationResultOverlay = generationResultOverlay;
            m_BrushToolSettingsOverlay = brushToolSettings;
            m_ImageManipulationCanvas = imageManipulationCanvas;
            m_EventBus = eventBus;
            m_Undo = undo;
            m_OperatorDataStorage = operatorDataStorage;

            m_EventBus.AIModeChangeEvent += OnAIModeChangeFromUIEvent;
            m_EventBus.ImageManipulationToolChangeEvent += OnImageManipulationModeChange;
            m_EventBus.DoodleUpdateEvent += OnDoodleUpdate;
            m_EventBus.ClearDoodleRequstEvent += OnClearDoodleRequest;
            m_EventBus.BrushSettingsChangeEvent += OnSetBrushSettings;

            m_AIPanel.OnAIModeChangedExt += OnAIModeChangedExt;
            m_AIPanel.OnGenerateExt += OnGenerateExt;
            m_AIPanel.OnImageReferenceEditChangedEvent += OnImageReferenceEditChanged;
            m_AIPanel.OnImageReferenceObjectFieldChangedEvent += OnImageReferenceObjectFieldChanged;
            m_AIPanel.OnDoodleChangedExt += OnDoodleChanged;
            m_AIPanel.OnImageReferencesUpdated += UpdateImageReferenceDoodles;
            m_AIPanel.OnActivateChangedExt += OnImageReferenceActivateChanged;

            m_BrushToolSettingsOverlay.OnValueChanged += OnBrushSettingsChanged;

            m_GenerationResultOverlay.OnSelectionChanged += OnResultSelectURI;

            m_Undo.OnUndoRedo += OnUndoRedo;
        }

        public void Dispose()
        {
            m_EventBus.AIModeChangeEvent -= OnAIModeChangeFromUIEvent;
            m_EventBus.ImageManipulationToolChangeEvent -= OnImageManipulationModeChange;
            m_EventBus.DoodleUpdateEvent -= OnDoodleUpdate;
            m_EventBus.ClearDoodleRequstEvent -= OnClearDoodleRequest;
            m_EventBus.BrushSettingsChangeEvent -= OnSetBrushSettings;

            m_AIPanel.OnAIModeChangedExt -= OnAIModeChangedExt;
            m_AIPanel.OnGenerateExt -= OnGenerateExt;
            m_AIPanel.OnImageReferenceEditChangedEvent -= OnImageReferenceEditChanged;
            m_AIPanel.OnImageReferenceObjectFieldChangedEvent -= OnImageReferenceObjectFieldChanged;
            m_AIPanel.OnDoodleChangedExt -= OnDoodleChanged;
            m_AIPanel.OnImageReferencesUpdated -= UpdateImageReferenceDoodles;
            m_AIPanel.OnActivateChangedExt -= OnImageReferenceActivateChanged;

            m_BrushToolSettingsOverlay.OnValueChanged -= OnBrushSettingsChanged;

            m_GenerationResultOverlay.OnSelectionChanged -= OnResultSelectURI;

            m_Undo.OnUndoRedo -= OnUndoRedo;

            m_AIPanel?.Dispose();
            m_GenerationResultOverlay?.Dispose();
            m_ImageManipulationCanvas?.Dispose();
            m_OperatorDataStorage?.Dispose();

            m_BaseTexture?.SafeDestroy();
            m_BaseTexture = null;
        }

        public void SetOriginalTexture(Texture2D texture, Uri textureUri)
        {
            m_OriginalTexture = texture;
            m_OriginalAssetUri = textureUri;
        }

        public void UpdateImageReferenceDoodles()
        {
            foreach (var controlType in m_AIPanel.ControlTypes)
            {
                var imageControl = m_AIPanel.GetImageControl(controlType);
                if (imageControl != null)
                {
                    if (imageControl.controlType == EControlType.InPaintMaskImage)
                    {
                        var dataKey = new DoodleDataKey(imageControl, EDoodlePadMode.Overlay);
                        m_OperatorDataStorage.SetTexture(dataKey, m_AIPanel.GetDoodlePadData(imageControl), true);

                        // Note for BaseImage
                        // We can't grab the UnsavedAssetByte to restore the doodle history
                        // because it is a merged texture of the base image and the doodle
                    }
                    else
                    {
                        var dataKey = new DoodleDataKey(imageControl, EDoodlePadMode.BaseImage);
                        m_OperatorDataStorage.SetTexture(dataKey, m_AIPanel.GetDoodlePadData(imageControl), true);
                    }
                }
            }
        }

        public bool ShowOverlays
        {
            get => m_ShowOverlays;
            set
            {
                if (value != m_ShowOverlays)
                {
                    m_ShowOverlays = value;
                    m_BrushToolSettingsOverlay.requestShow = m_ShowOverlays;
                    m_AIPanel.requestShow = m_ShowOverlays;
                    m_GenerationResultOverlay.requestShow = m_ShowOverlays;
                }
            }
        }

        public void SelectOriginal()
        {
            if (m_OverrideTexture == null)
                m_GenerationResultOverlay.SetGenerationSelection(m_OriginalAssetUri);
        }

        void OnAIModeChangedExt(EAIMode mode)
        {
            if (m_CurrentMode == mode)
                return;

            m_EventBus.SendEvent(new AIModeChangeEvent(this)
            {
                mode = mode
            });
        }

        void OnAIModeChangeFromUIEvent(AIModeChangeEvent evt)
        {
            if (m_CurrentMode == evt.mode)
                return;

            if (evt.sender != this)
            {
                if (evt.mode != EAIMode.None)
                {
                    m_Undo.SetAIModeUndo(evt.mode);

                    // We clear away any image manipulation tool selection undo since they might not be valid
                    m_Undo.ClearImageManipulationToolUndo();
                    m_Undo.ClearImageReferenceActiveEditUndo();
                }
            }

            SetAIMode(evt.mode);
        }

        void SetAIMode(EAIMode mode)
        {
            m_CurrentMode = mode;

            m_BrushToolSettingsOverlay.ClearMiscElements();
            m_Undo.ClearDoodleUndo();

            var validMode = mode != EAIMode.None;
            if (validMode)
            {
                m_AIPanel.SetAIMode(mode);
                m_GenerationResultOverlay.SetGenerationSelection(m_OverrideTexture ?? m_OriginalAssetUri);

                if (mode == EAIMode.Inpaint)
                {
                    m_AIPanel.ToggleImageReferenceEdit(m_AIPanel.GetImageControl(EControlType.InPaintMaskImage), true);
                }
                else
                {
                    m_AIPanel.ToggleImageReferenceEdit(null, false);
                    m_ImageManipulationCanvas.ClearDoodleData(EDoodlePadMode.BaseImage);
                }

                if (mode == EAIMode.Generation)
                {
                    var showSpriteToggle = new ShowSpriteTool();
                    showSpriteToggle.RegisterEventBus(m_EventBus);
                    m_BrushToolSettingsOverlay.AddMiscElements(showSpriteToggle);
                }
            }

            OnEnableEnhancers?.Invoke(validMode);

            m_EventBus.SendEvent(new ImageManipulationModeChangeEvent(this)
            {
                mode = EImageManipulationMode.None
            }, true);
            m_EventBus.SendEvent(new ShowImageManipulationToolEvent(this)
            {
                mode = m_CurrentMode,
                imageControl = m_CurrentImageReferenceType
            }, true);
        }

        void OnGenerateExt()
        {
            m_EventBus.SendEvent(new GenerateEvent(this));
            if (m_CurrentImageReferenceType != null && m_CurrentMode == EAIMode.Generation)
                m_AIPanel.ToggleImageReferenceEdit(m_CurrentImageReferenceType, false);
        }

        void OnImageManipulationModeChange(ImageManipulationModeChangeEvent evt)
        {
            // TODO: fix undo for other mode change
            var hasValidMode = evt.mode
                is EImageManipulationMode.Doodle
                or EImageManipulationMode.InpaintMask
                or EImageManipulationMode.Eraser
                or EImageManipulationMode.InpaintEraser;
            if (hasValidMode)
                m_Undo.SetImageManipulationToolUndo(evt.mode);

            var hasVisibleElements = evt.mode
                is EImageManipulationMode.Doodle
                or EImageManipulationMode.Eraser
                or EImageManipulationMode.InpaintEraser;

            m_BrushToolSettingsOverlay.hasValidMode = m_CurrentImageReferenceType != null && hasVisibleElements;
        }

        void SetOperatorDoodlePadData(byte[] data)
        {
            if (m_CurrentImageReferenceType != null)
            {
                m_AIPanel.OnDoodleChangedExt -= OnDoodleChanged;
                m_AIPanel.SetDoodlePadData(m_CurrentImageReferenceType, data);
                m_AIPanel.OnDoodleChangedExt += OnDoodleChanged;
            }
        }

        void UpdateInpaintBaseImageOperator(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                // TODO: this is extremely slow...
                var t = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                t.alphaIsTransparency = true;
                t.LoadImage(data);
                var combined = Utilities.CombineTexture(new[] { m_BaseTexture ?? m_OriginalTexture, t }, m_OperatorDataStorage.originalTexture.width, m_OperatorDataStorage.originalTexture.height);
                var combinedData = combined.EncodeToPNG();
                t.SafeDestroy();
                combined.SafeDestroy();
                SetUnsavedAssetBytes(combinedData);
            }
        }

        void SetUnsavedAssetBytes(byte[] data)
        {
            if (m_CurrentImageReferenceType != null && data != null && data.Length > 0)
            {
                m_AIPanel.SetUnsavedAssetBytes(m_CurrentImageReferenceType, data);
            }
        }

        void OnDoodleUpdate(DoodleUpdateEvent evt)
        {
            if (evt.sender == this)
                return;

            if (m_CurrentImageReferenceType == null)
                return;

            var data = evt.textureData;
            DoodleDataKey dataKey = null;
            if (m_CurrentMode == EAIMode.Inpaint)
            {
                if (evt.doodleMode == EDoodlePadMode.Overlay)
                {
                    dataKey = new DoodleDataKey(m_CurrentImageReferenceType, EDoodlePadMode.Overlay);
                    SetOperatorDoodlePadData(data);
                }
                else if (evt.doodleMode == EDoodlePadMode.BaseImage)
                {
                    dataKey = new DoodleDataKey(m_CurrentImageReferenceType, EDoodlePadMode.BaseImage);
                    UpdateInpaintBaseImageOperator(data);
                }
            }
            else
            {
                dataKey = new DoodleDataKey(m_CurrentImageReferenceType, evt.doodleMode);
                SetOperatorDoodlePadData(data);
            }

            if (evt.sender == m_Undo)
            {
                // event is sent by undo. We need to update the image manipulation canvas
                m_ImageManipulationCanvas.SetDoodleData(data, evt.doodleMode);
            }

            SetDoodleTextureWithUndo(dataKey, data, true);
        }

        void OnClearDoodleRequest(ClearDoodleRequestEvent evt)
        {
            var doodleMode = m_ImageManipulationCanvas.doodlePadMode;
            var dataKey = new DoodleDataKey(m_CurrentImageReferenceType, doodleMode);
            var t = m_OperatorDataStorage.GetClearTexture();
            SetDoodleTextureWithUndo(dataKey, t.data, false);

            m_EventBus.SendEvent(new DoodleUpdateEvent(this)
            {
                textureData = t.data,
                doodleMode = doodleMode
            }, queue: true);

            if (m_CurrentImageReferenceType?.controlType == EControlType.InPaintMaskImage)
            {
                if (doodleMode == EDoodlePadMode.BaseImage)
                    UpdateInpaintBaseImageOperator(t.data);
                else if (doodleMode == EDoodlePadMode.Overlay)
                    SetOperatorDoodlePadData(t.data);
            }
            else
                SetOperatorDoodlePadData(t.data);
        }

        void SetDoodleTextureWithUndo(DoodleDataKey dataKey, byte[] data, bool doodled)
        {
            InitDoodleUndo(dataKey);
            m_OperatorDataStorage.SetTexture(dataKey, data, doodled);
            var previousData = m_OperatorDataStorage.GetTexture(dataKey);
            m_Undo.SetDoodleUndo(new DoodleUndoData()
            {
                doodleDataKey = dataKey,
                doodleData = previousData.data
            });
        }

        void InitDoodleUndo(DoodleDataKey dataKey)
        {
            if (!m_Undo.HasDoodleUndo(dataKey))
            {
                var previousData = m_OperatorDataStorage.GetTexture(dataKey);
                m_Undo.InitDoodleUndo(new DoodleUndoData()
                {
                    doodleDataKey = dataKey,
                    doodleData = previousData?.data
                });
            }
        }

        void OnSetBrushSettings(BrushSettingsChangeEvent evt)
        {
            m_BrushToolSettingsOverlay.SetValueWithoutNotify(evt.brushSettings);
        }

        void OnImageReferenceEditChanged(ImageControl imageControl, bool active)
        {
            m_Undo.ClearDoodleUndo();
            m_Undo.SetImageReferenceActiveEditUndo(new ImageReferenceActiveEditUndoData()
            {
                imageControl = imageControl,
                isEditting = active
            });

            var currentImageControl = m_CurrentImageReferenceType;
            if (active)
            {
                m_CurrentImageReferenceType = imageControl;
                if (m_CurrentImageReferenceType.controlType == EControlType.InPaintMaskImage)
                {
                    var dataKey = new DoodleDataKey(m_CurrentImageReferenceType, EDoodlePadMode.BaseImage);
                    var data = m_OperatorDataStorage.GetTexture(dataKey);
                    UpdateInpaintBaseImageOperator(data.data);
                }
            }
            else
            {
                if (m_CurrentImageReferenceType == imageControl || imageControl == null)
                {
                    m_CurrentImageReferenceType = null;
                    m_ImageManipulationCanvas.StopDoodling(EDoodlePadMode.Overlay);
                    m_ImageManipulationCanvas.StopDoodling(EDoodlePadMode.BaseImage);
                }
            }

            if (currentImageControl != m_CurrentImageReferenceType)
            {
                m_EventBus.SendEvent(new ShowImageManipulationToolEvent(this)
                {
                    mode = m_CurrentMode,
                    imageControl = m_CurrentImageReferenceType
                });
                if (m_CurrentMode == EAIMode.Generation && m_CurrentImageReferenceType != null)
                {
                    m_EventBus.SendEvent(new ShowSpriteChangeEvent(this)
                    {
                        showBackground = ModulePreferences.ShowSprite,
                        backgroundAlpha = ModulePreferences.ShowSpriteOpacity
                    });
                }
                else
                {
                    m_EventBus.SendEvent(new ShowSpriteChangeEvent(this)
                    {
                        showBackground = true,
                        backgroundAlpha = 1.0f
                    });
                }

                var baseTexture = m_OperatorDataStorage.GetTexture(new DoodleDataKey(m_CurrentImageReferenceType, EDoodlePadMode.BaseImage));
                m_ImageManipulationCanvas.SetDoodleData(baseTexture?.data, EDoodlePadMode.BaseImage);

                if (m_CurrentMode == EAIMode.Inpaint)
                {
                    UpdateInpaintBaseImageOperator(baseTexture?.data);
                }

                var maskTexture = m_OperatorDataStorage.GetTexture(new DoodleDataKey(m_CurrentImageReferenceType, EDoodlePadMode.Overlay));
                m_ImageManipulationCanvas.SetDoodleData(maskTexture?.data, EDoodlePadMode.Overlay);
            }
        }

        void OnImageReferenceObjectFieldChanged(ImageControl imageControl, Texture2D texture)
        {
            var t = Utilities.GetReadableTexture(texture);
            if (t == null)
                return;

            var data = t.EncodeToPNG();
            SetOperatorDoodlePadData(data);
            var doodlePadType = imageControl.controlType == EControlType.InPaintMaskImage ? EDoodlePadMode.Overlay : EDoodlePadMode.BaseImage;
            var dataKey = new DoodleDataKey(imageControl, doodlePadType);
            m_OperatorDataStorage.SetTexture(dataKey, data, true);
            if (m_CurrentImageReferenceType == imageControl)
            {
                m_ImageManipulationCanvas.SetDoodleData(data, doodlePadType);
            }

            t.SafeDestroy();
        }

        void OnDoodleChanged(ImageControl imageControl)
        {
            var doodlePadType = imageControl.controlType == EControlType.InPaintMaskImage ? EDoodlePadMode.Overlay : EDoodlePadMode.BaseImage;
            var doodleData = m_AIPanel.GetDoodlePadData(imageControl);
            if (m_CurrentImageReferenceType == imageControl)
            {
                m_ImageManipulationCanvas.SetDoodleData(doodleData, doodlePadType);
            }

            m_OperatorDataStorage.SetTexture(new DoodleDataKey(imageControl, doodlePadType), doodleData, true);
        }

        void OnImageReferenceActivateChanged(ImageControl imageControl)
        {
            var active = m_AIPanel.IsActive(imageControl);
            var baseKey = new DoodleDataKey(imageControl, EDoodlePadMode.BaseImage);
            var overlayKey = new DoodleDataKey(imageControl, EDoodlePadMode.Overlay);

            if (active)
            {
                if (imageControl.controlType == EControlType.InPaintMaskImage)
                {
                    if (!m_OperatorDataStorage.HasTexture(overlayKey))
                    {
                        var data = m_AIPanel.GetDoodlePadData(imageControl);
                        if (data != null)
                            m_OperatorDataStorage.SetTexture(overlayKey, data, true);
                    }

                    if (!m_OperatorDataStorage.HasTexture(baseKey))
                    {
                        var data = m_AIPanel.GetUnsavedAssetBytes(imageControl);
                        if (data != null)
                            m_OperatorDataStorage.SetTexture(baseKey, data, true);
                    }
                }
                else
                {
                    if (!m_OperatorDataStorage.HasTexture(baseKey))
                    {
                        var data = m_AIPanel.GetDoodlePadData(imageControl);
                        if (data != null)
                            m_OperatorDataStorage.SetTexture(baseKey, data, true);
                    }
                }
            }
            else
            {
                if (m_CurrentImageReferenceType == imageControl)
                {
                    m_AIPanel.ToggleImageReferenceEdit(imageControl, false);
                    m_Undo.ClearImageManipulationToolUndo();
                    m_Undo.ClearImageReferenceActiveEditUndo();
                }

                m_OperatorDataStorage.DeleteTexture(baseKey);
                m_OperatorDataStorage.DeleteTexture(overlayKey);
            }
        }

        void OnBrushSettingsChanged(BrushSettingsData newSettings)
        {
            m_EventBus.SendEvent(new BrushSettingsChangeEvent(this) { brushSettings = newSettings });
        }

        void OnResultSelectURI(Uri uri)
        {
            if (uri == null)
                return;
            var modeIsStillActive = m_ImageManipulationCanvas != null;
            if (!modeIsStillActive)
                return;
            var currentAsset = m_OverrideTexture != null ? m_OverrideTexture : m_OriginalAssetUri;
            if (uri == currentAsset)
                return;
            try
            {
                var texture = m_OriginalTexture;
                m_OverrideTexture = uri == m_OriginalAssetUri ? null : uri;
                if (m_BaseTexture == null)
                {
                    m_BaseTexture = new Texture2D(1, 1)
                    {
                        name = "com.unity.2d.enhancers m_BaseTexture",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }

                if (m_OverrideTexture != null)
                {
                    m_BaseTexture.LoadImage(File.ReadAllBytes(m_OverrideTexture.LocalPath));
                    texture = m_BaseTexture;
                }
                else
                {
                    m_BaseTexture.SafeDestroy();
                    m_BaseTexture = null;
                }

                m_OperatorDataStorage.SetSize(new Vector2Int(texture.width, texture.height));

                if (m_CurrentMode == EAIMode.Inpaint)
                {
                    var dataKey = new DoodleDataKey(m_CurrentImageReferenceType, EDoodlePadMode.BaseImage);
                    var texData = m_OperatorDataStorage.GetTexture(dataKey);
                    UpdateInpaintBaseImageOperator(texData.data);
                }

                OnSelectedImage?.Invoke(texture);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void OnUndoRedo(ImageReferenceActiveEditUndoData data)
        {
            ImageControl imageControl = null;
            if (data.haveValidControlType && m_AIPanel.GetImageControl(data.controlType) is { } imgControl)
                imageControl = imgControl;
            m_AIPanel.ToggleImageReferenceEdit(imageControl, data.isEditting);
        }
    }
}
