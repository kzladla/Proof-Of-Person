using System;
using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts;
using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor
{
    /// <summary>
    /// A split button UI element with a main action button and a dropdown menu.
    /// The left side executes the primary action, while the right side shows a dropdown with additional options.
    /// </summary>
    class SplitButton : ManagedTemplate
    {
        public struct DropdownOption
        {
            public string Label;
            public Action Action;
            public bool Enabled;

            public DropdownOption(string label, Action action, bool enabled = true)
            {
                Label = label;
                Action = action;
                Enabled = enabled;
            }
        }

        Button m_MainButton;
        Button m_DropdownButton;
        Image m_Icon;
        Label m_Label;
        Image m_CaretIcon;

        string m_Text;
        string m_IconClassName;
        bool m_HasIcon;
        Action m_MainAction;
        readonly List<DropdownOption> m_DropdownOptions = new();

        public SplitButton() : base(AssistantUIConstants.UIModulePath) { }

        /// <summary>
        /// Sets the text displayed on the button.
        /// </summary>
        public void SetText(string text)
        {
            m_Text = text;
            m_Label.text = text;
        }

        /// <summary>
        /// Sets the icon displayed on the left side of the button.
        /// </summary>
        /// <param name="iconClassName">The USS class name for the icon (e.g., "wait")</param>
        public void SetIcon(string iconClassName)
        {
            // Remove old icon class
            if (!string.IsNullOrEmpty(m_IconClassName))
            {
                m_Icon.RemoveFromClassList($"mui-icon-{m_IconClassName}");
            }

            m_IconClassName = iconClassName;
            m_HasIcon = !string.IsNullOrEmpty(iconClassName);

            if (m_HasIcon)
            {
                m_Icon.style.display = DisplayStyle.Flex;
                m_Icon.AddToClassList($"mui-icon-{iconClassName}");
            }
            else
            {
                m_Icon.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Sets the main action that executes when the left button is clicked.
        /// </summary>
        public void SetMainAction(Action action)
        {
            m_MainAction = action;
        }

        /// <summary>
        /// Adds an option to the dropdown menu.
        /// </summary>
        public void AddDropdownOption(string label, Action action, bool enabled = true)
        {
            m_DropdownOptions.Add(new DropdownOption(label, action, enabled));
        }

        /// <summary>
        /// Clears all dropdown options.
        /// </summary>
        public void ClearDropdownOptions()
        {
            m_DropdownOptions.Clear();
        }

        /// <summary>
        /// Sets whether the button is enabled or disabled.
        /// </summary>
        public new void SetEnabled(bool enabled)
        {
            m_MainButton.SetEnabled(enabled);
            m_DropdownButton.SetEnabled(enabled);
        }

        protected override void InitializeView(TemplateContainer view)
        {
            // Query elements from UXML
            m_MainButton = view.Q<Button>("mainButton");
            m_DropdownButton = view.Q<Button>("dropdownButton");
            m_Icon = view.Q<Image>("icon");
            m_Label = view.Q<Label>("label");
            m_CaretIcon = view.Q<Image>("caret");

            // Register button callbacks
            m_MainButton.clicked += OnMainButtonClicked;
            m_DropdownButton.clicked += OnDropdownButtonClicked;
        }

        void OnMainButtonClicked()
        {
            m_MainAction?.Invoke();
        }

        void OnDropdownButtonClicked()
        {
            if (m_DropdownOptions.Count == 0)
                return;

            // Create and show dropdown menu
            var menu = new GenericDropdownMenu();
            foreach (var option in m_DropdownOptions)
            {
                if (option.Enabled)
                {
                    menu.AddItem(option.Label, false, option.Action);
                }
                else
                {
                    menu.AddDisabledItem(option.Label, false);
                }
            }

            var buttonRect = m_DropdownButton.worldBound;
            menu.DropDown(buttonRect, this,  DropdownMenuSizeMode.Content);
        }
    }
}
