using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Utils;
using Unity.Relay.Editor;
using Unity.Relay.Editor.Acp;
using UnityEditor;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace Unity.AI.Assistant.Editor.Acp
{
    enum AcpSessionState
    {
        Created,
        Starting,
        Active,
        Ending,
        Ended
    }

    /// <summary>
    /// Represents a single ACP session with an AI provider.
    /// Instances are managed by AcpSessions registry.
    /// </summary>
    class AcpSession : IDisposable
    {
        // Timeout for waiting for session/initialized during domain reload.
        // During domain reload, the subprocess is reused and won't send session/initialized again.
        // We wait briefly in case it's actually a relay restart (new subprocess will send it).
        static readonly TimeSpan k_DomainReloadInitTimeout = TimeSpan.FromMilliseconds(500);

        // Timeout for waiting for session/initialized during fresh session startup.
        static readonly TimeSpan k_FreshSessionInitTimeout = TimeSpan.FromSeconds(30);

        readonly AcpClient m_Client;
        readonly StringBuilder m_ResponseBuffer = new();
        readonly string m_ResumeSessionId;
        AssistantConversation m_Conversation;

        int m_RequestId;
        bool m_Disposed;
        AssistantMessage m_CurrentMessage;

        // Used to cancel pending operations when relay disconnects
        TaskCompletionSource<bool> m_PendingStartTcs;

        // Used to wait for session/initialized before StartAsync returns
        TaskCompletionSource<bool> m_PendingInitializedTcs;

        /// <summary>
        /// The task from StartAsync. Callers can await this to wait for initialization to complete.
        /// Returns true if session initialized successfully, false otherwise.
        /// </summary>
        public Task<bool> StartTask { get; set; }

        // Set when relay disconnects with an active session - used to resume after reconnect
        string m_PendingResumeSessionId;

        // Set when session was interrupted by relay disconnect - allows restart on reconnect
        bool m_WasInterruptedByDisconnect;

        // Guards against duplicate prompts being sent while one is in flight
        bool m_IsPromptInFlight;

        /// <summary>
        /// Unity's routing key for this session (also known as channelId in relay).
        /// </summary>
        public AssistantConversationId SessionId { get; }

        /// <summary>
        /// Agent's session ID from session/new - THE session ID per ACP spec.
        /// This is the ID needed for resuming sessions.
        /// </summary>
        public string AgentSessionId { get; private set; }

        /// <summary>
        /// Session title derived from the first user prompt.
        /// </summary>
        public string SessionTitle { get; private set; }

        /// <summary>
        /// The conversation for this session. Session owns and manages this instance.
        /// </summary>
        public AssistantConversation Conversation => m_Conversation;

        public string ProviderId { get; }
        public AcpSessionState State { get; private set; } = AcpSessionState.Created;
        public string AccumulatedText => m_ResponseBuffer.ToString();

        /// <summary>
        /// The current mode ID of the session (e.g., "read-only", "auto", "full-auto").
        /// Updated when session/initialized or current_mode_update is received.
        /// </summary>
        public string CurrentModeId { get; private set; }

        /// <summary>
        /// Whether the session is safe to release (turn has ended or session has ended).
        /// Used by AcpSessionCleanupManager to determine when to release inactive sessions.
        /// </summary>
        public bool IsSafeToRelease =>
            State == AcpSessionState.Ended ||
            State == AcpSessionState.Ending ||
            (State == AcpSessionState.Active && !m_IsPromptInFlight);

        // Used for reference counting
        internal int RefCount { get; set; }

        // Events for UI binding
        public event Action<string> OnTextChunk;
        public event Action<string> OnThoughtChunk;
        public event Action OnResponseComplete;
        public event Action<string> OnError;
        public event Action OnSessionEnded;

        /// <summary>
        /// Fired when session initialization completes (success or failure).
        /// Used by cleanup manager to release sessions that were marked during initialization.
        /// </summary>
        public event Action<bool> OnInitializationFinished;

        /// <summary>
        /// Fired when the agent's session ID is received. This ID can be used for resuming sessions.
        /// </summary>
        public event Action<string> OnAgentSessionIdReceived;

        /// <summary>
        /// Fired when the session title is received (derived from first user prompt).
        /// </summary>
        public event Action<string> OnSessionTitleReceived;

        /// <summary>
        /// Fired when session/initialized is received with modes and models data.
        /// Parameters: modes array (id, name, description), current mode ID, models array (modelId, name, description), current model ID
        /// </summary>
        public event Action<(string id, string name, string desc)[], string, (string modelId, string name, string description)[], string> OnSessionInitialized;

        /// <summary>
        /// Fired when current_mode_update notification is received.
        /// </summary>
        public event Action<string> OnModeChanged;

        /// <summary>
        /// Fired when available_commands_update notification is received.
        /// Parameters: array of (name, description, inputHint) tuples for each available command.
        /// The inputHint is null if the command takes no input.
        /// </summary>
        public event Action<(string name, string description, string inputHint)[]> OnAvailableCommandsUpdated;

        /// <summary>
        /// Fired when the ACP agent requests permission for an operation.
        /// The handler should call RespondToPermissionRequest with the user's choice.
        /// </summary>
        public event Action<AcpPermissionRequest> OnPermissionRequest;

        /// <summary>
        /// Fired when a tool call is started or updated (from tool_call session updates).
        /// Used by UI to display tool call progress.
        /// </summary>
        public event Action<AcpToolCallInfo> OnToolCall;

        /// <summary>
        /// Fired when a tool call receives an update (from tool_call_update session updates).
        /// Used by UI to update tool call status and display results.
        /// </summary>
        public event Action<AcpToolCallUpdate> OnToolCallUpdate;

        /// <summary>
        /// Fired when a plan update is received (from plan session updates).
        /// Used by UI to display the agent's execution plan.
        /// </summary>
        public event Action<AcpPlanBlock> OnPlanUpdate;

        /// <summary>
        /// Static event fired when an ACP update type is received that we don't handle yet.
        /// Used by developer tools to track what needs implementation.
        /// Parameters: sessionId, updateType, full payload JSON
        /// </summary>
        public static event Action<AssistantConversationId, string, string> OnUnhandledUpdate;

        /// <summary>
        /// Static event fired when an ACP session update is successfully handled.
        /// Used by developer tools to track which updates are being processed.
        /// Parameters: sessionId, updateType, toolCallId (optional, for tool_call/tool_call_update)
        /// </summary>
        public static event Action<AssistantConversationId, string, string> OnHandledUpdate;

        internal AcpSession(AssistantConversationId sessionId, string providerId, AcpClient sharedClient, string resumeSessionId = null, AssistantConversation existingConversation = null)
        {
            SessionId = sessionId;
            ProviderId = providerId;
            m_Client = sharedClient;
            m_ResumeSessionId = resumeSessionId;

            // Initialize conversation from existing or create new
            if (existingConversation != null)
            {
                m_Conversation = existingConversation;
            }
            else
            {
                m_Conversation = new AssistantConversation
                {
                    Id = sessionId,
                    Title = providerId ?? "ACP Session",
                    ProviderId = providerId,
                    CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    LastMessageTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
            }

            // Subscribe to shared client, will filter by sessionId
            m_Client.OnMessage += HandleMessage;
            m_Client.OnSessionStarted += HandleSessionStarted;
            m_Client.OnSessionEnded += HandleSessionEnded;
            m_Client.OnSessionError += HandleSessionError;
            m_Client.OnPermissionRequest += HandlePermissionRequest;
            m_Client.OnAgentSessionId += HandleAgentSessionId;
            m_Client.OnSessionTitle += HandleSessionTitle;
            m_Client.OnConnectionStateChanged += HandleConnectionStateChanged;
        }

        public async Task<bool> StartAsync()
        {
            if (State != AcpSessionState.Created)
                return State == AcpSessionState.Active;

            // Check for relay version mismatch
            var versionError = RelayService.Instance.VersionMismatchError;
            if (!string.IsNullOrEmpty(versionError))
            {
                ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Error, versionError);
                return false;
            }

            // Check for ACP capability
            if (!RelayService.Instance.HasCapability(RelayProtocol.Capabilities.Acp))
            {
                var errorMessage = "This relay doesn't support third-party providers. " +
                    "Enable 'Development Mode' in Developer Tools > Relay Settings, or rebuild the relay.";
                ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Error, errorMessage);
                return false;
            }

            State = AcpSessionState.Starting;
            m_ResponseBuffer.Clear();

            // Report phase: Creating session
            ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.CreatingSession);

            var agentKey = ProviderId ?? AcpConstants.DefaultProviderId;
            var envVarsList = GatewayPreferences.LoadEnvironmentVariablesList(agentKey);
            var presetNames = GatewayPreferences.GetDefaultEnvVarNames(agentKey);

            // Build env dictionary from stored vars, filling in from system environment for empty values
            var envVars = envVarsList
                .Where(v => !string.IsNullOrEmpty(v.Name))
                .ToDictionary(v => v.Name, v => !string.IsNullOrEmpty(v.Value) ? v.Value : Environment.GetEnvironmentVariable(v.Name) ?? "");

            // Add any missing preset vars from system environment
            foreach (var name in presetNames.Where(n => !envVars.ContainsKey(n)))
                envVars[name] = Environment.GetEnvironmentVariable(name) ?? "";

            // Collect secure var names: stored vars marked secure + all sensitive preset vars
            var secureEnvVarNames = envVarsList
                .Where(v => !string.IsNullOrEmpty(v.Name) && (v.IsSecure || GatewayPreferences.IsSensitiveEnvVar(v.Name)))
                .Select(v => v.Name)
                .Union(presetNames.Where(GatewayPreferences.IsSensitiveEnvVar))
                .ToList();

            var config = new AcpSessionConfig
            {
                SessionId = SessionId,
                AgentType = agentKey,
                WorkingDir = GatewayProjectPreferences.GetWorkingDir(agentKey),
                Env = envVars,
                SecureEnvVarNames = secureEnvVarNames,
                ResumeSessionId = m_PendingResumeSessionId ?? m_ResumeSessionId
            };

            m_PendingStartTcs = new TaskCompletionSource<bool>();
            var tcs = m_PendingStartTcs;
            AcpSessionError startError = null;

            void OnStarted(AssistantConversationId sid, int pid)
            {
                if (sid != SessionId) return;
                // Report phase: Waiting for initialized (dispatch to main thread since this triggers UI updates)
                MainThread.DispatchIfNeeded(() =>
                    ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.WaitingForInitialized)
                );
                tcs.TrySetResult(true);
            }

            void OnStartError(AssistantConversationId sid, AcpSessionError err)
            {
                if (sid != SessionId) return;
                startError = err;
                // Dispatch to main thread since ProviderStateObserver events trigger UI updates
                MainThread.DispatchIfNeeded(() =>
                    ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None));
                tcs.TrySetResult(false);
            }

            m_Client.OnSessionStarted += OnStarted;
            m_Client.OnSessionError += OnStartError;

            try
            {
                var sent = await m_Client.StartSessionAsync(config);
                if (!sent)
                {
                    State = AcpSessionState.Created;
                    ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None);
                    OnInitializationFinished?.Invoke(false);
                    return false;
                }

                // Report phase: Waiting for started
                ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.WaitingForStarted);

                // Wait for started/error event with timeout to prevent indefinite hang
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    State = AcpSessionState.Ended;
                    ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None);
                    if (ProviderStateObserver.ReadyState != ProviderStateObserver.ProviderReadyState.Error ||
                        string.IsNullOrEmpty(ProviderStateObserver.InitializationError))
                    {
                        ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Error,
                            "Session startup timed out waiting for provider to start.");
                    }
                    OnInitializationFinished?.Invoke(false);
                    return false;
                }

                var started = await tcs.Task;
                if (!started)
                {
                    // Check if this was a "resource not found" error during resume - if so, retry without resume ID
                    var wasResuming = !string.IsNullOrEmpty(config.ResumeSessionId);
                    var isNotFoundError = startError != null &&
                        startError.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);

                    if (wasResuming && isNotFoundError)
                    {
                        // Clear the resume IDs and retry with a fresh session.
                        // Note: After this retry, Claude won't have context from prior conversation turns.
                        // The Unity conversation continues (messages are preserved in m_Conversation),
                        // but Claude sees this as a new session. To restore full context, we would need
                        // to implement conversation history replay via the prompt, which is not yet supported.
                        // See RecoverableErrorPatterns in relay's errors.ts for the list of recoverable errors.
                        Debug.LogWarning($"[AcpSession] Resume failed with '{startError}', retrying without resume ID");
                        m_PendingResumeSessionId = null;
                        State = AcpSessionState.Created;
                        ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None);

                        // Recursively retry without resume - unsubscribe first to avoid double subscription
                        m_Client.OnSessionStarted -= OnStarted;
                        m_Client.OnSessionError -= OnStartError;
                        m_PendingStartTcs = null;
                        return await StartAsync();
                    }

                    State = AcpSessionState.Ended;
                    ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None);
                    OnInitializationFinished?.Invoke(false);
                    return false;
                }

                // Wait for session/initialized before returning
                // This ensures the agent is fully ready to receive prompts
                //
                // When resuming, check if we already received session/initialized for this session.
                // This happens during domain reload where the subprocess is reused and won't send
                // session/initialized again. We track this in SessionState which survives domain reload.
                //
                // Key distinction:
                // - m_ResumeSessionId (from constructor): Domain reload - subprocess reused, short timeout OK
                // - m_PendingResumeSessionId (from disconnect): Relay restart - new subprocess, need long timeout
                //
                // If resuming via m_PendingResumeSessionId, the relay was restarted and the subprocess
                // is new, so session/initialized WILL be sent. We must wait for it, not timeout early.
                var resumeId = config.ResumeSessionId;
                var isDomainReload = !string.IsNullOrEmpty(m_ResumeSessionId) &&
                    string.IsNullOrEmpty(m_PendingResumeSessionId);
                var alreadyInitialized = isDomainReload &&
                    !string.IsNullOrEmpty(resumeId) &&
                    SessionState.GetString($"AcpSession.Initialized.{resumeId}", "") == "true";

                m_PendingInitializedTcs = new TaskCompletionSource<bool>();

                // Use short timeout only for domain reload with flag set, long timeout otherwise
                var initTimeout = alreadyInitialized ? k_DomainReloadInitTimeout : k_FreshSessionInitTimeout;
                var initTimeoutTask = Task.Delay(initTimeout);
                var initCompletedTask = await Task.WhenAny(m_PendingInitializedTcs.Task, initTimeoutTask);

                if (initCompletedTask == initTimeoutTask)
                {
                    if (alreadyInitialized)
                    {
                        // Domain reload with flag set: subprocess reused, assume ready
                        ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None);
                        ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Ready);
                    }
                    else
                    {
                        // Fresh session or relay restart: real timeout error
                        State = AcpSessionState.Ended;
                        ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None);
                        ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Error,
                            "Session startup timed out waiting for initialization.");
                        OnInitializationFinished?.Invoke(false);
                        return false;
                    }
                }
                else
                {
                    var initialized = await m_PendingInitializedTcs.Task;
                    if (!initialized)
                    {
                        State = AcpSessionState.Ended;
                        ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None);
	                    if (startError != null &&
	                        (ProviderStateObserver.ReadyState != ProviderStateObserver.ProviderReadyState.Error ||
	                         string.IsNullOrEmpty(ProviderStateObserver.InitializationError)))
	                    {
	                        ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Error, startError.Message, startError.Code);
	                    }
                        OnInitializationFinished?.Invoke(false);
                        return false;
                    }
                    // Note: HandleSessionInitialized already sets phase and ready state
                }

                State = AcpSessionState.Active;
                OnInitializationFinished?.Invoke(true);
                return true;
            }
            finally
            {
                m_PendingStartTcs = null;
                m_PendingInitializedTcs = null;
                m_Client.OnSessionStarted -= OnStarted;
                m_Client.OnSessionError -= OnStartError;
            }
        }

        public async Task SendPromptAsync(string text)
        {
            await SendPromptAsync(new[] { new AcpTextContent { Text = text } });
        }

        public async Task SendPromptAsync(IEnumerable<AcpContentBlock> content)
        {
            // Guard against duplicate prompts while one is in flight
            if (m_IsPromptInFlight)
                return;

            m_IsPromptInFlight = true;

            try
            {
                // Auto-start session if in Created state (e.g., after relay reconnection)
                if (State == AcpSessionState.Created)
                {
                    StartTask = StartAsync();
                }

                // Wait for initialization to complete if session is still starting
                if (State == AcpSessionState.Starting && StartTask != null)
                {
                    var started = await StartTask;
                    if (!started)
                    {
                        m_IsPromptInFlight = false;
                        OnError?.Invoke("Session initialization failed");
                        return;
                    }
                }

                if (State != AcpSessionState.Active)
                {
                    m_IsPromptInFlight = false;
                    OnError?.Invoke("Session not active");
                    return;
                }

                var promptArray = content.Select(c => c switch
                {
                    AcpTextContent t => (object)new { type = "text", text = t.Text },
                    AcpResourceContent r => (object)new
                    {
                        type = "resource",
                        resource = new
                        {
                            text = r.Resource.Text,
                            uri = r.Resource.Uri,
                            mimeType = r.Resource.MimeType
                        }
                    },
                    AcpImageContent i => (object)new { type = "image", mimeType = i.MimeType, data = i.Data },
                    _ => null
                }).Where(x => x != null).ToArray();

                var payload = new
                {
                    jsonrpc = "2.0",
                    id = ++m_RequestId,
                    method = AcpConstants.Method_SessionPrompt,
                    @params = new { prompt = promptArray }
                };

                // Note: Don't capture user prompt here - AcpProvider already added it via AppendUserMessage
                // Both use the same conversation object, so capturing here would create duplicates

                await m_Client.SendRequestAsync(SessionId, payload);

                // Save conversation after prompt is sent (message was added by AcpProvider)
                SaveConversation();
            }
            catch
            {
                // Clear the flag on any exception so retries are possible
                m_IsPromptInFlight = false;
                throw;
            }
            // Note: m_IsPromptInFlight is cleared in HandleMessage when response completes or errors
        }

        public async Task CancelPromptAsync()
        {
            if (State != AcpSessionState.Active)
            {
                // Silently return - calling cancel on an inactive session is a no-op, not an error.
                // Previously this fired OnError("Session not active"), which could cause an infinite
                // error loop when AbortPrompt was called from error handlers.
                return;
            }

            var payload = new
            {
                jsonrpc = "2.0",
                id = ++m_RequestId,
                method = AcpConstants.Method_SessionCancel,
            };

            await m_Client.SendRequestAsync(SessionId, payload);
        }

        /// <summary>
        /// Request a mode change for the session.
        /// </summary>
        public async Task SetModeAsync(string modeId)
        {
            if (State != AcpSessionState.Active)
            {
                OnError?.Invoke("Session not active");
                return;
            }

            var payload = new
            {
                jsonrpc = "2.0",
                id = ++m_RequestId,
                method = AcpConstants.Method_SessionSetMode,
                @params = new { modeId }
            };

            await m_Client.SendRequestAsync(SessionId, payload);
        }

        /// <summary>
        /// Request a model change for the session.
        /// Uses the unstable session/set_model ACP method.
        /// </summary>
        public async Task SetModelAsync(string modelId)
        {
            if (State != AcpSessionState.Active)
            {
                OnError?.Invoke("Session not active");
                return;
            }

            var payload = new
            {
                jsonrpc = "2.0",
                id = ++m_RequestId,
                method = AcpConstants.Method_SessionSetModel,
                @params = new { modelId }
            };

            await m_Client.SendRequestAsync(SessionId, payload);
        }

        public async Task EndAsync()
        {
            if (State == AcpSessionState.Ending || State == AcpSessionState.Ended)
                return;

            State = AcpSessionState.Ending;
            await m_Client.EndSessionAsync(SessionId);
            State = AcpSessionState.Ended;
        }

        void HandleMessage(AssistantConversationId sessionId, JObject payload)
        {
            if (sessionId != SessionId) return;

            MainThread.DispatchAndForget(() =>
            {
                var method = payload["method"]?.ToString();
                if (method == AcpConstants.Method_SessionInitialized)
                {
                    HandleSessionInitialized(payload);
                }
                else if (method == AcpConstants.Method_SessionUpdate)
                {
                    var update = payload["params"]?["update"];
                    var updateType = update?["sessionUpdate"]?.ToString();

                    switch (updateType)
                    {
                        case AcpConstants.UpdateType_AgentMessageChunk:
                            var text = update?["content"]?["text"]?.ToString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                m_ResponseBuffer.Append(text);
                                OnTextChunk?.Invoke(text);
                                // Note: Don't capture here - AcpProvider handles message management
                            }
                            break;

                        case AcpConstants.UpdateType_AgentThoughtChunk:
                            var thoughtText = update?["content"]?["text"]?.ToString();
                            if (!string.IsNullOrEmpty(thoughtText))
                            {
                                OnThoughtChunk?.Invoke(thoughtText);
                            }
                            break;

                        case AcpConstants.UpdateType_CurrentModeUpdate:
                            var modeId = update?["currentModeId"]?.ToString();
                            if (!string.IsNullOrEmpty(modeId))
                            {
                                CurrentModeId = modeId;
                                OnModeChanged?.Invoke(modeId);
                            }
                            break;

                        case AcpConstants.UpdateType_ToolCall:
                            HandleToolCall(update as JObject);
                            // Note: Tool calls are captured by AcpProvider via HandleToolCall events
                            // Save conversation after tool call is added
                            SaveConversation();
                            break;

                        case AcpConstants.UpdateType_ToolCallUpdate:
                            HandleToolCallUpdate(update as JObject);
                            // Note: Tool call updates are captured by AcpProvider via HandleToolCallUpdate events
                            // Save conversation after tool call update
                            SaveConversation();
                            break;

                        case AcpConstants.UpdateType_AvailableCommandsUpdate:
                            HandleAvailableCommandsUpdate(update as JObject);
                            break;

                        case AcpConstants.UpdateType_Plan:
                            HandlePlanUpdate(update as JObject);
                            break;

                        // Known but not yet implemented update types
                        case AcpConstants.UpdateType_ToolResult:
                        case AcpConstants.UpdateType_AgentMessageStart:
                        case AcpConstants.UpdateType_AgentMessageEnd:
                        case AcpConstants.UpdateType_FileDiff:
                            OnUnhandledUpdate?.Invoke(sessionId, updateType, payload.ToString());
                            break;

                        default:
                            // Unknown update type - also track it
                            if (!string.IsNullOrEmpty(updateType))
                            {
                                OnUnhandledUpdate?.Invoke(sessionId, updateType, payload.ToString());
                            }
                            break;
                    }
                }
                else if (payload["result"] != null)
                {
                    m_IsPromptInFlight = false;
                    CompleteCurrentMessage();
                    OnResponseComplete?.Invoke();
                    // Save conversation after response is complete
                    SaveConversation();
                }
                else if (payload["error"] != null)
                {
                    // JSON-RPC error response - surface to UI
                    var errorObj = payload["error"];
                    var errorMessage = errorObj["message"]?.ToString() ?? "Unknown error";
                    var errorData = errorObj["data"];

                    // Format the error for display
                    var formattedError = FormatAcpError(errorMessage, errorData);

                    m_IsPromptInFlight = false;
                    CompleteCurrentMessage();
                    ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Error, formattedError);
                    OnError?.Invoke(formattedError);
                }
            });
        }

        void HandleSessionInitialized(JObject payload)
        {
            // Signal that initialization is complete (unblocks StartAsync if waiting)
            m_PendingInitializedTcs?.TrySetResult(true);

            // Track that this session has been initialized (survives domain reload)
            // This allows us to skip waiting for session/initialized when resuming
            // a session where the subprocess is reused (domain reload case)
            if (!string.IsNullOrEmpty(AgentSessionId))
            {
                SessionState.SetString($"AcpSession.Initialized.{AgentSessionId}", "true");
            }

            // Session is now fully initialized - clear phase and set ready
            ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None);
            ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Ready);

            // Parse modes
            var modes = payload["params"]?["modes"];

            // Always fire event - null/empty modes means clear the provider's modes
            if (modes == null)
            {
                OnSessionInitialized?.Invoke(Array.Empty<(string id, string name, string desc)>(), null, Array.Empty<(string modelId, string name, string description)>(), null);
                return;
            }


            var modeList = new List<(string id, string name, string desc)>();
            CurrentModeId = modes["currentModeId"]?.ToString();
            var availableModes = modes["availableModes"] as JArray;
            if (availableModes != null)
            {
                foreach (var m in availableModes)
                {
                    var id = m["id"]?.ToString();
                    var name = m["name"]?.ToString();
                    var desc = m["description"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(id))
                        modeList.Add((id, name ?? id, desc));
                }
            }
            else
            {
                OnSessionInitialized?.Invoke(Array.Empty<(string id, string name, string desc)>(), null, Array.Empty<(string modelId, string name, string description)>(), null);
                return;
            }

            // Parse models
            var models = payload["params"]?["models"];
            var modelList = new List<(string modelId, string name, string description)>();
            string currentModelId = null;

            if (models != null)
            {
                currentModelId = models["currentModelId"]?.ToString();
                var availableModels = models["availableModels"] as JArray;
                if (availableModels != null)
                {
                    foreach (var m in availableModels)
                    {
                        var modelId = m["modelId"]?.ToString();
                        var name = m["name"]?.ToString();
                        var description = m["description"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(modelId))
                            modelList.Add((modelId, name ?? modelId, description));
                    }
                }
            }

            OnSessionInitialized?.Invoke(modeList.ToArray(), CurrentModeId, modelList.ToArray(), currentModelId);
        }

        void HandleToolCall(JObject update)
        {
            if (update == null) return;

            var info = AcpToolCallInfo.FromUpdate(update);
            if (string.IsNullOrEmpty(info.ToolCallId)) return;

            OnToolCall?.Invoke(info);
            OnHandledUpdate?.Invoke(SessionId, AcpConstants.UpdateType_ToolCall, info.ToolCallId);
        }

        void HandleToolCallUpdate(JObject update)
        {
            if (update == null) return;

            var updateInfo = AcpToolCallUpdate.FromUpdate(update);
            if (string.IsNullOrEmpty(updateInfo.ToolCallId)) return;

            OnToolCallUpdate?.Invoke(updateInfo);
            OnHandledUpdate?.Invoke(SessionId, AcpConstants.UpdateType_ToolCallUpdate, updateInfo.ToolCallId);
        }

        void HandlePlanUpdate(JObject update)
        {
            if (update == null) return;

            var planBlock = AcpPlanBlock.FromUpdate(update);
            OnPlanUpdate?.Invoke(planBlock);
            OnHandledUpdate?.Invoke(SessionId, AcpConstants.UpdateType_Plan, null);
        }

        void HandleAvailableCommandsUpdate(JObject update)
        {
            if (update == null) return;

            var availableCommands = update["availableCommands"] as JArray;
            if (availableCommands == null) return;

            var commandList = new List<(string name, string description, string inputHint)>();
            foreach (var cmd in availableCommands)
            {
                var name = cmd["name"]?.ToString();
                var description = cmd["description"]?.ToString() ?? "";
                var input = cmd["input"] as JObject;
                var inputHint = input?["hint"]?.ToString();

                if (!string.IsNullOrEmpty(name))
                    commandList.Add((name, description, inputHint));
            }

            OnAvailableCommandsUpdated?.Invoke(commandList.ToArray());
            OnHandledUpdate?.Invoke(SessionId, AcpConstants.UpdateType_AvailableCommandsUpdate, null);
        }

        void HandleSessionStarted(AssistantConversationId sessionId, int pid)
        {
            // Handled in StartAsync via TCS
        }

        void HandleSessionEnded(AssistantConversationId sessionId, int exitCode)
        {
            if (sessionId != SessionId) return;
            State = AcpSessionState.Ended;

            // Clear the "initialized" flag - subprocess has exited, so next session
            // with this ID will need to wait for session/initialized again
            var agentId = AgentSessionId;
            MainThread.DispatchIfNeeded(() =>
            {
                if (!string.IsNullOrEmpty(agentId))
                {
                    SessionState.EraseString($"AcpSession.Initialized.{agentId}");
                }
                OnSessionEnded?.Invoke();
            });
        }

        void HandleSessionError(AssistantConversationId sessionId, AcpSessionError error)
        {
            if (sessionId != SessionId) return;

            // Fail any pending initialization wait
            m_PendingInitializedTcs?.TrySetResult(false);

            // Check if this is a "session not found" error that occurred after session startup
            // (e.g., during a prompt when resuming a session that no longer exists on the backend).
            // If so, clear the resume ID and allow retry with a fresh session.
            // See RecoverableErrorPatterns in relay's errors.ts for the list of recoverable errors.
            var isNotFoundError = error.Message != null &&
                error.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);
            var wasResuming = !string.IsNullOrEmpty(m_PendingResumeSessionId) ||
                !string.IsNullOrEmpty(AgentSessionId);

            if (isNotFoundError && wasResuming && State == AcpSessionState.Active)
            {
                Debug.LogWarning($"[AcpSession] Session error during active session: '{error.Message}', resetting for retry");

                // Clear resume IDs so next attempt starts fresh
                // Note: After retry, Claude won't have context from prior conversation turns.
                // The Unity conversation continues (messages are preserved in m_Conversation),
                // but Claude sees this as a new session.
                m_PendingResumeSessionId = null;

                // Reset state to allow restart
                State = AcpSessionState.Created;
                m_IsPromptInFlight = false;

                MainThread.DispatchIfNeeded(() =>
                {
                    ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None);
                    // Set to Ready so user can send prompt again (which will auto-start via SendPromptAsync)
                    ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Ready);
                });
                return;
            }

            // Dispatch to main thread since ProviderStateObserver events trigger UI updates
            MainThread.DispatchIfNeeded(() =>
            {
                ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None);
                ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Error, error.Message, error.Code);

                // Capture error in conversation history
                var errorMessage = new AssistantMessage
                {
                    Role = "error",
                    IsComplete = true,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                errorMessage.Blocks.Add(new ErrorBlock { Error = error.Message });

                m_Conversation.Messages.Add(errorMessage);
                m_Conversation.LastMessageTimestamp = errorMessage.Timestamp;
                SaveConversation();

                OnError?.Invoke(error.Message);
            });
        }

        void HandleConnectionStateChanged(bool isConnected)
        {
            if (isConnected)
            {
                // Relay reconnected - if session was interrupted by disconnect, allow restart
                // With a resume ID: will attempt to resume the previous session
                // Without a resume ID: will start fresh (e.g., if disconnected during initial startup)
                if (State == AcpSessionState.Ended && m_WasInterruptedByDisconnect)
                {
                    m_WasInterruptedByDisconnect = false;
                    State = AcpSessionState.Created;
                    MainThread.DispatchIfNeeded(() =>
                    {
                        // Set to Ready so user can send prompt (which will auto-start via SendPromptAsync)
                        // Using Initializing would disable input, creating a deadlock
                        ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Ready);
                    });
                }
                return;
            }

            // Relay disconnected - clear prompt in flight flag to allow retry after reconnect
            m_IsPromptInFlight = false;

            // Relay disconnected - fail any pending start or initialization operations
            var pendingTcs = m_PendingStartTcs;
            if (pendingTcs != null)
            {
                m_PendingStartTcs = null;
                pendingTcs.TrySetResult(false);
            }

            var pendingInitTcs = m_PendingInitializedTcs;
            if (pendingInitTcs != null)
            {
                m_PendingInitializedTcs = null;
                pendingInitTcs.TrySetResult(false);
            }

            // If session was active or starting, save agent session ID for resume and transition to ended
            // Note: We only set the ProviderStateObserver error state (shows banner) but don't
            // fire OnError/OnSessionEnded to avoid adding permanent error messages to conversation
            if (State == AcpSessionState.Starting || State == AcpSessionState.Active)
            {
                m_WasInterruptedByDisconnect = true;

                // Save agent session ID for resume after reconnection (if available)
                if (!string.IsNullOrEmpty(AgentSessionId))
                {
                    m_PendingResumeSessionId = AgentSessionId;
                }

                State = AcpSessionState.Ended;
                MainThread.DispatchIfNeeded(() =>
                {
                    ProviderStateObserver.SetPhase(ProviderStateObserver.InitializationPhase.None);
                    ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Error,
                        "Relay connection lost. Reconnecting...");
                });
            }
        }

        void HandleAgentSessionId(AssistantConversationId sessionId, string agentSessionId)
        {
            if (sessionId != SessionId)
                return;

            AgentSessionId = agentSessionId;
            m_Conversation.AgentSessionId = agentSessionId;

            MainThread.DispatchAndForget(() =>
            {
                // Track this session in memory so we can clean up orphans on reconnect
                // This is especially important for sessions that haven't had any messages yet
                // (and thus aren't persisted to storage)
                // NOTE: Must happen on main thread since AcpSessionTracker is a ScriptableSingleton
                AcpSessionTracker.instance.Track(agentSessionId, SessionId.Value, ProviderId);

                SaveConversation();
                OnAgentSessionIdReceived?.Invoke(agentSessionId);
            });
        }

        void HandleSessionTitle(AssistantConversationId sessionId, string title)
        {
            if (sessionId != SessionId) return;
            SessionTitle = title;
            m_Conversation.Title = title;

            MainThread.DispatchAndForget(() =>
            {
                SaveConversation();
                OnSessionTitleReceived?.Invoke(title);
            });
        }

        void HandlePermissionRequest(AssistantConversationId sessionId, AcpPermissionRequest request)
        {
            if (sessionId != SessionId) return;

            MainThread.DispatchAndForgetAsync(async () =>
            {
                await CalculateCostAsync(request);
                OnPermissionRequest?.Invoke(request);
            });
        }

        async Task CalculateCostAsync(AcpPermissionRequest request)
        {
            if (request.ToolCall?.Cost != null)
                return;

            if (request.ToolCall?.RawInput == null)
                return;

            var toolName = request.ToolCall.ToolName ?? request.ToolCall.Title;
            var cost = await AcpToolCostCalculator.TryGetCostAsync(toolName, request.ToolCall.RawInput);

            if (cost.HasValue)
                request.ToolCall.Cost = cost.Value;
        }

        /// <summary>
        /// Fire a permission request for an MCP tool.
        /// Called by AcpSessionRegistry when MCP tool approval is needed.
        /// </summary>
        internal void FireMcpPermissionRequest(AcpPermissionRequest request)
        {
            MainThread.DispatchAndForget(() => OnPermissionRequest?.Invoke(request));
        }

        /// <summary>
        /// Send a permission response back to the ACP agent.
        /// </summary>
        /// <param name="requestId">The JSON-RPC request ID.</param>
        /// <param name="outcome">The user's permission decision.</param>
        public async Task RespondToPermissionRequest(object requestId, AcpPermissionOutcome outcome)
        {
            await m_Client.SendPermissionResponseAsync(SessionId, requestId, outcome);
        }

        /// <summary>
        /// Capture a user prompt in the conversation history.
        /// </summary>
        void CaptureUserPrompt(object[] promptContent)
        {
            var message = new AssistantMessage
            {
                Role = "user",
                IsComplete = true,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var block = new PromptBlock
            {
                Content = JsonConvert.SerializeObject(promptContent)
            };
            message.Blocks.Add(block);

            m_Conversation.Messages.Add(message);
            m_Conversation.LastMessageTimestamp = message.Timestamp;
            SaveConversation();
        }

        /// <summary>
        /// Capture assistant message chunks in the conversation history.
        /// </summary>
        void CaptureAssistantMessage(string text, string blockType)
        {
            // If there's no current message or the last message isn't an assistant message, create a new one
            if (m_CurrentMessage == null || m_CurrentMessage.Role != "assistant")
            {
                m_CurrentMessage = new AssistantMessage
                {
                    Role = "assistant",
                    IsComplete = false,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                m_Conversation.Messages.Add(m_CurrentMessage);
            }

            // Only append to the last block if it's a ResponseBlock
            // This preserves the interleaving of text and tool calls
            ResponseBlock block = null;
            if (m_CurrentMessage.Blocks.Count > 0)
            {
                var lastBlock = m_CurrentMessage.Blocks[m_CurrentMessage.Blocks.Count - 1];
                if (lastBlock is ResponseBlock responseBlock)
                {
                    block = responseBlock;
                }
            }

            // If no suitable block found, create a new one
            if (block == null)
            {
                block = new ResponseBlock { Content = "" };
                m_CurrentMessage.Blocks.Add(block);
            }

            // Append text to the block
            block.Content += text;

            m_Conversation.LastMessageTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            SaveConversation();
        }

        /// <summary>
        /// Capture a tool call in the conversation history.
        /// </summary>
        void CaptureToolCall(JObject toolCallUpdate)
        {
            if (toolCallUpdate == null) return;

            // Ensure we have a current assistant message
            if (m_CurrentMessage == null || m_CurrentMessage.Role != "assistant")
            {
                m_CurrentMessage = new AssistantMessage
                {
                    Role = "assistant",
                    IsComplete = false,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                m_Conversation.Messages.Add(m_CurrentMessage);
            }

            // Extract toolCallId to check for existing block
            var toolCallId = toolCallUpdate?["toolCallId"]?.ToString();
            if (string.IsNullOrEmpty(toolCallId))
            {
                Debug.LogWarning("ACP: Tool call update missing toolCallId");
                return;
            }

            // Check if we already have a block for this tool call
            var existingBlock = m_CurrentMessage.Blocks.OfType<AcpToolCallStorageBlock>().FirstOrDefault(b =>
            {
                try
                {
                    return b.ToolCallData?["toolCallId"]?.ToString() == toolCallId;
                }
                catch
                {
                    return false;
                }
            });

            if (existingBlock != null)
            {
                // Update the existing block with the new data
                existingBlock.ToolCallData = toolCallUpdate;
            }
            else
            {
                // Create a new block for this tool call
                var block = new AcpToolCallStorageBlock
                {
                    ToolCallData = toolCallUpdate
                };
                m_CurrentMessage.Blocks.Add(block);
            }

            m_Conversation.LastMessageTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            SaveConversation();
        }

        /// <summary>
        /// Capture a tool call update in the conversation history.
        /// </summary>
        void CaptureToolCallUpdate(JObject toolCallUpdate)
        {
            if (toolCallUpdate == null) return;

            // Ensure we have a current assistant message
            if (m_CurrentMessage == null || m_CurrentMessage.Role != "assistant")
            {
                Debug.LogWarning("ACP: Received tool call update without an active assistant message");
                return;
            }

            // Extract toolCallId
            var toolCallId = toolCallUpdate?["toolCallId"]?.ToString();
            if (string.IsNullOrEmpty(toolCallId))
            {
                Debug.LogWarning("ACP: Tool call update missing toolCallId");
                return;
            }

            // Find the existing tool call block
            var existingBlock = m_CurrentMessage.Blocks.OfType<AcpToolCallStorageBlock>().FirstOrDefault(b =>
            {
                try
                {
                    return b.ToolCallData?["toolCallId"]?.ToString() == toolCallId;
                }
                catch
                {
                    return false;
                }
            });

            if (existingBlock != null)
            {
                // Merge the update into the existing block
                try
                {
                    var existingJson = existingBlock.ToolCallData;

                    // Update status if present
                    if (toolCallUpdate["status"] != null)
                    {
                        existingJson["status"] = toolCallUpdate["status"];
                    }

                    // Update other fields from the update
                    foreach (var prop in toolCallUpdate.Properties())
                    {
                        // Skip sessionUpdate as it's not needed in storage
                        if (prop.Name != "sessionUpdate")
                        {
                            existingJson[prop.Name] = prop.Value;
                        }
                    }

                    m_Conversation.LastMessageTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    SaveConversation();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"ACP: Failed to merge tool call update: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"ACP: Tool call update for unknown toolCallId: {toolCallId}");
            }
        }

        /// <summary>
        /// Mark the current assistant message as complete.
        /// </summary>
        void CompleteCurrentMessage()
        {
            if (m_CurrentMessage != null)
            {
                m_CurrentMessage.IsComplete = true;
                m_CurrentMessage = null;
                SaveConversation();
            }
        }

        /// <summary>
        /// Save the conversation to disk using explicit JSON serialization.
        /// </summary>
        void SaveConversation()
        {
            if (string.IsNullOrEmpty(m_Conversation.AgentSessionId))
                return; // Can't save until we have the agent session ID

            // Don't save conversations until they have at least one message
            if (m_Conversation.Messages.Count == 0)
                return;

            AcpConversationStorage.Save(m_Conversation);
        }

        /// <summary>
        /// Clears the current conversation and creates a fresh one.
        /// Used when storage is cleared while session is active.
        /// </summary>
        public void ClearConversation()
        {
            m_Conversation = new AssistantConversation
            {
                Id = SessionId,
                Title = ProviderId ?? "ACP Session",
                ProviderId = ProviderId,
                CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                LastMessageTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        /// <summary>
        /// Format an ACP error for display, extracting meaningful text from nested JSON.
        /// The error message often contains escaped JSON with further escaped JSON inside.
        /// </summary>
        static string FormatAcpError(string message, JToken errorData)
        {
            try
            {
                // The message format is often: "Internal error: API Error: 400 {escaped JSON}"
                // We need to extract and parse the embedded JSON to get the actual error

                int jsonStart = message.IndexOf('{');
                if (jsonStart < 0)
                {
                    // No embedded JSON, just clean up escape sequences
                    return CleanupErrorText(message);
                }

                var prefix = message.Substring(0, jsonStart).Trim();
                var jsonPart = message.Substring(jsonStart);

                // Parse the outer JSON
                var outerJson = JToken.Parse(jsonPart);
                var innerMessage = outerJson["error"]?["message"]?.ToString()
                                ?? outerJson["message"]?.ToString()
                                ?? "";

                // The inner message might start with JSON too: '{"message":"actual error"}. More text here'
                var lines = new List<string>();

                // Add the prefix (e.g., "Internal error: API Error: 400")
                if (!string.IsNullOrEmpty(prefix))
                {
                    lines.Add(prefix);
                }

                if (innerMessage.StartsWith("{"))
                {
                    // Find where the JSON ends (look for }. or just })
                    int braceCount = 0;
                    int jsonEnd = -1;
                    for (int i = 0; i < innerMessage.Length; i++)
                    {
                        if (innerMessage[i] == '{') braceCount++;
                        else if (innerMessage[i] == '}')
                        {
                            braceCount--;
                            if (braceCount == 0)
                            {
                                jsonEnd = i;
                                break;
                            }
                        }
                    }

                    if (jsonEnd > 0)
                    {
                        var innerJsonPart = innerMessage.Substring(0, jsonEnd + 1);
                        var restOfMessage = innerMessage.Substring(jsonEnd + 1).TrimStart('.', ' ');

                        // Parse the innermost JSON to get the actual error
                        var innerJson = JToken.Parse(innerJsonPart);
                        var actualError = innerJson["message"]?.ToString() ?? innerJsonPart;
                        lines.Add(actualError);

                        // Add the rest of the message (e.g., "Received Model Group=...")
                        if (!string.IsNullOrEmpty(restOfMessage))
                        {
                            // Split on \n and add each line
                            foreach (var line in restOfMessage.Split(new[] { "\\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                lines.Add(line.Trim());
                            }
                        }
                    }
                    else
                    {
                        lines.Add(CleanupErrorText(innerMessage));
                    }
                }
                else
                {
                    // No nested JSON, just split on newlines
                    foreach (var line in innerMessage.Split(new[] { "\\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        lines.Add(line.Trim());
                    }
                }

                return string.Join("\n", lines);
            }
            catch
            {
                // Parsing failed, just clean up the original message
                return CleanupErrorText(message);
            }
        }

        static string CleanupErrorText(string text)
        {
            return text
                .Replace("\\n", "\n")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;


            m_Client.OnMessage -= HandleMessage;
            m_Client.OnSessionStarted -= HandleSessionStarted;
            m_Client.OnSessionEnded -= HandleSessionEnded;
            m_Client.OnSessionError -= HandleSessionError;
            m_Client.OnPermissionRequest -= HandlePermissionRequest;
            m_Client.OnAgentSessionId -= HandleAgentSessionId;
            m_Client.OnSessionTitle -= HandleSessionTitle;
            m_Client.OnConnectionStateChanged -= HandleConnectionStateChanged;
        }
    }
}
