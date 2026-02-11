using System;
using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// User preferences shown in Edit -> Preferences...
    /// </summary>
    static partial class AssistantEditorPreferences
    {
        internal const string k_SettingsPrefix = "AIAssistant.";
        const string k_SendPromptModifierKey = k_SettingsPrefix + "SendPromptUseModifierKey";
        const string k_AutoRun = k_SettingsPrefix + "AutoRun";
        const string k_CollapseReasoningWhenComplete = k_SettingsPrefix + "CollapseReasoningWhenComplete";
        const string k_ForceAiGateway = k_SettingsPrefix + "ForceAiGateway";
        const string k_SelectedModelPrefix = k_SettingsPrefix + "SelectedModel.";
        const string k_EnablePackageAutoUpdate = k_SettingsPrefix + "EnablePackageAutoUpdate";
        const string k_ShowPackageUpdateBanner = k_SettingsPrefix + "ShowPackageUpdateBanner";

        public static event Action<bool> UseModifierKeyToSendPromptChanged;
        public static event Action<bool> AutoRunChanged;
        public static event Action<bool> CollapseReasoningWhenCompleteChanged;
        public static event Action<bool> ForceAiGatewayChanged;
        public static event Action<string, string> SelectedModelChanged;
        public static event Action<bool> EnablePackageAutoUpdateChanged;
        public static event Action<bool> ShowPackageUpdateBannerChanged;

        /// <summary>
        /// When true, pressing enter with modifier key (ex: ctrl) sends the prompt without having to click the button to send the prompt
        /// </summary>
        public static bool UseModifierKeyToSendPrompt
        {
            get => EditorPrefs.GetBool(k_SendPromptModifierKey, false);
            set
            {
                if (UseModifierKeyToSendPrompt != value)
                {
                    EditorPrefs.SetBool(k_SendPromptModifierKey, value);
                    UseModifierKeyToSendPromptChanged?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// When enabled, do not ask the user for permissions.
        /// </summary>
        public static bool AutoRun
        {
            get => EditorPrefs.GetBool(k_AutoRun, false);
            set
            {
                if (AutoRun != value)
                {
                    EditorPrefs.SetBool(k_AutoRun, value);
                    AutoRunChanged?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// If true, when the reasoning is completed, it'll collapse the reasoning section.
        /// </summary>
        public static bool CollapseReasoningWhenComplete
        {
            get => EditorPrefs.GetBool(k_CollapseReasoningWhenComplete, false);
            set
            {
                if (CollapseReasoningWhenComplete != value)
                {
                    EditorPrefs.SetBool(k_CollapseReasoningWhenComplete, value);
                    CollapseReasoningWhenCompleteChanged?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// When enabled, forces the AI Gateway to be shown in the Assistant UI,
        /// regardless of the IsMcpProEnabled setting from the account.
        /// </summary>
        public static bool ForceAiGateway
        {
            get => EditorPrefs.GetBool(k_ForceAiGateway, false);
            set
            {
                if (ForceAiGateway != value)
                {
                    EditorPrefs.SetBool(k_ForceAiGateway, value);
                    ForceAiGatewayChanged?.Invoke(value);
                }
            }
        }

        // TODO: Set to true to enable AI Gateway for users with IsMcpProEnabled account flag.
        // When false, AI Gateway is only available when ForceAiGateway is explicitly enabled.
        const bool k_EnableAiGatewayForMcpProUsers = false;

        /// <summary>
        /// Returns true if the AI Gateway should be enabled in the Assistant UI.
        /// Controlled by <see cref="k_EnableAiGatewayForMcpProUsers"/>.
        /// </summary>
        public static bool AiGatewayEnabled => k_EnableAiGatewayForMcpProUsers
            ? ForceAiGateway || Unity.AI.Toolkit.Accounts.Services.Account.settings.IsMcpProEnabled
            : ForceAiGateway;

        /// <summary>
        /// Get the selected model for a provider.
        /// Returns null if no selection has been made (the session's current model will be used).
        /// </summary>
        /// <param name="providerId">The provider ID.</param>
        /// <returns>The selected model ID, or null if not set.</returns>
        public static string GetSelectedModel(string providerId)
        {
            var value = EditorPrefs.GetString(k_SelectedModelPrefix + providerId, null);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// Set the selected model for a provider.
        /// </summary>
        /// <param name="providerId">The provider ID.</param>
        /// <param name="modelId">The model ID to select.</param>
        public static void SetSelectedModel(string providerId, string modelId)
        {
            var current = GetSelectedModel(providerId);
            if (current != modelId)
            {
                EditorPrefs.SetString(k_SelectedModelPrefix + providerId, modelId);
                SelectedModelChanged?.Invoke(providerId, modelId);
            }
        }
      
        /// <summary>
        /// If true, automatically check for and prompt to update the Assistant package when a new version is available.
        /// </summary>
        public static bool EnablePackageAutoUpdate
        {
            get => EditorPrefs.GetBool(k_EnablePackageAutoUpdate, true);
            set
            {
                if (EnablePackageAutoUpdate != value)
                {
                    EditorPrefs.SetBool(k_EnablePackageAutoUpdate, value);
                    EnablePackageAutoUpdateChanged?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// Debug setting to show or hide the package update banner for testing purposes.
        /// </summary>
        public static bool ShowPackageUpdateBanner
        {
            get => EditorPrefs.GetBool(k_ShowPackageUpdateBanner, true);
            set
            {
                if (ShowPackageUpdateBanner != value)
                {
                    EditorPrefs.SetBool(k_ShowPackageUpdateBanner, value);
                    ShowPackageUpdateBannerChanged?.Invoke(value);
                }
            }
        }
    }
}
