using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor;
using Unity.AI.Toolkit.Accounts.Components;
using Unity.Relay.Editor.Acp;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Editor.SessionBanner
{
    class AcpSessionStatusBannerProvider
    {
        public static event Action<AcpProviderDescriptor, string, AcpInstallStep> OnInstallDialogRequested;

        BasicBannerContent m_ConnectingBanner;
        BasicBannerContent m_StartingSessionBanner;
        BasicBannerContent m_ErrorBanner;
        BasicBannerContent m_AuthBanner;
        BasicBannerContent m_UnsupportedPlatformBanner;

        string m_ProviderId;
        string m_ProviderDisplayName;
        string m_LastError;
        string[] m_MissingAuthVars = Array.Empty<string>();
        AcpProviderDescriptor m_ProviderDescriptor;
        bool m_HasInstallStep;
        bool m_IsAttached;
        bool m_ProviderJustChanged;

        public event Action OnChange;

        public void Attach()
        {
            if (m_IsAttached)
                return;

            m_IsAttached = true;
            ProviderStateObserver.OnProviderChanged += OnProviderChanged;
            ProviderStateObserver.OnReadyStateChanged += OnReadyStateChanged;
            ProviderStateObserver.OnPhaseChanged += OnPhaseChanged;
            AcpProvidersRegistry.OnProvidersChanged += OnProvidersChanged;
            GatewayPreferences.EnvironmentVariablesChanged += OnEnvironmentVariablesChanged;
            ExecutableAvailabilityState.OnAvailabilityChanged += OnExecutableAvailabilityChanged;
            AcpProvidersRegistry.Client.OnValidateExecutableResponse += OnValidateExecutableResponse;
            RefreshProviderInfo();

            // Request initial validation for non-Unity providers
            if (!ProviderStateObserver.IsUnityProvider)
            {
                RequestExecutableValidation(ProviderStateObserver.CurrentProviderId);
            }
        }

        public void Detach()
        {
            if (!m_IsAttached)
                return;

            m_IsAttached = false;
            ProviderStateObserver.OnProviderChanged -= OnProviderChanged;
            ProviderStateObserver.OnReadyStateChanged -= OnReadyStateChanged;
            ProviderStateObserver.OnPhaseChanged -= OnPhaseChanged;
            AcpProvidersRegistry.OnProvidersChanged -= OnProvidersChanged;
            GatewayPreferences.EnvironmentVariablesChanged -= OnEnvironmentVariablesChanged;
            ExecutableAvailabilityState.OnAvailabilityChanged -= OnExecutableAvailabilityChanged;
            AcpProvidersRegistry.Client.OnValidateExecutableResponse -= OnValidateExecutableResponse;
        }

        public VisualElement GetCurrentView()
        {
            if (ProviderStateObserver.IsUnityProvider)
                return null;

            if (IsProviderUnsupportedOnCurrentPlatform())
                return m_UnsupportedPlatformBanner ??= BuildUnsupportedPlatformBanner();

            // Check proactive validation result (before session starts)
            var providerId = ProviderStateObserver.CurrentProviderId;
            var availability = ExecutableAvailabilityState.IsAvailable(providerId);
            if (availability == false)
            {
                return BuildProviderUnavailableBanner();
            }

            switch (ProviderStateObserver.ReadyState)
            {
                case ProviderStateObserver.ProviderReadyState.Initializing:
                    return GetInitializingView();
                case ProviderStateObserver.ProviderReadyState.Error:
                    if (m_MissingAuthVars.Length > 0)
                        return BuildAuthRequiredBanner();
                    return BuildErrorBanner();
                case ProviderStateObserver.ProviderReadyState.Ready:
                    return null;
            }

            return null;
        }

        void OnProviderChanged(string providerId)
        {
            m_ProviderJustChanged = !ProviderStateObserver.IsUnityProvider;
            RefreshProviderInfo();

            // Request validation for the new provider
            if (!ProviderStateObserver.IsUnityProvider)
            {
                RequestExecutableValidation(providerId);
            }

            OnChange?.Invoke();
        }

        void OnReadyStateChanged(ProviderStateObserver.ProviderReadyState state, string error)
        {
            if (state != ProviderStateObserver.ProviderReadyState.Initializing)
            {
                m_ProviderJustChanged = false;
            }

            OnChange?.Invoke();
        }

        void OnPhaseChanged(ProviderStateObserver.InitializationPhase phase)
        {
            OnChange?.Invoke();
        }

        void OnProvidersChanged()
        {
            RefreshProviderInfo();
            OnChange?.Invoke();
        }

        void OnEnvironmentVariablesChanged(string agentType)
        {
            // Only react if it's the current provider
            if (agentType != ProviderStateObserver.CurrentProviderId)
                return;

            // Clear cache and request new validation
            ExecutableAvailabilityState.ClearCache(agentType);
            RequestExecutableValidation(agentType);
        }

        void OnValidateExecutableResponse(string agentType, bool isValid, string executablePath, string error)
        {
            ExecutableAvailabilityState.HandleValidationResponse(agentType, isValid, executablePath, error);
        }

        void OnExecutableAvailabilityChanged(string providerId)
        {
            if (providerId == ProviderStateObserver.CurrentProviderId)
            {
                OnChange?.Invoke();
            }
        }

        void RequestExecutableValidation(string providerId)
        {
            if (string.IsNullOrEmpty(providerId) || providerId == "unity")
                return;

            // Load environment variables from preferences
            var env = GatewayPreferences.LoadEnvironmentVariables(providerId);
            ExecutableAvailabilityState.RequestValidation(providerId, env);
        }

        void RefreshProviderInfo()
        {
            var providerId = ProviderStateObserver.CurrentProviderId;
            if (string.IsNullOrEmpty(providerId) || providerId == "unity")
                return;

            if (m_ProviderId != providerId)
            {
                m_ProviderId = providerId;
                m_ConnectingBanner = null;
                m_StartingSessionBanner = null;
                m_ErrorBanner = null;
                m_AuthBanner = null;
                m_UnsupportedPlatformBanner = null;
                m_LastError = null;
            }

            AcpProvidersRegistry.EnsureInitialized();
            var provider = AcpProvidersRegistry.Providers.FirstOrDefault(p => p.Id == providerId);
            var displayName = !string.IsNullOrEmpty(provider?.DisplayName) ? provider.DisplayName : providerId;
            var hasInstallStep = HasInstallStep(provider);

            var authVarNames = provider?.EnvVarNames?
                .Where(IsAuthVarName)
                .Distinct()
                .ToArray() ?? Array.Empty<string>();

            var env = GatewayPreferences.LoadEnvironmentVariables(providerId, authVarNames);
            var missingAuthVars = authVarNames
                .Where(name => !env.TryGetValue(name, out var value) || string.IsNullOrEmpty(value))
                .ToArray();

            if (!string.Equals(m_ProviderDisplayName, displayName, StringComparison.Ordinal) ||
                !m_MissingAuthVars.SequenceEqual(missingAuthVars) ||
                m_HasInstallStep != hasInstallStep)
            {
                m_ConnectingBanner = null;
                m_StartingSessionBanner = null;
                m_ErrorBanner = null;
                m_AuthBanner = null;
                m_UnsupportedPlatformBanner = null;
                m_LastError = null;
            }

            m_ProviderDisplayName = displayName;
            m_MissingAuthVars = missingAuthVars;
            m_ProviderDescriptor = provider;
            m_HasInstallStep = hasInstallStep;
        }

        VisualElement GetInitializingView()
        {
            switch (ProviderStateObserver.CurrentPhase)
            {
                case ProviderStateObserver.InitializationPhase.CreatingSession:
                case ProviderStateObserver.InitializationPhase.WaitingForStarted:
                case ProviderStateObserver.InitializationPhase.WaitingForInitialized:
                    return m_StartingSessionBanner ??= BuildStartingSessionBanner();
                case ProviderStateObserver.InitializationPhase.ConnectingToRelay:
                    return m_ConnectingBanner ??= BuildConnectingBanner();
                case ProviderStateObserver.InitializationPhase.None:
                default:
                    return m_ProviderJustChanged
                        ? m_ConnectingBanner ??= BuildConnectingBanner()
                        : m_StartingSessionBanner ??= BuildStartingSessionBanner();
            }
        }

        static bool IsAuthVarName(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                   name.IndexOf("API_KEY", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        BasicBannerContent BuildConnectingBanner()
        {
            var providerName = string.IsNullOrEmpty(m_ProviderDisplayName) ? "provider" : m_ProviderDisplayName;
            var message = $"Connecting to {providerName}...";
            return new BasicBannerContent(message, links: null, loadingMessage: message);
        }

        BasicBannerContent BuildStartingSessionBanner()
        {
            var providerName = string.IsNullOrEmpty(m_ProviderDisplayName) ? "provider" : m_ProviderDisplayName;
            var message = $"Starting {providerName} session...";
            return new BasicBannerContent(message, links: null, loadingMessage: message);
        }

        BasicBannerContent BuildErrorBanner()
        {
            var providerName = string.IsNullOrEmpty(m_ProviderDisplayName) ? "provider" : m_ProviderDisplayName;
            var error = ProviderStateObserver.InitializationError;
            if (string.IsNullOrEmpty(error))
                error = "Session failed to initialize.";

            if (m_ErrorBanner != null && m_LastError == error)
                return m_ErrorBanner;

            m_LastError = error;

            // Register link handler for opening Gateway preferences (used by error messages with links)
            void OpenGatewayPreferences()
            {
                if (!string.IsNullOrEmpty(m_ProviderId) && GatewayPreferences.AgentTypes.ContainsKey(m_ProviderId))
                {
                    GatewayPreferences.SelectedAgentType = m_ProviderId;
                }

                SettingsService.OpenUserPreferences("Preferences/AI/Gateway");
            }

            var links = new List<LabelLink>
            {
                new LabelLink("open-gateway-preferences", OpenGatewayPreferences)
            };

            // Show full error in banner (errors are not shown in conversation)
            m_ErrorBanner = new BasicBannerContent($"Failed to start {providerName} session.\n{error}", links);
            return m_ErrorBanner;
        }

        BasicBannerContent BuildProviderUnavailableBanner()
        {
            var providerName = string.IsNullOrEmpty(m_ProviderDisplayName) ? "provider" : m_ProviderDisplayName;
            var platform = GetInstallPlatformKey();
            var installStep = m_ProviderDescriptor?.GetInstallStep(platform);
            var hasInstallSteps = installStep != null;

            var message =
                $"The executable for {providerName} wasn't found.\nIf you have it installed, you can <link=open-gateway-preferences><color=#7BAEFA>enter the path for it manually</color></link>.";
            if (hasInstallSteps)
            {
                message +=
                    "\nAlternatively, you can <link=open-install-dialog><color=#7BAEFA>install it from the internet</color></link>.";
            }

            void OpenGatewayPreferences()
            {
                if (!string.IsNullOrEmpty(m_ProviderId) && GatewayPreferences.AgentTypes.ContainsKey(m_ProviderId))
                {
                    GatewayPreferences.SelectedAgentType = m_ProviderId;
                }

                SettingsService.OpenUserPreferences("Preferences/AI/Gateway");
            }

            void OpenInstallDialog()
            {
                if (!hasInstallSteps || installStep == null)
                    return;

                OnInstallDialogRequested?.Invoke(m_ProviderDescriptor, platform, installStep);
            }

            var links = new List<LabelLink>
            {
                new LabelLink("open-gateway-preferences", OpenGatewayPreferences)
            };
            if (hasInstallSteps)
            {
                links.Add(new LabelLink("open-install-dialog", OpenInstallDialog));
            }

            return new BasicBannerContent(
                message,
                links);
        }

        BasicBannerContent BuildAuthRequiredBanner()
        {
            if (m_AuthBanner != null)
                return m_AuthBanner;

            var providerName = string.IsNullOrEmpty(m_ProviderDisplayName) ? "provider" : m_ProviderDisplayName;
            var missingVars = string.Join(", ", m_MissingAuthVars);

            void OpenGatewayPreferences()
            {
                if (!string.IsNullOrEmpty(m_ProviderId) && GatewayPreferences.AgentTypes.ContainsKey(m_ProviderId))
                {
                    GatewayPreferences.SelectedAgentType = m_ProviderId;
                }

                SettingsService.OpenUserPreferences("Preferences/AI/Gateway");
            }

            var links = new List<LabelLink>
            {
                new LabelLink("open-gateway-preferences", OpenGatewayPreferences)
            };

            m_AuthBanner = new BasicBannerContent(
                $"Missing credentials for {providerName}. Set {missingVars} in the " +
                "<link=open-gateway-preferences><color=#7BAEFA>Gateway preferences</color></link> and restart the session.",
                links);
            return m_AuthBanner;
        }

        static bool HasInstallStep(AcpProviderDescriptor provider)
        {
            var platform = GetInstallPlatformKey();
            if (string.IsNullOrEmpty(platform))
                return false;

            var step = provider?.GetInstallStep(platform);
            return step != null;
        }

        static string GetInstallPlatformKey()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    return "win32";
                case RuntimePlatform.OSXEditor:
                    return "darwin";
                case RuntimePlatform.LinuxEditor:
                    return "linux";
                default:
                    return null;
            }
        }

        static bool IsProviderUnsupportedOnCurrentPlatform()
        {
            return ProviderStateObserver.IsProviderUnsupportedOnCurrentPlatform;
        }

        BasicBannerContent BuildUnsupportedPlatformBanner()
        {
            var providerName = string.IsNullOrEmpty(m_ProviderDisplayName) ? "provider" : m_ProviderDisplayName;
            return new BasicBannerContent($"Currently the {providerName} provider is not supported on Windows.");
        }
    }
}
