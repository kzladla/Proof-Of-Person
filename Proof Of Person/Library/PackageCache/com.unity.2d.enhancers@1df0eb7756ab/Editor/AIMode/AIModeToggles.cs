using System;
using System.Collections.Generic;
using Unity.U2D.AI.Editor.AIBridge;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Events.UI;
using Unity.U2D.AI.Editor.ImageManipulation;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEditor.U2D.Common;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor
{
    class AIModeToggle : SpriteFrameModeToolStripBase, IEventSender
    {
        OverlayToolbar m_Toolbar;
        ISpriteEditor m_SpriteEditor;
        SpriteFrameAIModule m_SpriteAiEditor;
        EAIMode m_Mode = EAIMode.None;
        ImageManipulationToolStrip m_ImageManipulationToolStrip;

        (string iconPath, string iconOnPath, string tooltip, EAIMode mode, EditorToolbarToggle toggle)[] m_AIModeData = {
            ("SpriteGenerationMode@2x.png", "SpriteGenerationMode On@2x.png", "Generate", mode: EAIMode.Generation, null),
            ("SpriteRefinementMode@2x.png", "SpriteRefinementMode On@2x.png", "Refine", mode: EAIMode.Inpaint, null),
            ("UpscaleMode@2x.png", "UpscaleMode On@2x.png", "Upscale", mode: EAIMode.Upscale, null),
            ("RemoveBackgroundMode@2x.png", "RemoveBackgroundMode On@2x.png", "Remove background", mode: EAIMode.RemoveBackground, null),
            ("PixelateMode@2x.png", "PixelateMode On@2x.png", "Pixelate", mode: EAIMode.Pixelate, null),
            ("RecolorMode@2x.png", "RecolorMode On@2x.png", "Recolor", mode: EAIMode.Recolor, null),
        };

        const string k_IconPath = "Packages/com.unity.2d.enhancers/Editor/PackageResources/icons/Modes";

        public AIModeToggle()
        {
            m_Toolbar = new OverlayToolbar();

            var iconDictionary = new Dictionary<EAIMode, Texture2D>();
            for (var i = 0; i < m_AIModeData.Length; ++i)
            {
                iconDictionary[m_AIModeData[i].mode] = Utilities.LoadIconDark(k_IconPath, m_AIModeData[i].iconPath);
                var offIcon = Utilities.LoadIcon(k_IconPath, m_AIModeData[i].iconPath);
                var onIcon = Utilities.LoadIcon(k_IconPath, m_AIModeData[i].iconOnPath);
                var toggle = new EditorToolbarToggle
                {
                    offIcon = offIcon,
                    onIcon = onIcon,
                    tooltip = m_AIModeData[i].tooltip
                };
                var mode = m_AIModeData[i].mode;
                toggle.RegisterValueChangedCallback(value => OnToggle(mode, value.newValue));
                m_AIModeData[i].toggle = toggle;
                m_Toolbar.Add(toggle);
            }

            EditorToolbarUtility.SetupChildrenAsButtonStrip(m_Toolbar);

            m_ImageManipulationToolStrip = new ImageManipulationToolStrip(iconDictionary);
        }

        void OnToggle(EAIMode mode, bool value)
        {
            if (value == true && !m_SpriteAiEditor.moduleModeActivated)
            {
                // We have not been activated. Inform overlay we want to be activated.
                // SpriteFrameModeToggled will be called and we will then set our mode then.
                m_Mode = mode;
                ActivateSpriteFrameModeTool();
            }
            else
            {
                if (value)
                {
                    m_Mode = mode;
                    SetAIMode(mode);
                }
                else
                {
                    // Todo: Use proper radio toggle buttons
                    for (int i = 0; i < m_AIModeData.Length; ++i)
                    {
                        if (mode == m_AIModeData[i].mode)
                        {
                            m_AIModeData[i].toggle.SetValueWithoutNotify(true);
                            break;
                        }
                    }
                }
            }

        }

        void SetAIMode(EAIMode mode)
        {
            for (int i = 0; i < m_AIModeData.Length; ++i)
            {
                if (mode != m_AIModeData[i].mode)
                    m_AIModeData[i].toggle.SetValueWithoutNotify(false);
                else
                {
                    m_AIModeData[i].toggle.SetValueWithoutNotify(true);
                    m_SpriteAiEditor.uiEventBus.SendEvent(new AIModeChangeEvent(this)
                    {
                        mode = mode
                    });
                }
            }
        }

        public void SetAIModeNoNotify(EAIMode mode)
        {
            for (int i = 0; i < m_AIModeData.Length; ++i)
            {
                if (mode != m_AIModeData[i].mode)
                    m_AIModeData[i].toggle.SetValueWithoutNotify(false);
                else
                {
                    m_AIModeData[i].toggle.SetValueWithoutNotify(true);
                }
            }
        }

        protected override bool SpriteFrameModeToggled(SpriteFrameModeToolStripBase value)
        {
            if (this != value)
            {
                for (int i = 0; i < m_AIModeData.Length; ++i)
                {
                    m_AIModeData[i].toggle.SetValueWithoutNotify(false);
                }
                m_SpriteAiEditor.uiEventBus.SendEvent(new AIModeChangeEvent(this)
                {
                    mode = EAIMode.None
                });
                return false;
            }

            SetAIMode(m_Mode);
            return true;
        }

        public override VisualElement[] GetUIContent(Layout overlayLayout)
        {
            if (overlayLayout is Layout.HorizontalToolbar or Layout.Panel)
                m_Toolbar.style.flexDirection = FlexDirection.Row;
            else
                m_Toolbar.style.flexDirection = FlexDirection.Column;
            m_ImageManipulationToolStrip.SetLayout(overlayLayout);

            return new VisualElement[]{m_Toolbar, m_ImageManipulationToolStrip};
        }

        public override int order => 1;
        public override bool OverlayActivated(SpriteEditorFrameModuleModeBase spriteEditor)
        {
            m_SpriteAiEditor = spriteEditor as SpriteFrameAIModule;
            if (m_SpriteAiEditor == null)
                return false;
            m_ImageManipulationToolStrip.Activate(m_SpriteAiEditor.uiEventBus);
            m_SpriteAiEditor.uiEventBus.AIModeChangeEvent += OnAIModeChanged;
            m_SpriteAiEditor.uiEventBus.AIModeDisableEvent += OnAIModeDisabled;
            OnAIModeDisabled(new AIModeEnableEvent(this) { enable = m_SpriteAiEditor.aiModeEnabled });
            return true;
        }

        void OnAIModeDisabled(AIModeEnableEvent obj)
        {
            m_Toolbar.SetEnabled(obj.enable);
        }

        void OnAIModeChanged(AIModeChangeEvent evt)
        {
            if (evt.sender != this)
            {
                SetAIMode(evt.mode);
            }
        }

        public override void OverlayDeactivated()
        {
            m_ImageManipulationToolStrip.Deactivate();
            if (m_SpriteAiEditor != null)
            {
                m_SpriteAiEditor.uiEventBus.AIModeChangeEvent -= OnAIModeChanged;
                m_SpriteAiEditor.uiEventBus.AIModeDisableEvent -= OnAIModeDisabled;
                m_SpriteAiEditor = null;
            }
        }

        public override Type GetSpriteFrameModeType()
        {
            return typeof(SpriteFrameAIModule);
        }
    }
}
