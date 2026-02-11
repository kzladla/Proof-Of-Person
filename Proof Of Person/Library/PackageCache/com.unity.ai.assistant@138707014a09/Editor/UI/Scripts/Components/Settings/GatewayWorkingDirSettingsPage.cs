using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// Project Settings page for configuring AI Gateway working directories per provider.
    /// Appears under Project Settings > AI > Gateway.
    /// </summary>
    class GatewayWorkingDirSettingsPage : ManagedTemplate
    {
        /// <summary>
        /// Stable list of provider entries to ensure consistent ordering between
        /// dropdown choices and key lookup (Dictionary enumeration order is not guaranteed).
        /// </summary>
        static readonly List<(string Key, GatewayAgentTypeInfo Info)> s_ProviderEntries =
            GatewayPreferences.AgentTypes.Select(kvp => (kvp.Key, kvp.Value)).ToList();

        DropdownField m_ProviderDropdown;
        TextField m_WorkdirPathField;
        Button m_WorkdirBrowseButton;
        Label m_WorkdirHelpText;
        Toggle m_IncludeDefaultAgentsToggle;

        public GatewayWorkingDirSettingsPage() :
            base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            // Query UI elements
            m_ProviderDropdown = view.Q<DropdownField>("provider-dropdown");
            m_WorkdirPathField = view.Q<TextField>("workdir-path-field");
            m_WorkdirBrowseButton = view.Q<Button>("workdir-browse-button");
            m_WorkdirHelpText = view.Q<Label>("workdir-help-text");
            m_IncludeDefaultAgentsToggle = view.Q<Toggle>("include-default-agents-toggle");

            // Set up provider dropdown using stable entry list
            if (m_ProviderDropdown != null)
            {
                m_ProviderDropdown.choices = s_ProviderEntries
                    .Select(e => e.Info.DisplayName)
                    .ToList();

                // Default to first provider
                m_ProviderDropdown.index = 0;

                m_ProviderDropdown.RegisterValueChangedCallback(OnProviderChanged);
            }

            // Set up working directory path field
            m_WorkdirPathField?.RegisterValueChangedCallback(OnWorkdirPathChanged);
            m_WorkdirBrowseButton?.RegisterCallback<ClickEvent>(_ => BrowseWorkdir());

            // Set up include default agents.md toggle
            m_IncludeDefaultAgentsToggle?.RegisterValueChangedCallback(OnIncludeDefaultAgentsChanged);

            // Load initial data
            LoadWorkdirPath();
            LoadIncludeDefaultAgents();

            RegisterAttachEvents(OnAttach, OnDetach);
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            GatewayProjectPreferences.WorkingDirChanged += OnWorkingDirChangedExternally;
            GatewayProjectPreferences.IncludeDefaultAgentsMdChanged += OnIncludeDefaultAgentsChangedExternally;
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            GatewayProjectPreferences.WorkingDirChanged -= OnWorkingDirChangedExternally;
            GatewayProjectPreferences.IncludeDefaultAgentsMdChanged -= OnIncludeDefaultAgentsChangedExternally;
        }

        void OnWorkingDirChangedExternally(string agentType)
        {
            // Only refresh if the changed agent type matches the currently selected one
            if (agentType == GetSelectedProviderKey())
            {
                LoadWorkdirPath();
            }
        }

        string GetSelectedProviderKey()
        {
            if (m_ProviderDropdown == null || m_ProviderDropdown.index < 0)
                return s_ProviderEntries[0].Key;

            return m_ProviderDropdown.index < s_ProviderEntries.Count
                ? s_ProviderEntries[m_ProviderDropdown.index].Key
                : s_ProviderEntries[0].Key;
        }

        GatewayAgentTypeInfo GetSelectedProviderInfo()
        {
            if (m_ProviderDropdown == null || m_ProviderDropdown.index < 0)
                return s_ProviderEntries[0].Info;

            return m_ProviderDropdown.index < s_ProviderEntries.Count
                ? s_ProviderEntries[m_ProviderDropdown.index].Info
                : s_ProviderEntries[0].Info;
        }

        void OnProviderChanged(ChangeEvent<string> evt)
        {
            LoadWorkdirPath();
        }

        void LoadWorkdirPath()
        {
            if (m_WorkdirPathField == null) return;

            var providerKey = GetSelectedProviderKey();
            var configuredPath = GatewayProjectPreferences.GetConfiguredWorkingDir(providerKey);
            m_WorkdirPathField.SetValueWithoutNotify(configuredPath);
        }

        void OnWorkdirPathChanged(ChangeEvent<string> evt)
        {
            var providerKey = GetSelectedProviderKey();
            GatewayProjectPreferences.SetWorkingDir(providerKey, evt.newValue);
        }

        void BrowseWorkdir()
        {
            var providerKey = GetSelectedProviderKey();
            var providerInfo = GetSelectedProviderInfo();
            var title = $"Select Working Directory for {providerInfo.DisplayName}";

            // Start from current configured path or project root
            var currentPath = GatewayProjectPreferences.GetWorkingDir(providerKey);
            var startFolder = !string.IsNullOrEmpty(currentPath) && System.IO.Directory.Exists(currentPath)
                ? currentPath
                : GatewayProjectPreferences.ProjectRoot;

            var selectedPath = EditorUtility.OpenFolderPanel(title, startFolder, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                m_WorkdirPathField.value = selectedPath;
            }
        }

        void LoadIncludeDefaultAgents()
        {
            if (m_IncludeDefaultAgentsToggle == null) return;

            var value = GatewayProjectPreferences.IncludeDefaultAgentsMd;
            m_IncludeDefaultAgentsToggle.SetValueWithoutNotify(value);
        }

        void OnIncludeDefaultAgentsChanged(ChangeEvent<bool> evt)
        {
            GatewayProjectPreferences.IncludeDefaultAgentsMd = evt.newValue;
        }

        void OnIncludeDefaultAgentsChangedExternally()
        {
            LoadIncludeDefaultAgents();
        }
    }
}
