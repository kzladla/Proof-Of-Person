using System;
using System.Collections.Generic;
using Unity.U2D.AI.Editor.AIBridge;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Events.UI;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    internal class ImageManipulationToolStrip : OverlayToolbar, IEventSender
    {
        class ModeIndicator : VisualElement
        {
            readonly Dictionary<EAIMode, Texture2D> k_ModeIcons;

            public ModeIndicator(Dictionary<EAIMode, Texture2D> modeIcons)
            {
                k_ModeIcons = modeIcons;

                // TODO use uss styles
                style.height = 20;
                style.alignItems = Align.Center;
                style.justifyContent = Justify.Center;
            }

            public void SetMode(EAIMode mode)
            {
                Clear();

                if (k_ModeIcons.TryGetValue(mode, out var modeIcon))
                {
                    var icon = new Image
                    {
                        image = modeIcon,
                        style =
                        {
                            height = 16
                        }
                    };
                    Add(icon);
                }
            }
        }

        ModeIndicator m_ModeIndicator;
        UIEventBus m_UiEventBus;
        Dictionary<EAIMode, List<IToolToggle>> m_Tools = new();
        Dictionary<EAIMode, VisualElement> m_ToolStrips = new();
        EAIMode m_AIMode;

        public ImageManipulationToolStrip(Dictionary<EAIMode, Texture2D> icons)
        {
            m_ModeIndicator = new ModeIndicator(icons);
            Add(m_ModeIndicator);
            EditorToolbarUtility.SetupChildrenAsButtonStrip(m_ModeIndicator);

            var toolStripGenerate = new VisualElement();
            m_ToolStrips[EAIMode.Generation] = toolStripGenerate;
            AddToolToUI<DoodleToolToggle>(toolStripGenerate, EAIMode.Generation);
            AddToolToUI<DoodleEraserToolToggle>(toolStripGenerate, EAIMode.Generation);
            AddToolToUI<ClearToolToggle>(toolStripGenerate, EAIMode.Generation);
            EditorToolbarUtility.SetupChildrenAsButtonStrip(toolStripGenerate);

            var toolStripInpaint = new VisualElement();
            m_ToolStrips[EAIMode.Inpaint] = toolStripInpaint;
            AddToolToUI<DoodleToolToggle>(toolStripInpaint, EAIMode.Inpaint);
            AddToolToUI<DoodleEraserToolToggle>(toolStripInpaint, EAIMode.Inpaint);
            toolStripInpaint.Add(new VisualElement());
            AddToolToUI<InpaintMaskTool>(toolStripInpaint, EAIMode.Inpaint);
            AddToolToUI<InpaintMaskEraserToolToggle>(toolStripInpaint, EAIMode.Inpaint);
            toolStripInpaint.Add(new VisualElement());
            AddToolToUI<ClearToolToggle>(toolStripInpaint, EAIMode.Inpaint);
            EditorToolbarUtility.SetupChildrenAsButtonStrip(toolStripInpaint);

            foreach (var toolStrip in m_ToolStrips.Values)
            {
                toolStrip.style.display = DisplayStyle.None;
                Add(toolStrip);
            }

            style.display = DisplayStyle.None;
        }

        public void SetLayout(Layout overlayLayout)
        {
            foreach (var toolStrip in m_ToolStrips.Values)
                toolStrip.style.flexDirection = overlayLayout is Layout.HorizontalToolbar or Layout.Panel ? FlexDirection.Row : FlexDirection.Column;
            style.flexDirection = overlayLayout is Layout.HorizontalToolbar or Layout.Panel ? FlexDirection.Row : FlexDirection.Column;
        }

        public void Activate(UIEventBus uiEventBus)
        {
            m_UiEventBus = uiEventBus;

            Debug.Assert(uiEventBus != null);

            m_UiEventBus.AIModeChangeEvent += OnAIModeChangeEventHandler;
            m_UiEventBus.ShowImageManipulationToolEvent += OnShowImageManipulationTool;
            m_UiEventBus.ImageManipulationToolChangeEvent += OnShowImageManipulationTool;
            style.display = DisplayStyle.None;
        }

        public void Deactivate()
        {
            foreach (var toolGroup in m_Tools.Values)
            {
                foreach (var tool in toolGroup)
                {
                    tool?.RegisterEventBus(null);
                }
            }

            if (m_UiEventBus != null)
            {
                m_UiEventBus.AIModeChangeEvent -= OnAIModeChangeEventHandler;
                m_UiEventBus.ShowImageManipulationToolEvent -= OnShowImageManipulationTool;
                m_UiEventBus.ImageManipulationToolChangeEvent -= OnShowImageManipulationTool;
                m_UiEventBus = null;
            }
        }

        void AddToolToUI<T>(VisualElement strip, EAIMode mode) where T : VisualElement, IToolToggle, new()
        {
            var tool = new T();
            if (!m_Tools.ContainsKey(mode))
                m_Tools[mode] = new List<IToolToggle>();
            m_Tools[mode].Add(tool);
            strip.Add(tool);
        }

        void OnShowImageManipulationTool(ShowImageManipulationToolEvent evt)
        {
            // TODO: change this to tell what operation tool it should show
            var enableTools = (evt.mode == EAIMode.Generation && evt.imageControl != null) || evt.mode == EAIMode.Inpaint;
            m_AIMode = enableTools ? evt.mode : EAIMode.None;
            style.display = enableTools ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (var mode in m_Tools.Keys)
            {
                foreach (var tool in m_Tools[mode])
                {
                    tool.RegisterEventBus(enableTools ? m_UiEventBus : null);
                    tool.SetToggleValue(false);
                }
            }

            foreach (var (mode, toolStrip) in m_ToolStrips)
                toolStrip.style.display = mode == m_AIMode ? DisplayStyle.Flex : DisplayStyle.None;

            if (enableTools)
            {
                m_UiEventBus.SendEvent(new ImageManipulationModeChangeEvent(this)
                {
                    mode = EImageManipulationMode.Doodle,
                    doodleMode = EDoodlePadMode.BaseImage
                });
            }
            else
            {
                m_UiEventBus.SendEvent(new ImageManipulationModeChangeEvent(this)
                {
                    mode = EImageManipulationMode.None,
                    doodleMode = EDoodlePadMode.BaseImage
                });
            }
        }

        void OnShowImageManipulationTool(ImageManipulationModeChangeEvent evt)
        {
            if (!m_Tools.TryGetValue(m_AIMode, out var tools))
                return;
            foreach (var tool in tools)
                tool.SetToggleValue(tool.toolMode == evt.mode);
        }

        void OnAIModeChangeEventHandler(AIModeChangeEvent evt)
        {
            var newMode = evt.mode;

            m_ModeIndicator.SetMode(newMode);

            foreach (var (mode, toolStrip) in m_ToolStrips)
                toolStrip.style.display = mode == newMode ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
