using System;
using System.Collections.Generic;
using Unity.U2D.AI.Editor.AIBridge;
using Unity.U2D.AI.Editor.Styles;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.U2D.AI.Editor.Overlay
{
    internal interface IAIPanel : IDisposable
    {
        event Action OnImageReferencesUpdated;
        event Action<ImageControl, Texture2D> OnImageReferenceObjectFieldChangedEvent;
        event Action<ImageControl, bool> OnImageReferenceEditChangedEvent;

        event Action OnGenerateExt;
        event Action<EAIMode> OnAIModeChangedExt;
        event Action<ImageControl> OnDoodleChangedExt;
        event Action<ImageControl> OnActivateChangedExt;

        void SetAIBridge(IAIControlTypeBridge controlTypeBridge, IAIEditorBridge bridge);
        VisualElement visualElement { get; }
        IReadOnlyList<EControlType> ControlTypes { get; }
        bool requestShow { get; set; }
        ImageControl GetImageControl(EControlType type);
        void SetAIMode(EAIMode newMode);
        bool IsActive(ImageControl imageControl);
        void ToggleImageReferenceEdit(ImageControl type, bool isEditing);
        void SetDoodlePadData(ImageControl imageControl, byte[] data);
        void SetUnsavedAssetBytes(ImageControl imageControl, byte[] data);
        byte[] GetDoodlePadData(ImageControl imageControl);
        byte[] GetUnsavedAssetBytes(ImageControl imageControl);
    }

    namespace Unity.U2D.AI.Editor.Overlay
    {
        [Overlay(typeof(ISpriteEditor), overlayId, nameof(AIPanel),
            defaultLayout = Layout.Panel, defaultDockPosition = DockPosition.Top, defaultDockZone = DockZone.RightColumn,
            minWidth = 250, minHeight = 400, maxHeight = 1000, maxWidth = 1000, defaultWidth = 250, defaultHeight = 600)]
        internal class AIPanel : UnityEditor.Overlays.Overlay, ITransientOverlay, IAIPanel
        {
            public const string overlayId = "com.unity.2d.enhancers/AIOperatorOverlay";
            public const string uxmlPath = "Packages/com.unity.2d.enhancers/Editor/Overlays/AIPanel/AIPanel.uxml";

            public event Action OnImageReferencesUpdated;
            public event Action<ImageControl, Texture2D> OnImageReferenceObjectFieldChangedEvent;
            public event Action<ImageControl, bool> OnImageReferenceEditChangedEvent;

            public event Action OnGenerateExt;
            public event Action<EAIMode> OnAIModeChangedExt;
            public event Action<ImageControl> OnDoodleChangedExt;
            public event Action<ImageControl> OnActivateChangedExt;

            public VisualElement visualElement => m_Root;
            public IReadOnlyList<EControlType> ControlTypes => m_ControlTypes;

            List<EControlType> m_ControlTypes;
            Dictionary<EControlType, ImageControl> m_ImageControls;

            VisualElement m_AIPanel;
            VisualElement m_Root;
            bool m_RequestShow = false;

            EControlType? m_CurrentImageReferenceType;
            IAIEditorBridge m_AIEditorBridge;
            IAIControlTypeBridge m_ControlTypeBridge;

            void CreateAIOverlay()
            {
                displayName = TextContent.aiOverlayImageGenerationDisplayName;

                m_AIPanel = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath).CloneTree();
                StyleReference.AddSpriteEditorOverlayStyle(m_AIPanel);
                m_AIPanel.name = "AIOverlay";
                m_AIPanel.tooltip = TextContent.aiOverlayTooltip;
                rootVisualElement.tooltip = TextContent.aiOverlayTooltip;
            }

            void UpdateImageReferences()
            {
                var imageReferenceTypes = Enum.GetValues(typeof(EControlType));
                m_ImageControls = new Dictionary<EControlType, ImageControl>(imageReferenceTypes.Length);
                m_ControlTypes = new List<EControlType>(imageReferenceTypes.Length);
                foreach (EControlType controlType in imageReferenceTypes)
                {
                    m_ControlTypes.Add(controlType);
                    var mode = m_ControlTypeBridge.GetMode(controlType);
                    var ctx = m_ControlTypeBridge.GetImageReferenceElement(controlType, m_AIPanel);
                    var doodlePad = m_ControlTypeBridge.GetDoodlePadElement(controlType, ctx);

                    var supportsDoodlePad = doodlePad != null;
                    if (supportsDoodlePad)
                    {
                        var imageControl = new ImageControl(controlType, ctx);

                        var editButton = m_ControlTypeBridge.GetDoodlePadEditButton(controlType, ctx);
                        if (editButton != null)
                        {
                            if (mode != EAIMode.Generation)
                                editButton.style.display = DisplayStyle.None;
                            else
                            {
                                editButton.clicked += () =>
                                {
                                    ToggleImageReferenceEdit(imageControl, m_CurrentImageReferenceType != controlType);
                                };
                            }
                        }

                        if (mode == EAIMode.Generation || mode == EAIMode.Inpaint)
                        {
                            // Since AITK doesn't edit the UnSavedAssetBytes, we don't need to handle it.
                            //controlTypeBridge.AddUnsavedAssetBytesChangedHandler(controlType, ctx, bytes => { OnUnsavedAssetBytesChanged?.Invoke(imageControl, bytes); });
                            m_ControlTypeBridge.AddDoodleDataChangedHandler(controlType, ctx, _ => { OnDoodleChangedExt?.Invoke(imageControl); });
                            m_ControlTypeBridge.AddIsActiveChangedHandler(controlType, ctx, _ => { OnActivateChangedExt?.Invoke(imageControl); });

                            m_ImageControls[controlType] = imageControl;

                            // To handle a case where there is a single image reference.
                            if (mode != EAIMode.Inpaint)
                            {
                                var of = m_ControlTypeBridge.GetObjectFieldElement(controlType, ctx);
                                of?.RegisterValueChangedCallback(x => OnObjectFieldChanged(x.newValue, imageControl));
                            }
                        }
                    }
                }

                if (m_AIPanel != null && m_Root.Contains(m_AIPanel))
                    m_Root.Remove(m_AIPanel);

                m_Root.Add(m_AIPanel);

                OnImageReferencesUpdated?.Invoke();
            }

            public ImageControl GetImageControl(EControlType type) => m_ImageControls.GetValueOrDefault(type);

            public void SetAIBridge(IAIControlTypeBridge controlTypeBridge, IAIEditorBridge bridge)
            {
                m_AIEditorBridge?.Dispose();

                m_AIEditorBridge = bridge;

                m_AIEditorBridge.EnableReplaceBlankAsset(false);
                m_AIEditorBridge.EnableReplaceRefinementAsset(false);
                m_AIEditorBridge.AddAIModeHandler(newMode => OnAIModeChangedExt?.Invoke(newMode));
                m_AIEditorBridge.AddGenerationTriggeredHandler(() => OnGenerateExt?.Invoke());

                m_ControlTypeBridge?.Dispose();
                m_ControlTypeBridge = controlTypeBridge;
                UpdateImageReferences();
            }

            public void Dispose()
            {
                m_ControlTypeBridge?.Dispose();

                m_AIEditorBridge?.Dispose();
                m_AIEditorBridge?.EnableReplaceBlankAsset(true);
                m_AIEditorBridge?.EnableReplaceRefinementAsset(true);
            }

            public override void OnCreated()
            {
                displayName = TextContent.aiOverlayImageGenerationDisplayName;

                m_Root = new VisualElement
                {
                    style = { flexGrow = 1, flexShrink = 1 }
                };
                CreateAIOverlay();
                m_Root.Add(m_AIPanel);
                displayed = false;
                m_RequestShow = false;
            }

            public override void OnWillBeDestroyed()
            {
                Dispose();
                base.OnWillBeDestroyed();
            }

            public void ToggleImageReferenceEdit(ImageControl type, bool isEditing)
            {
                m_CurrentImageReferenceType = isEditing ? type?.controlType : null;
                OnImageReferenceEditChangedEvent?.Invoke(type, isEditing);
                foreach (var (controlType, imageControl) in m_ImageControls)
                {
                    var isSelected = isEditing && controlType == type?.controlType;
                    m_ControlTypeBridge.SetImageReferenceAsSelected(imageControl.controlType, imageControl.ctx, isSelected);
                }
            }

            void OnObjectFieldChanged(Object evt, ImageControl imageReferenceType)
            {
                if (evt is Texture2D texture)
                    OnImageReferenceObjectFieldChangedEvent?.Invoke(imageReferenceType, texture);
            }

            public override VisualElement CreatePanelContent()
            {
                if (displayed != m_RequestShow)
                    displayed = m_RequestShow;
                return m_Root;
            }

            public bool requestShow
            {
                get => m_RequestShow;
                set
                {
                    m_RequestShow = value;
                    displayed = m_RequestShow;
                }
            }

            public bool visible => m_RequestShow;

            public void SetAIMode(EAIMode newMode)
            {
                m_AIEditorBridge.SetAIMode(newMode);

                displayName = GetDisplayName(newMode);
            }

            static string GetDisplayName(EAIMode newMode)
            {
                switch (newMode)
                {
                    case EAIMode.Generation:
                        return TextContent.aiOverlayImageGenerationDisplayName;
                    case EAIMode.Inpaint:
                        return TextContent.aiOverlayInPaintDisplayName;
                    case EAIMode.Pixelate:
                        return TextContent.aiOverlayPixelateDisplayName;
                    case EAIMode.Recolor:
                        return TextContent.aiOverlayRecolorDisplayName;
                    case EAIMode.Upscale:
                        return TextContent.aiOverlayUpscaleDisplayName;
                    case EAIMode.RemoveBackground:
                        return TextContent.aiOverlayRemoveBackgroundDisplayName;
                }

                return string.Empty;
            }

            public void SetDoodlePadData(ImageControl imageControl, byte[] data)
            {
                m_ControlTypeBridge.SetDoodlePadData(imageControl.controlType, imageControl.ctx, data);
            }

            public void SetUnsavedAssetBytes(ImageControl imageControl, byte[] data)
            {
                m_ControlTypeBridge.SetUnsavedAssetBytes(imageControl.controlType, imageControl.ctx, data);
            }

            public byte[] GetDoodlePadData(ImageControl imageControl)
            {
                return m_ControlTypeBridge.GetDoodlePadData(imageControl.controlType, imageControl.ctx);
            }

            public byte[] GetUnsavedAssetBytes(ImageControl imageControl)
            {
                return m_ControlTypeBridge.GetUnsavedAssetBytes(imageControl.controlType, imageControl.ctx);
            }

            public bool IsActive(ImageControl imageControl)
            {
                return m_ControlTypeBridge.IsActive(imageControl.controlType, imageControl.ctx);
            }
        }
    }
}
