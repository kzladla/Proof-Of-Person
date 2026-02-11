using System;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Events.UI;
using Unity.U2D.AI.Editor.Prefs;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEditor.U2D.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.ImageManipulation
{
    interface ISpriteOpacityPopup
    {
        event Action<float> OnValueChanged;
        float value { get; set; }
        void SetValueWithoutNotify(float newValue);
    }

    internal class SpriteOpacityPopup : OverlayPopupWindow, ISpriteOpacityPopup
    {
        public event Action<float> OnValueChanged;

        public float value
        {
            get => m_Slider.value;
            set => m_Slider.value = value;
        }

        Slider m_Slider;

        void OnEnable()
        {
            m_Slider = new Slider(0, 1)
            {
                label = TextContent.spriteOpacity,
                showInputField = true,
                tooltip = TextContent.spriteOpacityTooltip
            };
            m_Slider.labelElement.AddToClassList(CommonStyles.shortLabelClass);
            m_Slider.RegisterValueChangedCallback(OnSliderValueChanged);

            rootVisualElement.Add(m_Slider);
            rootVisualElement.style.justifyContent = Justify.SpaceAround;
            rootVisualElement.styleSheets.Add(CommonStyles.GetCommonStyleSheet());
        }

        public void SetValueWithoutNotify(float newValue)
        {
            m_Slider.SetValueWithoutNotify(newValue);
        }

        void OnSliderValueChanged(ChangeEvent<float> evt)
        {
            OnValueChanged?.Invoke(evt.newValue);
        }
    }

    internal class ShowSpriteTool : EditorToolbarDropdownToggle, IEventSender
    {
        UIEventBus m_EventBus;

        public ShowSpriteTool()
        {
            style.flexDirection = FlexDirection.Row;

            icon = Utilities.LoadIcon("Packages/com.unity.2d.enhancers/Editor/PackageResources/icons", "HideImage@32.png");
            tooltip = TextContent.hideSpriteTooltip;
            this.RegisterValueChangedCallback(OnToggleValueChanged);

            EditorToolbarUtility.SetupChildrenAsButtonStrip(this);

            dropdownClicked += OnDropdownClicked;
        }

        void OnDropdownClicked()
        {
            var isSpriteHidden = value;
            if (isSpriteHidden)
                return;

            ISpriteOpacityPopup popup = OverlayPopupWindow.Show<SpriteOpacityPopup>(this, new Vector2(200, 24));
            if (popup == null)
                return;

            popup.SetValueWithoutNotify(ModulePreferences.ShowSpriteOpacity);
            popup.OnValueChanged += OnSliderValueChanged;
        }

        public void RegisterEventBus(UIEventBus eventBus)
        {
            if (m_EventBus != null)
                m_EventBus.ShowSpriteChangeEvent -= OnShowSpriteChangeEvent;

            m_EventBus = eventBus;
            if (m_EventBus != null)
                m_EventBus.ShowSpriteChangeEvent += OnShowSpriteChangeEvent;
        }

        void OnToggleValueChanged(ChangeEvent<bool> evt)
        {
            ModulePreferences.ShowSprite = !evt.newValue;
            SendChangeEvent();
        }

        void OnSliderValueChanged(float newOpacity)
        {
            ModulePreferences.ShowSpriteOpacity = newOpacity;
            SendChangeEvent();
        }

        void SendChangeEvent()
        {
            m_EventBus.SendEvent(new ShowSpriteChangeEvent(this)
            {
                showBackground = ModulePreferences.ShowSprite,
                backgroundAlpha = ModulePreferences.ShowSpriteOpacity
            });
        }

        void OnShowSpriteChangeEvent(ShowSpriteChangeEvent evt)
        {
            if (evt.sender != this)
            {
                SetValues(evt.showBackground, evt.backgroundAlpha);
            }
        }

        void SetValues(bool showSprite, float opacity)
        {
            SetValueWithoutNotify(!showSprite);
            if (EditorWindow.HasOpenInstances<SpriteOpacityPopup>())
            {
                var popup = EditorWindow.GetWindow<SpriteOpacityPopup>();
                popup.SetValueWithoutNotify(opacity);
            }
        }
    }
}
