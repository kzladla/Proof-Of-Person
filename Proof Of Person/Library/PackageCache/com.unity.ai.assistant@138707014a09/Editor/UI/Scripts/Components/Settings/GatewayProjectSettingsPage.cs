using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor;
using Unity.Relay.Editor;
using Unity.Relay.Editor.Acp;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class GatewayProjectSettingsPage : ManagedTemplate
    {
        DropdownField m_AgentTypeDropdown;
        Label m_AgentHelpText;
        Label m_AgentVersionLabel;
        bool m_IsSavingEnvVars; // Flag to prevent reload when we're the ones saving
        VisualElement m_CliPathContainer;
        Label m_CliPathLabel;
        TextField m_CliPathField;
        Button m_CliPathBrowseButton;
        Foldout m_EnvVarsFoldout;
        VisualElement m_EnvVarsContainer;
        Button m_AddEnvVarButton;

        List<GatewayPreferences.EnvVar> m_EnvVars = new();

        public GatewayProjectSettingsPage() :
            base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            LoadStyle(view, "GatewayProjectSettingsPage.uss", true);

            // Query UI elements
            m_AgentTypeDropdown = view.Q<DropdownField>("agent-type-dropdown");
            m_AgentHelpText = view.Q<Label>("agent-help-text");
            m_AgentVersionLabel = view.Q<Label>("agent-version-label");
            m_CliPathContainer = view.Q<VisualElement>("cli-path-container");
            m_CliPathLabel = view.Q<Label>("cli-path-label");
            m_CliPathField = view.Q<TextField>("cli-path-field");
            m_CliPathBrowseButton = view.Q<Button>("cli-path-browse-button");
            m_EnvVarsFoldout = view.Q<Foldout>("env-vars-foldout");
            m_EnvVarsContainer = view.Q<VisualElement>("env-vars-container");
            m_AddEnvVarButton = view.Q<Button>("add-env-var-button");

            // Set up agent type dropdown
            if (m_AgentTypeDropdown != null)
            {
                m_AgentTypeDropdown.choices = GatewayPreferences.AgentTypes.Values
                    .Select(a => a.DisplayName)
                    .ToList();

                var savedAgentType = GatewayPreferences.SelectedAgentType;
                var selectedIndex = GatewayPreferences.AgentTypes.Keys.ToList().IndexOf(savedAgentType);
                m_AgentTypeDropdown.index = selectedIndex >= 0 ? selectedIndex : 0;

                m_AgentTypeDropdown.RegisterValueChangedCallback(OnAgentTypeChanged);
                UpdateHelpText();
                UpdateVersionLabel();
            }

            // Set up CLI path
            m_CliPathField?.RegisterValueChangedCallback(OnCliPathChanged);
            m_CliPathBrowseButton?.RegisterCallback<ClickEvent>(_ => BrowseCliPath());

            // Set up environment variables
            m_AddEnvVarButton?.RegisterCallback<ClickEvent>(_ => AddEnvVar());

            // Load initial data
            LoadEnvVars();
            RenderEnvVars();
            LoadCliPath();

            RegisterAttachEvents(OnAttach, OnDetach);
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            GatewayPreferences.SelectedAgentTypeChanged += OnSelectedAgentTypeChangedExternally;
            GatewayPreferences.EnvironmentVariablesChanged += OnEnvironmentVariablesChangedExternally;
            AcpProvidersRegistry.OnProvidersChanged += OnProvidersChanged;
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            GatewayPreferences.SelectedAgentTypeChanged -= OnSelectedAgentTypeChangedExternally;
            GatewayPreferences.EnvironmentVariablesChanged -= OnEnvironmentVariablesChangedExternally;
            AcpProvidersRegistry.OnProvidersChanged -= OnProvidersChanged;
        }

        void OnProvidersChanged()
        {
            // Update version label when provider versions arrive asynchronously
            UpdateVersionLabel();
        }

        void OnEnvironmentVariablesChangedExternally(string agentType)
        {
            // Skip if we're the ones who triggered the save
            if (m_IsSavingEnvVars)
                return;

            // Only refresh if the changed agent type matches the currently selected one
            if (agentType == GetSelectedAgentTypeKey())
            {
                LoadEnvVars();
                RenderEnvVars();
                LoadCliPath();
            }
        }

        void OnSelectedAgentTypeChangedExternally()
        {
            // Refresh UI when changed externally
            if (m_AgentTypeDropdown != null)
            {
                var selectedIndex = GatewayPreferences.AgentTypes.Keys.ToList()
                    .IndexOf(GatewayPreferences.SelectedAgentType);
                m_AgentTypeDropdown.SetValueWithoutNotify(
                    m_AgentTypeDropdown.choices[selectedIndex >= 0 ? selectedIndex : 0]);
            }

            UpdateHelpText();
            UpdateVersionLabel();
            LoadEnvVars();
            RenderEnvVars();
            LoadCliPath();
        }

        string GetSelectedAgentTypeKey()
        {
            if (m_AgentTypeDropdown == null || m_AgentTypeDropdown.index < 0)
                return AcpConstants.DefaultProviderId;

            var keys = GatewayPreferences.AgentTypes.Keys.ToList();
            return m_AgentTypeDropdown.index < keys.Count ? keys[m_AgentTypeDropdown.index] : AcpConstants.DefaultProviderId;
        }

        void OnAgentTypeChanged(ChangeEvent<string> evt)
        {
            var agentKey = GetSelectedAgentTypeKey();
            GatewayPreferences.SelectedAgentType = agentKey;

            UpdateHelpText();
            UpdateVersionLabel();
            LoadEnvVarsForAgent(agentKey);
            RenderEnvVars();
            LoadCliPath();
        }

        void UpdateHelpText()
        {
            if (m_AgentHelpText == null) return;

            var agentKey = GetSelectedAgentTypeKey();
            if (GatewayPreferences.AgentTypes.TryGetValue(agentKey, out var agentInfo))
            {
                m_AgentHelpText.text = agentInfo.HelpText;
            }
        }

        void UpdateVersionLabel()
        {
            if (m_AgentVersionLabel == null) return;

            var agentKey = GetSelectedAgentTypeKey();
            var provider = AcpProvidersRegistry.Providers.FirstOrDefault(p => p.Id == agentKey);

            if (provider != null && !string.IsNullOrEmpty(provider.Version))
            {
                var versionText = $"v{provider.Version}";
                if (provider.IsCustom)
                {
                    versionText += "  [Custom]";
                }
                m_AgentVersionLabel.text = versionText;
                m_AgentVersionLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                m_AgentVersionLabel.text = "";
                m_AgentVersionLabel.style.display = DisplayStyle.None;
            }
        }

        void LoadEnvVars()
        {
            var agentType = GetSelectedAgentTypeKey();
            LoadEnvVarsForAgent(agentType);
        }

        void LoadEnvVarsForAgent(string agentType)
        {
            var storedVars = GatewayPreferences.LoadEnvironmentVariablesList(agentType);
            var presetNames = GatewayPreferences.GetDefaultEnvVarNames(agentType);

            // Build list: presets first (in order), then custom vars
            var presetVars = presetNames.Select(name =>
                storedVars.Find(v => v.Name == name)
                ?? new GatewayPreferences.EnvVar(name, "", GatewayPreferences.IsSensitiveEnvVar(name)));

            var customVars = storedVars.Where(v => !presetNames.Contains(v.Name));

            // Fill in from system environment for empty non-secure values
            m_EnvVars = presetVars.Concat(customVars)
                .Select(v => string.IsNullOrEmpty(v.Value) && !v.IsSecure
                    ? v with { Value = System.Environment.GetEnvironmentVariable(v.Name) ?? "" }
                    : v)
                .ToList();
        }

        void SaveEnvVars()
        {
            var agentType = GetSelectedAgentTypeKey();
            // Set flag to prevent OnEnvironmentVariablesChangedExternally from reloading
            m_IsSavingEnvVars = true;
            try
            {
                // This method handles both secure (stored in keychain) and non-secure (stored in EditorPrefs) vars
                GatewayPreferences.SaveEnvironmentVariablesWithSecureStorage(agentType, m_EnvVars);
            }
            finally
            {
                m_IsSavingEnvVars = false;
            }
        }

        void RenderEnvVars()
        {
            if (m_EnvVarsContainer == null) return;

            m_EnvVarsContainer.Clear();

            // Get the CLI path env var name to exclude from the list (it's shown in the dedicated field)
            var agentKey = GetSelectedAgentTypeKey();
            var cliPathEnvVarName = GatewayPreferences.GetCliPathEnvVarName(agentKey);

            for (int i = 0; i < m_EnvVars.Count; i++)
            {
                var index = i;
                var envVar = m_EnvVars[i];

                // Skip the CLI path env var - it's already displayed in the CLI Path field
                if (!string.IsNullOrEmpty(cliPathEnvVarName) && envVar.Name == cliPathEnvVarName)
                    continue;

                var row = new VisualElement();
                row.AddToClassList("gateway-env-var-row");

                var nameField = new TextField { value = envVar.Name ?? "" };
                nameField.AddToClassList("gateway-env-var-name");
                nameField.RegisterValueChangedCallback(evt =>
                {
                    m_EnvVars[index] = m_EnvVars[index] with
                    {
                        Name = evt.newValue,
                        IsSecure = GatewayPreferences.IsSensitiveEnvVar(evt.newValue)
                    };
                    SaveEnvVars();
                });

                if (envVar.IsSecure)
                    nameField.tooltip = "This key is stored securely in your system keychain";

                // For secure entries, show placeholder since actual value is in keychain
                var displayValue = envVar.IsSecure && string.IsNullOrEmpty(envVar.Value)
                    ? "(stored in keychain)"
                    : envVar.Value ?? "";

                var valueField = new TextField { value = displayValue, isPasswordField = true };
                valueField.AddToClassList("gateway-env-var-value");
                valueField.RegisterValueChangedCallback(evt =>
                {
                    m_EnvVars[index] = m_EnvVars[index] with { Value = evt.newValue };
                    SaveEnvVars();
                });

                var visibilityButton = new Button { text = "O" };
                visibilityButton.AddToClassList("gateway-env-var-visibility-button");
                visibilityButton.clicked += () =>
                {
                    valueField.isPasswordField = !valueField.isPasswordField;
                    visibilityButton.text = valueField.isPasswordField ? "O" : "*";
                };

                var removeButton = new Button(() => RemoveEnvVar(index)) { text = "-" };
                removeButton.AddToClassList("gateway-env-var-remove-button");

                // Add lock icon column - always present for consistent layout
                var lockLabel = new Label(envVar.IsSecure ? "🔒" : "");
                lockLabel.AddToClassList("gateway-env-var-lock");
                if (envVar.IsSecure)
                    lockLabel.tooltip = "Stored securely in system keychain";

                row.Add(lockLabel);
                row.Add(nameField);
                row.Add(valueField);
                row.Add(visibilityButton);
                row.Add(removeButton);

                m_EnvVarsContainer.Add(row);
            }
        }

        void AddEnvVar()
        {
            m_EnvVars.Add(new GatewayPreferences.EnvVar("", ""));
            SaveEnvVars();
            RenderEnvVars();
        }

        void RemoveEnvVar(int index)
        {
            if (index >= 0 && index < m_EnvVars.Count)
            {
                m_EnvVars.RemoveAt(index);
                SaveEnvVars();
                RenderEnvVars();
            }
        }

        void OnCliPathChanged(ChangeEvent<string> evt)
        {
            var agentKey = GetSelectedAgentTypeKey();
            var envVarName = GatewayPreferences.GetCliPathEnvVarName(agentKey);
            if (string.IsNullOrEmpty(envVarName)) return;

            var existingIndex = m_EnvVars.FindIndex(v => v.Name == envVarName);
            if (string.IsNullOrEmpty(evt.newValue))
            {
                if (existingIndex >= 0)
                {
                    m_EnvVars.RemoveAt(existingIndex);
                    SaveEnvVars();
                    RenderEnvVars();
                }
            }
            else
            {
                if (existingIndex >= 0)
                    m_EnvVars[existingIndex] = m_EnvVars[existingIndex] with { Value = evt.newValue };
                else
                {
                    m_EnvVars.Insert(0, new GatewayPreferences.EnvVar(envVarName, evt.newValue));
                    RenderEnvVars();
                }
                SaveEnvVars();
            }
        }

        void BrowseCliPath()
        {
            var agentKey = GetSelectedAgentTypeKey();
            GatewayPreferences.AgentTypes.TryGetValue(agentKey, out var agentInfo);
            var title = $"Select {agentInfo?.DisplayName ?? "Agent"} CLI Executable";

            var path = EditorUtility.OpenFilePanel(title, "", "");
            if (!string.IsNullOrEmpty(path))
            {
                m_CliPathField.value = path;
            }
        }

        void LoadCliPath()
        {
            if (m_CliPathField == null) return;

            var agentKey = GetSelectedAgentTypeKey();
            var envVarName = GatewayPreferences.GetCliPathEnvVarName(agentKey);

            if (string.IsNullOrEmpty(envVarName))
            {
                m_CliPathField.value = "";
                m_CliPathContainer?.SetEnabled(false);
                return;
            }

            m_CliPathContainer?.SetEnabled(true);

            var envVar = m_EnvVars.Find(v => v.Name == envVarName);
            m_CliPathField.SetValueWithoutNotify(envVar?.Value ?? "");

            if (m_CliPathLabel != null)
            {
                m_CliPathLabel.tooltip = $"Sets {envVarName} environment variable";
            }
        }
    }
}
