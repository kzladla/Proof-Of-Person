using System;
using System.Collections.Generic;
using Unity.U2D.AI.Editor.ImageManipulation;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEditor.U2D.Sprites;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.Overlay
{
    interface IBrushToolSettingsOverlay
    {
        event Action<BrushSettingsData> OnValueChanged;

        BrushSettingsData value { get; set; }
        bool requestShow { get; set; }
        bool hasValidMode { get; set; }
        void SetValueWithoutNotify(BrushSettingsData newValue);
        void AddMiscElements(VisualElement element);
        void ClearMiscElements();
    }

    [Overlay(typeof(ISpriteEditor), overlayId, nameof(BrushSettings),
        defaultLayout = Layout.Panel,
        defaultDockZone = DockZone.TopToolbar,
        defaultDockPosition = DockPosition.Top)]
    internal class BrushToolSettingsOverlay : UnityEditor.Overlays.Overlay, ICreateHorizontalToolbar, ITransientOverlay, IBrushToolSettingsOverlay
    {
        public const string overlayId = "com.unity.2d.enhancers/ToolSettingsOverlay";

        public event Action<BrushSettingsData> OnValueChanged;

        List<VisualElement> m_MiscElements = new List<VisualElement>();

        BrushSettingsData m_Settings;

        bool m_RequestShow = false;
        bool m_HasCorrectMode = false;

        BrushSettingsOverlayToolbar m_Toolbar;

        public BrushSettingsData value
        {
            get => m_Settings;
            set
            {
                if (m_Settings.Equals(value))
                    return;

                SetValueWithoutNotify(value);
                OnValueChanged?.Invoke(value);
            }
        }

        public bool visible => m_RequestShow;

        public bool requestShow
        {
            get => m_RequestShow;
            set
            {
                m_RequestShow = value;
                UpdateDisplayed();
            }
        }

        public bool hasValidMode
        {
            get => m_HasCorrectMode;
            set
            {
                m_HasCorrectMode = value;
                UpdateDisplayed();
            }
        }

        public BrushToolSettingsOverlay()
        {
            displayName = TextContent.brushSettingsOverlayDisplayName;

            UpdateDisplayed();
        }

        void UpdateDisplayed()
        {
            displayed = requestShow && hasValidMode;
        }

        public OverlayToolbar CreateHorizontalToolbarContent()
        {
            m_Toolbar = new BrushSettingsOverlayToolbar();
            m_Toolbar.SetMiscElements(m_MiscElements);
            m_Toolbar.SetValueWithoutNotify(value);
            m_Toolbar.RegisterValueChangedCallback(OnToolbarValueChanged);
            if (displayed != requestShow)
                displayed = requestShow;
            return m_Toolbar;
        }

        public override VisualElement CreatePanelContent()
        {
            m_Toolbar = new BrushSettingsOverlayToolbar();
            m_Toolbar.SetMiscElements(m_MiscElements);
            m_Toolbar.SetValueWithoutNotify(value);
            m_Toolbar.RegisterValueChangedCallback(OnToolbarValueChanged);
            if (displayed != requestShow)
                displayed = requestShow;
            return m_Toolbar;
        }

        public void AddMiscElements(VisualElement ve)
        {
            m_MiscElements = new List<VisualElement> { ve };
            UpdateMiscToolbars();
        }

        public void ClearMiscElements()
        {
            m_MiscElements.Clear();
            UpdateMiscToolbars();
        }

        void UpdateMiscToolbars()
        {
            m_Toolbar?.SetMiscElements(m_MiscElements);
        }

        void OnToolbarValueChanged(ChangeEvent<BrushSettingsData> evt)
        {
            value = evt.newValue;
        }

        public void SetValueWithoutNotify(BrushSettingsData newValue)
        {
            m_Settings = newValue;
            m_Toolbar?.SetValueWithoutNotify(newValue);
        }
    }

    [EditorToolbarElement(toolbarId, typeof(ISpriteEditor))]
    class BrushSettingsOverlayToolbar : OverlayToolbar, INotifyValueChanged<BrushSettingsData>
    {
        public const string toolbarId = "BrushSettingsOverlayToolbar";

        public void SetValueWithoutNotify(BrushSettingsData newValue)
        {
            m_Settings = newValue;

            UpdateUI();
        }

        public BrushSettingsData value
        {
            get => m_Settings;
            set
            {
                if (m_Settings.Equals(value))
                    return;

                using var evt = ChangeEvent<BrushSettingsData>.GetPooled(m_Settings, value);
                evt.target = this;
                SetValueWithoutNotify(value);
                SendEvent(evt);

                UpdateUI();
            }
        }

        BrushSettingsData m_Settings;

        Slider m_BrushSizeSlider;
        ColorField m_BrushColorField;

        VisualElement m_MiscElementHolder;

        public BrushSettingsOverlayToolbar()
        {
            m_BrushSizeSlider = new Slider
            {
                label = TextContent.brushSize, tooltip = TextContent.brushSizeTooltip,
                showInputField = true
            };
            m_BrushColorField = new ColorField
            {
                label = TextContent.brushColor, tooltip = TextContent.brushColorTooltip
            };

            m_BrushSizeSlider.labelElement.AddToClassList(CommonStyles.shortLabelClass);
            m_BrushColorField.labelElement.AddToClassList(CommonStyles.shortLabelClass);

            m_BrushSizeSlider.style.minWidth = 200;
            m_BrushColorField.style.minWidth = 150;

            Add(m_BrushSizeSlider);
            Add(new ToolbarSpacer());
            Add(m_BrushColorField);

            m_MiscElementHolder = new VisualElement();
            m_MiscElementHolder.style.marginLeft = 20; // TODO replace with a spacer or proper style.
            Add(m_MiscElementHolder);

            this.styleSheets.Add(CommonStyles.GetCommonStyleSheet());

            AddEventListeners();
        }

        public void SetMiscElements(IEnumerable<VisualElement> miscElements)
        {
            m_MiscElementHolder.Clear();
            foreach (var miscElement in miscElements)
                m_MiscElementHolder.Add(miscElement);

            EditorToolbarUtility.SetupChildrenAsButtonStrip(m_MiscElementHolder);
        }

        void AddEventListeners()
        {
            m_BrushSizeSlider.RegisterValueChangedCallback(evt =>
            {
                var newSettings = value;
                newSettings.size = evt.newValue;
                value = newSettings;
            });
            m_BrushColorField.RegisterValueChangedCallback(evt =>
            {
                var newSettings = value;
                newSettings.color = evt.newValue;
                value = newSettings;
            });
        }

        void UpdateUI()
        {
            m_BrushSizeSlider.lowValue = m_Settings.minSize;
            m_BrushSizeSlider.highValue = m_Settings.maxSize;
            m_BrushSizeSlider.SetValueWithoutNotify(m_Settings.size);
            m_BrushColorField.SetValueWithoutNotify(m_Settings.color);

            m_BrushSizeSlider.style.display = m_Settings.canModifySize ? DisplayStyle.Flex : DisplayStyle.None;
            m_BrushColorField.style.display = m_Settings.canModifyColor ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
