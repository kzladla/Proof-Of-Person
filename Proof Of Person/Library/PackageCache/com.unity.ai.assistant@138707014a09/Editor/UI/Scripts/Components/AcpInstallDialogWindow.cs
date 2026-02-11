using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.SessionBanner;
using Unity.Relay.Editor;
using Unity.Relay.Editor.Acp;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using Debug = UnityEngine.Debug;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    [InitializeOnLoad]
    class AcpInstallDialogWindow : EditorWindow
    {
        const float k_WindowWidth = 520f;
        const float k_WindowHeight = 360f;

        const string k_ViewPath = AssistantUIConstants.UIModulePath + AssistantUIConstants.ViewFolder + "AcpInstallDialogView.uxml";

        AcpProviderDescriptor m_Provider;
        string m_ProviderId;
        string m_ProviderName;
        string m_Platform;
        AcpInstallStep m_Step;

        Label m_TitleLabel;
        Label m_DescriptionLabel;
        VisualElement m_CommandsContainer;
        VisualElement m_ApiKeyContainer;
        TextField m_ApiKeyField;
        Label m_StatusLabel;
        Button m_InstallButton;
        Button m_CancelButton;

        Process m_InstallProcess;
        SynchronizationContext m_MainThreadContext;

        static AcpInstallDialogWindow()
        {
            AcpSessionStatusBannerProvider.OnInstallDialogRequested += Show;
        }

        public static void Show(AcpProviderDescriptor provider, string platform, AcpInstallStep step)
        {
            if (provider == null || step == null)
                return;

            var window = CreateInstance<AcpInstallDialogWindow>();
            window.m_Provider = provider;
            window.m_ProviderId = provider.Id;
            window.m_ProviderName = string.IsNullOrEmpty(provider.DisplayName) ? provider.Id : provider.DisplayName;
            window.m_Platform = platform;
            window.m_Step = step;
            window.titleContent = new GUIContent($"Install {window.m_ProviderName}");

            var size = new Vector2(k_WindowWidth, k_WindowHeight);
            window.minSize = size;
            window.maxSize = size;
            window.position = GetCenteredRect(size);
            window.ShowModalUtility();
        }

        void CreateGUI()
        {
            m_MainThreadContext = SynchronizationContext.Current;

            var root = rootVisualElement;
            root.Clear();

            LoadStyle(root, AssistantUIConstants.UIStylePath + AssistantUIConstants.AssistantBaseStyle);
            LoadStyle(root, AssistantUIConstants.UIStylePath + (EditorGUIUtility.isProSkin
                ? AssistantUIConstants.AssistantSharedStyleDark
                : AssistantUIConstants.AssistantSharedStyleLight) + AssistantUIConstants.StyleExtension);

            var view = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_ViewPath);
            if (view == null)
            {
                Debug.LogError($"Missing install dialog view at {k_ViewPath}.");
                return;
            }

            view.CloneTree(root);
            var dialogRoot = root.Q<VisualElement>(className: "acp-install-dialog");
            if (dialogRoot != null)
            {
                dialogRoot.AddToClassList(EditorGUIUtility.isProSkin ? "theme-dark" : "theme-light");
            }

            m_TitleLabel = root.Q<Label>("titleLabel");
            m_DescriptionLabel = root.Q<Label>("descriptionLabel");
            m_CommandsContainer = root.Q<VisualElement>("commandsContainer");
            var commandLabel = root.Q<Label>("commandLabel");
            var copyButton = root.Q<Button>("copyButton");

            m_ApiKeyContainer = root.Q<VisualElement>("apiKeyContainer");
            m_ApiKeyField = root.Q<TextField>("apiKeyField");

            m_StatusLabel = root.Q<Label>("statusLabel");
            m_InstallButton = root.Q<Button>("installButton");
            m_CancelButton = root.Q<Button>("cancelButton");

            if (m_TitleLabel != null)
                m_TitleLabel.text = $"Install {m_ProviderName}";
            if (m_DescriptionLabel != null)
                m_DescriptionLabel.text = "Review the command below. The Gateway will run it locally and stop on failure. You can copy and run it yourself instead.";
            if (m_StatusLabel != null)
                m_StatusLabel.style.display = DisplayStyle.None;

            if (commandLabel != null)
                commandLabel.text = m_Step?.Display ?? string.Empty;
            if (copyButton != null)
                copyButton.clicked += () => GUIUtility.systemCopyBuffer = m_Step?.Display ?? string.Empty;

            if (m_InstallButton != null)
                m_InstallButton.clicked += OnInstallClicked;
            if (m_CancelButton != null)
                m_CancelButton.clicked += Close;
        }

        void OnInstallClicked()
        {
            if (string.IsNullOrEmpty(m_ProviderId) || string.IsNullOrEmpty(m_Platform))
            {
                ShowInstallResult(new AcpInstallResult
                {
                    Ok = false,
                    Error = "Missing provider or platform."
                });
                return;
            }

            if (m_Step?.Exec?.Command == null)
            {
                ShowInstallResult(new AcpInstallResult
                {
                    Ok = false,
                    Error = "No install command available."
                });
                return;
            }

            var args = m_Step.Exec.Args != null ? string.Join(" ", m_Step.Exec.Args) : string.Empty;
            var startInfo = new ProcessStartInfo
            {
                FileName = m_Step.Exec.Command,
                Arguments = args,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            m_InstallProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            m_InstallProcess.Exited += OnProcessExited;

            SetRunningState(true);

            try
            {
                m_InstallProcess.Start();
            }
            catch (System.Exception ex)
            {
                ShowInstallResult(new AcpInstallResult
                {
                    Ok = false,
                    Error = $"Failed to start process: {ex.Message}"
                });
                CleanupProcess();
            }
        }

        void SetRunningState(bool isRunning)
        {
            m_InstallButton?.SetEnabled(!isRunning);

            if (isRunning && m_CancelButton != null)
                m_CancelButton.style.display = DisplayStyle.None;

            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = isRunning ? "Running install in terminal..." : string.Empty;
                m_StatusLabel.style.display = DisplayStyle.Flex;
            }
        }

        void ShowInstallResult(AcpInstallResult result)
        {
            if (m_StatusLabel == null)
                return;

            m_StatusLabel.style.display = DisplayStyle.Flex;
            if (result.Ok)
            {
                m_StatusLabel.text = string.Empty;
                m_StatusLabel.style.display = DisplayStyle.None;

                // Update window and title
                titleContent = new GUIContent($"{m_ProviderName} Installed");
                if (m_TitleLabel != null)
                    m_TitleLabel.text = $"{m_ProviderName} has been installed.";

                // Hide command section
                if (m_CommandsContainer != null)
                    m_CommandsContainer.style.display = DisplayStyle.None;

                // Check for post-install info
                var postInstall = m_Provider?.PostInstall;
                if (postInstall != null && !string.IsNullOrEmpty(postInstall.Message))
                {
                    // Show post-install message with clickable links
                    if (m_DescriptionLabel != null)
                    {
                        m_DescriptionLabel.text = postInstall.Message;
                        m_DescriptionLabel.RegisterCallback<PointerDownLinkTagEvent>(OnLinkClicked);
                    }

                    // Show API key input
                    if (m_ApiKeyContainer != null)
                        m_ApiKeyContainer.style.display = DisplayStyle.Flex;

                    // Setup API key field submission
                    if (m_ApiKeyField != null)
                    {
                        m_ApiKeyField.RegisterCallback<KeyDownEvent>(evt =>
                        {
                            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                            {
                                SaveApiKeyAndClose(postInstall.EnvVarName, m_ApiKeyField.value);
                            }
                        });
                        m_ApiKeyField.Focus();
                    }

                    // Transform install button to Save button
                    if (m_InstallButton != null)
                    {
                        m_InstallButton.text = "Save";
                        m_InstallButton.clicked -= OnInstallClicked;
                        m_InstallButton.clicked += () => SaveApiKeyAndClose(postInstall.EnvVarName, m_ApiKeyField?.value);
                        m_InstallButton.SetEnabled(true);
                    }
                }
                else
                {
                    // No post-install info, just show done
                    if (m_DescriptionLabel != null)
                        m_DescriptionLabel.text = $"{m_ProviderName} is ready to use.";

                    // Transform install button to Done button
                    if (m_InstallButton != null)
                    {
                        m_InstallButton.text = "Done";
                        m_InstallButton.clicked -= OnInstallClicked;
                        m_InstallButton.clicked += Close;
                        m_InstallButton.SetEnabled(true);
                    }

                    // Trigger banner refresh
                    RefreshBanner();
                }
            }
            else
            {
                var errorMessage = string.IsNullOrEmpty(result.Error) ? "Unknown error." : result.Error;
                var failedStep = string.IsNullOrEmpty(result.FailedStep) ? "" : $" Failed at: {result.FailedStep}";
                m_StatusLabel.text = $"Install failed. {errorMessage}{failedStep}";

                // Transform install button to Back button
                if (m_InstallButton != null)
                {
                    m_InstallButton.text = "Back";
                    m_InstallButton.clicked -= OnInstallClicked;
                    m_InstallButton.clicked += Close;
                    m_InstallButton.SetEnabled(true);
                }
            }
        }

        void OnLinkClicked(PointerDownLinkTagEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.linkID))
                Application.OpenURL(evt.linkID);
        }

        void SaveApiKeyAndClose(string envVarName, string apiKey)
        {
            if (string.IsNullOrEmpty(envVarName) || string.IsNullOrEmpty(apiKey))
            {
                Close();
                return;
            }

            // Use secure storage for API keys (async operation)
            _ = SaveApiKeySecureAsync(envVarName, apiKey);

            // Set this provider as the selected agent type
            GatewayPreferences.SelectedAgentType = m_ProviderId;

            // Trigger banner refresh
            RefreshBanner();

            // Close this dialog first, then switch to the provider and start a session
            Close();

            // Use delayCall to ensure the dialog is fully closed before switching providers
            EditorApplication.delayCall += () =>
            {
                var assistantWindow = AssistantWindow.ShowWindow();
                if (assistantWindow?.m_Context != null)
                {
                    _ = assistantWindow.m_Context.SwitchProviderAsync(m_ProviderId);
                }
            };
        }

        async Task SaveApiKeySecureAsync(string envVarName, string apiKey)
        {
            var envVars = GatewayPreferences.LoadEnvironmentVariablesList(m_ProviderId);
            var isSecure = CredentialClient.Instance.IsConnected &&
                           await CredentialClient.Instance.StoreAsync(m_ProviderId, envVarName, apiKey);

            var newVar = new GatewayPreferences.EnvVar(envVarName, isSecure ? "" : apiKey, isSecure);
            var existingIdx = envVars.FindIndex(v => v.Name == envVarName);

            if (existingIdx >= 0)
                envVars[existingIdx] = newVar;
            else
                envVars.Add(newVar);

            GatewayPreferences.SaveEnvironmentVariables(m_ProviderId, envVars);
        }

        void RefreshBanner()
        {
            ExecutableAvailabilityState.ClearCache(m_ProviderId);
            var env = GatewayPreferences.LoadEnvironmentVariables(m_ProviderId);
            ExecutableAvailabilityState.RequestValidation(m_ProviderId, env);
        }

        static void LoadStyle(VisualElement root, string path)
        {
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (style != null)
            {
                root.styleSheets.Add(style);
                return;
            }

            var themeStyle = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
            if (themeStyle != null)
            {
                root.styleSheets.Add(themeStyle);
            }
        }

        void OnProcessExited(object sender, System.EventArgs e)
        {
            var exitCode = m_InstallProcess?.ExitCode ?? -1;
            m_MainThreadContext?.Post(_ =>
            {
                ShowInstallResult(new AcpInstallResult
                {
                    Ok = exitCode == 0,
                    Error = exitCode != 0 ? $"Process exited with code {exitCode}" : null
                });
                CleanupProcess();
            }, null);
        }

        void CleanupProcess()
        {
            if (m_InstallProcess == null)
                return;

            m_InstallProcess.Exited -= OnProcessExited;
            m_InstallProcess.Dispose();
            m_InstallProcess = null;
        }

        void OnDestroy()
        {
            if (m_InstallProcess != null && !m_InstallProcess.HasExited)
            {
                m_InstallProcess.Kill();
            }
            CleanupProcess();
        }

        static Rect GetCenteredRect(Vector2 size)
        {
            var editorMainWindowRect = EditorGUIUtility.GetMainWindowPosition();
            return new Rect(
                editorMainWindowRect.x + (editorMainWindowRect.width - size.x) * 0.5f,
                editorMainWindowRect.y + (editorMainWindowRect.height - size.y) * 0.5f,
                size.x,
                size.y
            );
        }
    }
}
