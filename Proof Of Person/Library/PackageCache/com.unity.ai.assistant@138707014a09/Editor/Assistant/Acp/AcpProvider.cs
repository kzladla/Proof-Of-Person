using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Agents;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using Unity.Relay.Editor;
using Unity.Relay.Editor.Acp;
using UnityEngine;
using TaskUtils = Unity.AI.Assistant.Editor.Utils.TaskUtils;

namespace Unity.AI.Assistant.Editor.Acp
{
    /// <summary>
    /// IAssistantProvider implementation that wraps ACP sessions.
    /// Session lifecycle is managed internally - no external Start/End calls needed.
    /// </summary>
    class AcpProvider : IAssistantProvider, IDisposable
    {
        readonly string m_ProviderId;

        AcpSession m_CurrentSession;
        AssistantConversationId m_ActiveSessionId;

        /// <summary>
        /// Sets the current session, automatically handling event binding/unbinding.
        /// </summary>
        AcpSession CurrentSession
        {
            set
            {
                if (m_CurrentSession == value)
                    return;

                if (m_CurrentSession != null)
                    UnbindSessionEvents(m_CurrentSession);

                m_CurrentSession = value;

                if (m_CurrentSession != null)
                    BindSessionEvents(m_CurrentSession);
            }
        }

        /// <summary>
        /// The conversation is owned by the session. This property provides access to it.
        /// </summary>
        AssistantConversation Conversation => m_CurrentSession?.Conversation;

        // Streaming state
        int m_CurrentStreamingMessageIndex = -1;
        int m_CurrentResponseBlockIndex = -1;
        int m_CurrentThoughtBlockIndex = -1;
        bool m_IsReasoningPhaseActive;
        bool m_HadToolCallThisTurn;
        bool m_Disposed;
        bool m_CancelRequested;

        // Pending permission requests by tool call ID
        readonly Dictionary<string, (object requestId, AcpPermissionOption[] options)> m_PendingPermissions = new();

        // Track tool call blocks by tool call ID for updates (storage blocks for persistence)
        readonly Dictionary<string, AcpToolCallStorageBlock> m_ToolCallBlocks = new();

        public string ProviderId => m_ProviderId;

        // Properties - ACP doesn't support some Unity-specific features
        readonly IToolPermissions m_ToolPermissions = new AllowAllToolPermissions(); // No tool permissions support yet
        public IToolPermissions ToolPermissions => m_ToolPermissions;
        public IFunctionCaller FunctionCaller => null;
        public bool SessionStatusTrackingEnabled => false;
        public bool AutoRunSettingAvailable => false;

#pragma warning disable CS0067
        // IAssistantProvider events
        public event Action<IEnumerable<AssistantConversationInfo>> ConversationsRefreshed;
        public event Action<AssistantConversationId, Assistant.PromptState> PromptStateChanged;
        public event Action<AssistantConversation> ConversationLoaded;
        public event Action<AssistantConversation> ConversationChanged;
        public event Action<AssistantConversation> ConversationCreated;
        public event Action<AssistantConversationId> ConversationDeleted;
        public event Action<AssistantConversationId, ErrorInfo> ConversationErrorOccured;
        public event Action<AssistantMessageId, FeedbackData?> FeedbackLoaded;
        public event Action<AssistantMessageId, int?, bool> MessageCostReceived;
        public event Action<AssistantConversationId, string> IncompleteMessageStarted;
        public event Action<AssistantConversationId> IncompleteMessageCompleted;

        // Capability events - AcpProvider fires these
        public event Action<(string id, string name, string desc)[], string> ModesAvailable;
        public event Action<(string modelId, string name, string description)[], string> ModelsAvailable;
        public event Action<string> ModeChanged;
        public event Action<(string name, string description)[]> AvailableCommandsChanged;
#pragma warning restore CS0067

        /// <summary>
        /// Create and initialize an ACP provider. No session is created during construction.
        /// After event handlers are connected, caller should call EnsureSession() or ConversationLoad().
        /// </summary>
        public static async Task<AcpProvider> CreateAsync(string providerId)
        {
            var provider = new AcpProvider(providerId);
            await provider.InitializeAsync();
            return provider;
        }

        AcpProvider(string providerId)
        {
            m_ProviderId = providerId;
        }

        /// <summary>
        /// Gets the active session ID for this provider.
        /// </summary>
        public AssistantConversationId ActiveSessionId => m_ActiveSessionId;

        /// <summary>
        /// Initialize provider infrastructure (storage events, history).
        /// Does NOT create a session - caller should call EnsureSession() or ConversationLoad() after wiring events.
        /// </summary>
        async Task InitializeAsync()
        {
            // Subscribe to storage events to keep history updated
            AcpConversationStorage.OnSessionSaved += OnConversationSaved;
            AcpConversationStorage.OnStorageCleared += OnStorageCleared;

            // Load and display stored conversations in history panel
            await RefreshConversationsAsync();
        }

        // === Core IAssistantProvider implementation ===

        public Task ProcessPrompt(AssistantConversationId conversationId, AssistantPrompt prompt, IAgent agent = null, CancellationToken ct = default)
        {
            // Create session on-demand if needed
            if (m_CurrentSession == null)
            {
                EnsureSession();
            }

            m_HadToolCallThisTurn = false;
            m_CancelRequested = false;

            // Check if this is a new conversation (first prompt) - no messages yet
            var isNewConversation = Conversation.Messages.Count == 0;

            AppendUserMessage(prompt.Value);

            // Fire ConversationCreated for new conversations (after user message is added)
            if (isNewConversation)
            {
                ConversationCreated?.Invoke(Conversation);
            }

            // Inject default agents.md as VirtualAttachment if needed (first prompt only)
            if (isNewConversation)
            {
                TryInjectDefaultAgentsMd(prompt, m_ProviderId);
            }

            var content = AcpContextBuilder.BuildPromptContent(
                prompt.Value,
                prompt.ConsoleAttachments,
                prompt.VirtualAttachments);

            PromptStateChanged?.Invoke(Conversation.Id, Assistant.PromptState.AwaitingServer);
            return m_CurrentSession.SendPromptAsync(content);
        }

        /// <summary>
        /// Ensures a session exists, creating one if needed.
        /// Session initialization happens in background.
        /// </summary>
        public void EnsureSession()
        {
            if (m_CurrentSession != null)
                return;

            ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Initializing);

            m_ActiveSessionId = AcpSessionRegistry.GenerateSessionId(m_ProviderId);
            CurrentSession = AcpSessionRegistry.Acquire(m_ActiveSessionId, m_ProviderId);
        }

        public void AbortPrompt(AssistantConversationId conversationId)
        {
            if (m_CurrentSession == null || m_CancelRequested)
                return;

            m_CancelRequested = true;

            // Cancel pending tool calls and update UI first
            CancelPendingToolCalls(completeStreamingMessage: false);
            RefreshAssetDatabaseIfToolCall();

            // Update state to canceling while waiting for stop response
            PromptStateChanged?.Invoke(Conversation.Id, Assistant.PromptState.Canceling);

            // Then send the cancel request to the server
            TaskUtils.WithExceptionLogging(m_CurrentSession?.CancelPromptAsync());
        }

        public async Task EndSessionAsync(AssistantConversationId conversationId)
        {
            if (m_CurrentSession == null || m_Disposed)
                return;

            if (conversationId.IsValid && m_ActiveSessionId.IsValid && conversationId != m_ActiveSessionId)
                return;

            try
            {
                await m_CurrentSession.CancelPromptAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AcpProvider] Cancel prompt before end failed: {ex.Message}");
            }

            try
            {
                await CancelAllPendingPermissionsAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AcpProvider] Cancel pending permissions failed: {ex.Message}");
            }

            CancelPendingToolCalls(completeStreamingMessage: true);
            RefreshAssetDatabaseIfToolCall();

            var sessionId = m_ActiveSessionId;
            m_ActiveSessionId = AssistantConversationId.Invalid;
            CurrentSession = null;

            try
            {
                await AcpSessionRegistry.ReleaseAsync(sessionId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AcpProvider] End session failed: {ex.Message}");
            }

            ResetStreamingState();
        }

        // Mode/model - delegates to session
        public Task SetModeAsync(string modeId)
            => m_CurrentSession?.SetModeAsync(modeId) ?? Task.CompletedTask;

        public Task SetModelAsync(string modelId)
            => m_CurrentSession?.SetModelAsync(modelId) ?? Task.CompletedTask;

        // === Event mapping from AcpSession ===

        void BindSessionEvents(AcpSession session)
        {
            session.OnSessionInitialized += OnSessionInitialized;
            session.OnModeChanged += OnModeChanged;
            session.OnAvailableCommandsUpdated += OnAvailableCommandsUpdated;
            session.OnSessionTitleReceived += OnSessionTitleReceived;
            session.OnAgentSessionIdReceived += OnAgentSessionIdReceived;
            session.OnTextChunk += OnTextChunk;
            session.OnThoughtChunk += OnThoughtChunk;
            session.OnResponseComplete += OnResponseComplete;
            session.OnError += OnError;
            session.OnSessionEnded += OnSessionEnded;
            session.OnToolCall += OnToolCall;
            session.OnToolCallUpdate += OnToolCallUpdate;
            session.OnPermissionRequest += OnPermissionRequest;
            session.OnPlanUpdate += OnPlanUpdate;
        }

        void UnbindSessionEvents(AcpSession session)
        {
            session.OnSessionInitialized -= OnSessionInitialized;
            session.OnModeChanged -= OnModeChanged;
            session.OnAvailableCommandsUpdated -= OnAvailableCommandsUpdated;
            session.OnSessionTitleReceived -= OnSessionTitleReceived;
            session.OnAgentSessionIdReceived -= OnAgentSessionIdReceived;
            session.OnTextChunk -= OnTextChunk;
            session.OnThoughtChunk -= OnThoughtChunk;
            session.OnResponseComplete -= OnResponseComplete;
            session.OnError -= OnError;
            session.OnSessionEnded -= OnSessionEnded;
            session.OnToolCall -= OnToolCall;
            session.OnToolCallUpdate -= OnToolCallUpdate;
            session.OnPermissionRequest -= OnPermissionRequest;
            session.OnPlanUpdate -= OnPlanUpdate;
        }

        void OnSessionInitialized(
            (string id, string name, string desc)[] modes,
            string currentModeId,
            (string modelId, string name, string description)[] models,
            string currentModelId)
        {
            ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Ready);

            if (modes?.Length > 0)
                ModesAvailable?.Invoke(modes, currentModeId);
            if (models?.Length > 0)
                ModelsAvailable?.Invoke(models, currentModelId);
        }

        void OnModeChanged(string modeId)
        {
            ModeChanged?.Invoke(modeId);
        }

        void OnAvailableCommandsUpdated((string name, string description, string inputHint)[] commands)
        {
            // Convert to simpler tuple (dropping inputHint for now)
            var simplified = commands.Select(c => (c.name, c.description)).ToArray();
            AvailableCommandsChanged?.Invoke(simplified);
        }

        void OnSessionTitleReceived(string title)
        {
            if (string.IsNullOrEmpty(title))
                return;

            if (Conversation == null || Conversation.Title == title)
                return;

            Conversation.Title = title;
            ConversationChanged?.Invoke(Conversation);
        }

        void OnAgentSessionIdReceived(string agentSessionId)
        {
            // Update session state with the agent session ID so it can be restored after domain reload
            // The agent session ID is what's used for storage lookups, not the Unity routing ID
            // Uses the same key as AssistantUISessionState.LastActiveConversationId
            UnityEditor.SessionState.SetString("AssistantUserSession_LastActiveConversationId", agentSessionId);
        }

        void OnTextChunk(string chunk)
        {
            AppendResponseChunk(chunk);
        }

        void OnThoughtChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return;

            EnsureAssistantMessage();

            if (!m_IsReasoningPhaseActive)
            {
                AppendResponseChunk(chunk);
                return;
            }

            CloseCurrentResponseBlock();
            EnsureThoughtBlock();

            if (Conversation == null || m_CurrentStreamingMessageIndex < 0 || m_CurrentThoughtBlockIndex < 0)
                return;

            var message = Conversation.Messages[m_CurrentStreamingMessageIndex];
            if (message.Blocks[m_CurrentThoughtBlockIndex] is ThoughtBlock thoughtBlock)
            {
                thoughtBlock.Content += chunk;
                ConversationChanged?.Invoke(Conversation);
            }
        }

        void AppendResponseChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return;

            EnsureAssistantMessage();
            m_IsReasoningPhaseActive = false;
            CloseCurrentThoughtBlock();
            EnsureResponseBlock();

            if (Conversation == null || m_CurrentStreamingMessageIndex < 0 || m_CurrentResponseBlockIndex < 0)
                return;

            var message = Conversation.Messages[m_CurrentStreamingMessageIndex];
            if (message.Blocks[m_CurrentResponseBlockIndex] is ResponseBlock responseBlock)
            {
                responseBlock.Content += chunk;
                ConversationChanged?.Invoke(Conversation);
            }
        }

        void OnResponseComplete()
        {
            m_CancelRequested = false;

            // Complete any streaming message if one exists
            if (Conversation != null && m_CurrentStreamingMessageIndex >= 0)
            {
                var message = Conversation.Messages[m_CurrentStreamingMessageIndex];
                message.IsComplete = true;

                // Mark all response blocks as complete
                foreach (var block in message.Blocks)
                {
                    if (block is ResponseBlock responseBlock)
                    {
                        responseBlock.IsComplete = true;
                    }
                }
            }

            m_CurrentStreamingMessageIndex = -1;
            m_CurrentResponseBlockIndex = -1;
            m_CurrentThoughtBlockIndex = -1;
            m_IsReasoningPhaseActive = false;

            PromptStateChanged?.Invoke(Conversation.Id, Assistant.PromptState.Connected);

            if (Conversation != null)
            {
                ConversationChanged?.Invoke(Conversation);
            }

            RefreshAssetDatabaseIfToolCall();
        }

        void OnError(string error)
        {
            // Set to true so AbortPrompt (called by UI's OnConversationErrorOccured) is a no-op
            m_CancelRequested = true;

            // Show error in banner only - not in conversation
            ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Error, error);

            m_CurrentStreamingMessageIndex = -1;
            m_CurrentResponseBlockIndex = -1;
            m_CurrentThoughtBlockIndex = -1;
            m_IsReasoningPhaseActive = false;

            // Reset prompt state so the UI stops showing spinner/canceling
            PromptStateChanged?.Invoke(Conversation.Id, Assistant.PromptState.Connected);
        }

        void OnSessionEnded()
        {
            // Session ended unexpectedly - fire an error
            if (!m_Disposed)
            {
                ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Error, "Session ended unexpectedly");

                var error = new ErrorInfo("Session ended unexpectedly", "The ACP session was terminated");
                ConversationErrorOccured?.Invoke(Conversation?.Id ?? m_ActiveSessionId, error);
            }
        }

        // === Tool call and permission handling ===

        /// <summary>
        /// Marks all pending tool calls as cancelled/failed and clears tracking state.
        /// Called when the conversation is cancelled or disconnected.
        /// </summary>
        void CancelPendingToolCalls(bool completeStreamingMessage)
        {
            if (Conversation == null)
                return;

            bool anyUpdated = false;

            // Update all pending tool call blocks to failed status
            foreach (var kvp in m_ToolCallBlocks)
            {
                var block = kvp.Value;
                block.ToolCallData ??= new JObject();
                var currentStatus = GetCurrentStatus(block.ToolCallData);

                if (currentStatus == AcpToolCallStatus.Pending)
                {
                    // Create synthetic update to mark as failed/cancelled
                    var update = new AcpToolCallUpdate
                    {
                        ToolCallId = kvp.Key,
                        Status = AcpToolCallStatus.Failed,
                        Content = "Cancelled"
                    };
                    ApplyUpdateToData(block.ToolCallData, update);
                    SetUnityLatestUpdate(block.ToolCallData, update);
                    anyUpdated = true;
                }

                // If there's a pending permission, mark it as cancelled
                // This sets PermissionResponse so HasPendingPermission returns false
                // and the UI will dismiss the permission element
                if (HasPendingPermission(block.ToolCallData))
                {
                    SetPermissionResponse(block.ToolCallData, AcpPermissionOutcome.Cancelled());
                    anyUpdated = true;
                }
            }

            // Clear pending permissions tracking (user cannot respond to them after cancel)
            m_PendingPermissions.Clear();

            // Complete the streaming message if one exists
            if (completeStreamingMessage)
            {
                if (m_CurrentStreamingMessageIndex >= 0 && m_CurrentStreamingMessageIndex < Conversation.Messages.Count)
                {
                    var message = Conversation.Messages[m_CurrentStreamingMessageIndex];
                    message.IsComplete = true;
                }

                // Reset streaming state
                m_CurrentStreamingMessageIndex = -1;
                m_CurrentResponseBlockIndex = -1;
                m_CurrentThoughtBlockIndex = -1;
                m_IsReasoningPhaseActive = false;
            }

            if (anyUpdated)
            {
                ConversationChanged?.Invoke(Conversation);
                SaveConversationIfPossible();
            }
        }

        void RefreshAssetDatabaseIfToolCall()
        {
            if (!m_HadToolCallThisTurn)
                return;

            m_HadToolCallThisTurn = false;
            EditorApplication.delayCall += () => AssetDatabase.Refresh();
        }

        void CloseCurrentResponseBlock()
        {
            if (Conversation == null || m_CurrentStreamingMessageIndex < 0)
                return;

            var message = Conversation.Messages[m_CurrentStreamingMessageIndex];
            if (m_CurrentResponseBlockIndex < 0 || m_CurrentResponseBlockIndex >= message.Blocks.Count)
                return;

            if (message.Blocks[m_CurrentResponseBlockIndex] is ResponseBlock responseBlock)
            {
                responseBlock.IsComplete = true;
            }

            m_CurrentResponseBlockIndex = -1;
        }

        void CloseCurrentThoughtBlock()
        {
            m_CurrentThoughtBlockIndex = -1;
        }

        static string ToStatusString(AcpToolCallStatus status)
        {
            return status switch
            {
                AcpToolCallStatus.Completed => AcpConstants.Status_Completed,
                AcpToolCallStatus.Failed => AcpConstants.Status_Failed,
                _ => AcpConstants.Status_Pending
            };
        }

        static JObject GetOrCreateUnityData(JObject toolCallData)
        {
            if (toolCallData == null)
                return null;

            if (toolCallData[AcpToolCallStorageKeys.UnityDataKey] is JObject unityData)
                return unityData;

            unityData = new JObject();
            toolCallData[AcpToolCallStorageKeys.UnityDataKey] = unityData;
            return unityData;
        }

        static void SetUnityField(JObject toolCallData, string key, JToken value)
        {
            var unityData = GetOrCreateUnityData(toolCallData);
            if (unityData == null)
                return;

            if (value == null || value.Type == JTokenType.Null)
            {
                unityData.Remove(key);
                return;
            }

            unityData[key] = value;
        }

        static void ApplyCallInfoToData(JObject toolCallData, AcpToolCallInfo info, bool isReasoning)
        {
            if (toolCallData == null || info == null)
                return;

            toolCallData["toolCallId"] = info.ToolCallId;
            if (!string.IsNullOrEmpty(info.Title))
                toolCallData["title"] = info.Title;
            toolCallData["status"] = ToStatusString(info.Status);

            if (!string.IsNullOrEmpty(info.ToolName))
            {
                if (toolCallData["_meta"] is not JObject meta)
                {
                    meta = new JObject();
                    toolCallData["_meta"] = meta;
                }
                meta["toolName"] = info.ToolName;
            }

            if (!string.IsNullOrEmpty(info.Description))
            {
                toolCallData["rawInput"] = new JObject
                {
                    ["description"] = info.Description
                };
            }

            // Preserve reasoning marker for legacy parsing.
            if (isReasoning)
                toolCallData["kind"] = "think";

            SetUnityField(toolCallData, AcpToolCallStorageKeys.UnityCallInfoKey, JObject.FromObject(info));
            SetUnityField(toolCallData, AcpToolCallStorageKeys.UnityIsReasoningKey, new JValue(isReasoning));
        }

        static void ApplyUpdateToData(JObject toolCallData, AcpToolCallUpdate update)
        {
            if (toolCallData == null || update == null)
                return;

            if (!string.IsNullOrEmpty(update.ToolCallId))
                toolCallData["toolCallId"] = update.ToolCallId;
            toolCallData["status"] = ToStatusString(update.Status);

            if (!string.IsNullOrEmpty(update.ToolName))
            {
                if (toolCallData["_meta"] is not JObject meta)
                {
                    meta = new JObject();
                    toolCallData["_meta"] = meta;
                }
                meta["toolName"] = update.ToolName;
            }
        }

        static void SetPendingPermission(JObject toolCallData, AcpPermissionRequest request)
        {
            if (toolCallData == null)
                return;

            SetUnityField(toolCallData, AcpToolCallStorageKeys.UnityPendingPermissionKey,
                request != null ? JObject.FromObject(request) : null);
            // Clear any previous response when a new permission is pending.
            if (request != null)
            {
                SetUnityField(toolCallData, AcpToolCallStorageKeys.UnityPermissionResponseKey, null);
            }
        }

        static void SetPermissionResponse(JObject toolCallData, AcpPermissionOutcome outcome)
        {
            if (toolCallData == null)
                return;

            SetUnityField(toolCallData, AcpToolCallStorageKeys.UnityPermissionResponseKey,
                outcome != null ? JObject.FromObject(outcome) : null);
        }

        static void SetUnityLatestUpdate(JObject toolCallData, AcpToolCallUpdate update)
        {
            if (toolCallData == null)
                return;

            SetUnityField(toolCallData, AcpToolCallStorageKeys.UnityLatestUpdateKey,
                update != null ? JObject.FromObject(update) : null);
        }

        static AcpToolCallStatus GetCurrentStatus(JObject toolCallData)
        {
            if (toolCallData == null)
                return AcpToolCallStatus.Pending;

            var unityData = toolCallData[AcpToolCallStorageKeys.UnityDataKey] as JObject;
            var unityUpdate = unityData?[AcpToolCallStorageKeys.UnityLatestUpdateKey]?.ToObject<AcpToolCallUpdate>();
            if (unityUpdate != null)
                return unityUpdate.Status;

            var unityCallInfo = unityData?[AcpToolCallStorageKeys.UnityCallInfoKey]?.ToObject<AcpToolCallInfo>();
            if (unityCallInfo != null)
                return unityCallInfo.Status;

            return toolCallData["status"]?.ToString() switch
            {
                AcpConstants.Status_Completed => AcpToolCallStatus.Completed,
                AcpConstants.Status_Failed => AcpToolCallStatus.Failed,
                _ => AcpToolCallStatus.Pending
            };
        }

        static bool HasPendingPermission(JObject toolCallData)
        {
            if (toolCallData == null)
                return false;

            var unityData = toolCallData[AcpToolCallStorageKeys.UnityDataKey] as JObject;
            if (unityData == null)
                return false;

            var pending = unityData[AcpToolCallStorageKeys.UnityPendingPermissionKey];
            var response = unityData[AcpToolCallStorageKeys.UnityPermissionResponseKey];
            return pending != null && pending.Type != JTokenType.Null &&
                   (response == null || response.Type == JTokenType.Null);
        }

        static string GetToolCallId(JObject toolCallData)
        {
            if (toolCallData == null)
                return null;

            var unityData = toolCallData[AcpToolCallStorageKeys.UnityDataKey] as JObject;
            var unityCallInfo = unityData?[AcpToolCallStorageKeys.UnityCallInfoKey]?.ToObject<AcpToolCallInfo>();
            if (!string.IsNullOrEmpty(unityCallInfo?.ToolCallId))
                return unityCallInfo.ToolCallId;

            var unityUpdate = unityData?[AcpToolCallStorageKeys.UnityLatestUpdateKey]?.ToObject<AcpToolCallUpdate>();
            if (!string.IsNullOrEmpty(unityUpdate?.ToolCallId))
                return unityUpdate.ToolCallId;

            return toolCallData["toolCallId"]?.ToString();
        }

        static bool GetIsReasoning(JObject toolCallData)
        {
            if (toolCallData == null)
                return false;

            var unityData = toolCallData[AcpToolCallStorageKeys.UnityDataKey] as JObject;
            var unityIsReasoning = unityData?[AcpToolCallStorageKeys.UnityIsReasoningKey]?.Value<bool>();
            if (unityIsReasoning.HasValue)
                return unityIsReasoning.Value;

            return toolCallData["kind"]?.ToString() == "think";
        }

        static object NormalizeRequestId(object requestId)
        {
            return requestId switch
            {
                JValue jValue => jValue.Value,
                JToken jToken => jToken.ToObject<object>(),
                _ => requestId
            };
        }

        void SaveConversationIfPossible()
        {
            if (Conversation == null)
                return;

            if (string.IsNullOrEmpty(Conversation.AgentSessionId) || string.IsNullOrEmpty(Conversation.ProviderId))
                return;

            AcpConversationStorage.Save(Conversation);
        }

        void OnToolCall(AcpToolCallInfo info)
        {
            if (string.IsNullOrEmpty(info.ToolCallId))
                return;

            m_HadToolCallThisTurn = true;

            // Check if we already have a block for this tool call (update case)
            if (m_ToolCallBlocks.TryGetValue(info.ToolCallId, out var existingBlock))
            {
                // Update existing block's call info (e.g., title changes from "Terminal" to actual command)
                existingBlock.ToolCallData ??= new JObject();
                var isReasoning = GetIsReasoning(existingBlock.ToolCallData) || m_IsReasoningPhaseActive;
                ApplyCallInfoToData(existingBlock.ToolCallData, info, isReasoning);
                ConversationChanged?.Invoke(Conversation);
                return;
            }

            // New tool call - create block
            CloseCurrentThoughtBlock();
            CloseCurrentResponseBlock();
            EnsureAssistantMessage();

            if (Conversation == null || m_CurrentStreamingMessageIndex < 0)
                return;

            var message = Conversation.Messages[m_CurrentStreamingMessageIndex];

            var toolCallData = new JObject();
            ApplyCallInfoToData(toolCallData, info, m_IsReasoningPhaseActive);

            var block = new AcpToolCallStorageBlock
            {
                ToolCallData = toolCallData
            };

            message.Blocks.Add(block);
            m_ToolCallBlocks[info.ToolCallId] = block;

            // Reset response block index so subsequent text creates a new ResponseBlock
            m_CurrentResponseBlockIndex = -1;

            ConversationChanged?.Invoke(Conversation);
        }

        void OnToolCallUpdate(AcpToolCallUpdate update)
        {
            if (string.IsNullOrEmpty(update.ToolCallId))
                return;

            m_HadToolCallThisTurn = true;

            CloseCurrentThoughtBlock();

            // Find the tool call block and update it
            if (m_ToolCallBlocks.TryGetValue(update.ToolCallId, out var block))
            {
                block.ToolCallData ??= new JObject();
                ApplyUpdateToData(block.ToolCallData, update);
                SetUnityLatestUpdate(block.ToolCallData, update);
                ConversationChanged?.Invoke(Conversation);
            }
            // Fallback: For MCP approval flow, the agent's toolCallId differs from the MCP approval's toolCallId.
            // The tool response includes _meta.toolExecutionId which matches the MCP approval's ID.
            // Use this to link the result back to the correct UI element.
            else if (!string.IsNullOrEmpty(update.ToolExecutionId) &&
                     m_ToolCallBlocks.TryGetValue(update.ToolExecutionId, out block))
            {
                block.ToolCallData ??= new JObject();
                ApplyUpdateToData(block.ToolCallData, update);
                SetUnityLatestUpdate(block.ToolCallData, update);
                ConversationChanged?.Invoke(Conversation);
            }
        }

        void OnPermissionRequest(AcpPermissionRequest request)
        {
            var toolCallId = request.ToolCall?.ToolCallId;
            if (string.IsNullOrEmpty(toolCallId))
                return;

            CloseCurrentThoughtBlock();
            CloseCurrentResponseBlock();

            // Store pending request for later response
            m_PendingPermissions[toolCallId] = (NormalizeRequestId(request.RequestId), request.Options);

            // Try to find existing tool call block
            var callInfo = new AcpToolCallInfo
            {
                ToolCallId = request.ToolCall.ToolCallId,
                Title = request.ToolCall.Title,
                ToolName = request.ToolCall.ToolName ?? request.ToolCall.Title,
                Status = AcpToolCallStatus.Pending,
                Description = request.ToolCall.RawInput?["description"]?.ToString()
            };

            if (!m_ToolCallBlocks.TryGetValue(toolCallId, out var block))
            {
                // Per ACP spec, request_permission.toolCall contains full display info.
                // We create a block from it if none exists, supporting agents that
                // send request_permission before or without a separate tool_call event.
                EnsureAssistantMessage();

                if (Conversation == null || m_CurrentStreamingMessageIndex < 0)
                    return;

                var message = Conversation.Messages[m_CurrentStreamingMessageIndex];

                var toolCallData = new JObject();
                ApplyCallInfoToData(toolCallData, callInfo, isReasoning: false);

                block = new AcpToolCallStorageBlock
                {
                    ToolCallData = toolCallData
                };

                message.Blocks.Add(block);
                m_ToolCallBlocks[toolCallId] = block;
                m_CurrentResponseBlockIndex = -1;
            }

            // Update the tool call block with the pending permission
            block.ToolCallData ??= new JObject();
            var permissionIsReasoning = GetIsReasoning(block.ToolCallData);
            ApplyCallInfoToData(block.ToolCallData, callInfo, permissionIsReasoning);
            SetPendingPermission(block.ToolCallData, request);
            ConversationChanged?.Invoke(Conversation);
            SaveConversationIfPossible();
        }

        void OnPlanUpdate(AcpPlanBlock planBlock)
        {
            CloseCurrentThoughtBlock();
            CloseCurrentResponseBlock();
            EnsureAssistantMessage();

            if (Conversation == null || m_CurrentStreamingMessageIndex < 0)
                return;

            var message = Conversation.Messages[m_CurrentStreamingMessageIndex];
            var storageData = new AcpPlanStorageData();
            if (planBlock != null)
            {
                foreach (var entry in planBlock.Entries)
                {
                    storageData.Entries.Add(new AcpPlanStorageEntry
                    {
                        Content = entry.Content ?? "",
                        Status = string.IsNullOrEmpty(entry.Status) ? "pending" : entry.Status,
                        Priority = entry.Priority ?? ""
                    });
                }
            }

            var planData = JObject.FromObject(storageData);

            message.Blocks.Add(new AcpPlanStorageBlock { PlanData = planData });

            ConversationChanged?.Invoke(Conversation);
            SaveConversationIfPossible();
        }

        /// <summary>
        /// Respond to a pending permission request for a tool call.
        /// </summary>
        public async Task RespondToPermissionAsync(string toolCallId, ToolPermissions.UserAnswer answer)
        {
            if (m_CurrentSession == null)
                return;

            if (!m_PendingPermissions.TryGetValue(toolCallId, out var pending))
            {
                Debug.LogWarning($"[AcpProvider] No pending permission for tool call {toolCallId}");
                return;
            }

            // Check if this is an MCP tool approval (not a native ACP permission)
            if (McpToolApprovalHandler.IsPending(toolCallId))
            {
                HandleMcpPermissionResponse(toolCallId, answer);
                m_PendingPermissions.Remove(toolCallId);
                return;
            }

            // Native ACP permission flow
            var optionId = AcpPermissionMapping.FindOptionId(pending.options, answer);
            var outcome = optionId != null
                ? AcpPermissionOutcome.Selected(optionId)
                : AcpPermissionOutcome.Cancelled();

            await m_CurrentSession.RespondToPermissionRequest(pending.requestId, outcome);

            if (m_ToolCallBlocks.TryGetValue(toolCallId, out var block))
            {
                block.ToolCallData ??= new JObject();
                SetPermissionResponse(block.ToolCallData, outcome);
                ConversationChanged?.Invoke(Conversation);
                SaveConversationIfPossible();
            }

            m_PendingPermissions.Remove(toolCallId);
        }

        async Task CancelAllPendingPermissionsAsync()
        {
            if (m_CurrentSession == null || m_PendingPermissions.Count == 0)
                return;

            var hadUpdates = false;
            var pending = m_PendingPermissions.ToArray();

            foreach (var kvp in pending)
            {
                var toolCallId = kvp.Key;
                var requestId = kvp.Value.requestId;

                if (McpToolApprovalHandler.IsPending(toolCallId))
                {
                    HandleMcpPermissionResponse(toolCallId, Unity.AI.Assistant.FunctionCalling.ToolPermissions.UserAnswer.DenyOnce);
                    hadUpdates = true;
                    continue;
                }

                try
                {
                    var outcome = AcpPermissionOutcome.Cancelled();
                    await m_CurrentSession.RespondToPermissionRequest(requestId, outcome);

                    if (m_ToolCallBlocks.TryGetValue(toolCallId, out var block))
                    {
                        block.ToolCallData ??= new JObject();
                        SetPermissionResponse(block.ToolCallData, outcome);
                        hadUpdates = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AcpProvider] Permission cancel failed for {toolCallId}: {ex.Message}");
                }
            }

            m_PendingPermissions.Clear();

            if (hadUpdates && Conversation != null)
            {
                ConversationChanged?.Invoke(Conversation);
                SaveConversationIfPossible();
            }
        }

        /// <summary>
        /// Handle permission response for MCP tools (Codex).
        /// MCP tools use a different approval flow than native ACP permissions.
        /// </summary>
        void HandleMcpPermissionResponse(string toolCallId, Unity.AI.Assistant.FunctionCalling.ToolPermissions.UserAnswer answer)
        {
            var approved = answer != Unity.AI.Assistant.FunctionCalling.ToolPermissions.UserAnswer.DenyOnce;
            var alwaysAllow = answer == Unity.AI.Assistant.FunctionCalling.ToolPermissions.UserAnswer.AllowAlways;

            McpToolApprovalHandler.Complete(toolCallId, approved, approved ? "User approved" : "User rejected", alwaysAllow);

            if (m_ToolCallBlocks.TryGetValue(toolCallId, out var block))
            {
                block.ToolCallData ??= new JObject();
                var outcomeId = AcpPermissionMapping.ToAcpKind(answer);
                SetPermissionResponse(block.ToolCallData, AcpPermissionOutcome.Selected(outcomeId));

                // For MCP tools, create a synthetic update to mark as complete/failed
                // (MCP results come back through MCP, not ACP's tool_call_update)
                var update = new AcpToolCallUpdate
                {
                    ToolCallId = toolCallId,
                    Status = approved ? AcpToolCallStatus.Completed : AcpToolCallStatus.Failed,
                    Content = approved ? "Permission granted" : "Permission denied"
                };
                ApplyUpdateToData(block.ToolCallData, update);
                SetUnityLatestUpdate(block.ToolCallData, update);

                ConversationChanged?.Invoke(Conversation);
                SaveConversationIfPossible();
            }
        }

        // === Conversation management ===

        void AppendUserMessage(string text)
        {
            var userMessage = new AssistantMessage
            {
                Id = AssistantMessageId.GetNextInternalId(m_ActiveSessionId),
                Role = Assistant.k_UserRole,
                IsComplete = true,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            userMessage.Blocks.Add(new PromptBlock { Content = text });
            Conversation.Messages.Add(userMessage);
            Conversation.LastMessageTimestamp = userMessage.Timestamp;

            ConversationChanged?.Invoke(Conversation);
        }

        void EnsureAssistantMessage()
        {
            if (m_CurrentStreamingMessageIndex >= 0)
                return;

            var assistantMessage = new AssistantMessage
            {
                Id = AssistantMessageId.GetNextInternalId(m_ActiveSessionId),
                Role = Assistant.k_AssistantRole,
                IsComplete = false,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            Conversation.Messages.Add(assistantMessage);
            Conversation.LastMessageTimestamp = assistantMessage.Timestamp;
            m_CurrentStreamingMessageIndex = Conversation.Messages.Count - 1;
            m_CurrentResponseBlockIndex = -1;
            m_CurrentThoughtBlockIndex = -1;
            m_IsReasoningPhaseActive = true;
        }

        void EnsureResponseBlock()
        {
            if (m_CurrentResponseBlockIndex >= 0)
                return;

            if (Conversation == null || m_CurrentStreamingMessageIndex < 0)
                return;

            var message = Conversation.Messages[m_CurrentStreamingMessageIndex];
            message.Blocks.Add(new ResponseBlock { Content = "", IsComplete = false });
            m_CurrentResponseBlockIndex = message.Blocks.Count - 1;
        }

        void EnsureThoughtBlock()
        {
            if (m_CurrentThoughtBlockIndex >= 0)
                return;

            if (Conversation == null || m_CurrentStreamingMessageIndex < 0)
                return;

            var message = Conversation.Messages[m_CurrentStreamingMessageIndex];
            message.Blocks.Add(new ThoughtBlock { Content = "" });
            m_CurrentThoughtBlockIndex = message.Blocks.Count - 1;
        }

        // === Conversation conversion helpers ===

        /// <summary>
        /// Converts a stored ACP conversation to runtime format with proper IDs and normalized content.
        /// </summary>
        AssistantConversation ConvertStoredToRuntimeConversation(AssistantConversation storedConversation, AssistantConversationId conversationId)
        {
            // Set the conversation ID for runtime
            storedConversation.Id = conversationId;

            // Ensure title is set
            if (string.IsNullOrEmpty(storedConversation.Title))
                storedConversation.Title = "Untitled Conversation";

            // Normalize stored blocks for runtime
            foreach (var message in storedConversation.Messages)
            {
                message.Id = AssistantMessageId.GetNextInternalId(conversationId);
                message.IsError = message.Role == "error";

                NormalizeStoredBlocks(message);
            }

            return storedConversation;
        }

        /// <summary>
        /// Normalizes stored blocks to make them suitable for runtime display.
        /// </summary>
        void NormalizeStoredBlocks(AssistantMessage message)
        {
            for (int i = 0; i < message.Blocks.Count; i++)
            {
                var block = message.Blocks[i];

                // PromptBlock content might need text extraction from JSON
                if (block is PromptBlock promptBlock && !string.IsNullOrEmpty(promptBlock.Content))
                {
                    try
                    {
                        // Try to parse as JSON array (ACP format)
                        var promptArray = JsonConvert.DeserializeObject<object[]>(promptBlock.Content);
                        var textContent = new StringBuilder();
                        foreach (var item in promptArray)
                        {
                            if (item is JObject obj && obj["type"]?.ToString() == "text")
                            {
                                textContent.Append(obj["text"]?.ToString() ?? "");
                            }
                        }
                        if (textContent.Length > 0)
                        {
                            promptBlock.Content = textContent.ToString();
                        }
                    }
                    catch
                    {
                        // Content is already plain text, no conversion needed
                    }
                }
            }
        }

        void RestoreToolCallStateFromConversation(AssistantConversation conversation)
        {
            m_ToolCallBlocks.Clear();
            m_PendingPermissions.Clear();

            if (conversation == null)
                return;

            foreach (var message in conversation.Messages)
            {
                foreach (var block in message.Blocks)
                {
                    if (block is not AcpToolCallStorageBlock storageBlock)
                        continue;

                    var toolCallId = GetToolCallId(storageBlock.ToolCallData);
                    if (string.IsNullOrEmpty(toolCallId))
                        continue;

                    m_ToolCallBlocks[toolCallId] = storageBlock;

                    if (!HasPendingPermission(storageBlock.ToolCallData))
                        continue;

                    var unityData = storageBlock.ToolCallData[AcpToolCallStorageKeys.UnityDataKey] as JObject;
                    var pendingRequest = unityData?[AcpToolCallStorageKeys.UnityPendingPermissionKey]?.ToObject<AcpPermissionRequest>();
                    if (pendingRequest == null)
                        continue;

                    var requestId = NormalizeRequestId(pendingRequest.RequestId);
                    m_PendingPermissions[toolCallId] = (requestId, pendingRequest.Options);
                }
            }
        }

        // === Unsupported features (no-op implementations) ===

        public async Task ConversationLoad(AssistantConversationId conversationId, CancellationToken ct = default)
        {
            // Set initializing state immediately to prevent ProcessPrompt from creating a new session
            // while we're loading. This also disables input during the load.
            ProviderStateObserver.SetReadyState(ProviderStateObserver.ProviderReadyState.Initializing);

            // The conversationId IS the agentSessionId (set in RefreshConversationsAsync line 888)
            var agentSessionId = conversationId.ToString();

            // Wait for relay to be ready before attempting to load/resume session
            try
            {
                var relayClient = await RelayService.Instance.GetClientAsync(TimeSpan.FromSeconds(10), ct);
                if (relayClient == null)
                {
                    ConversationErrorOccured?.Invoke(conversationId, new ErrorInfo("Relay not ready", "Could not connect to relay service"));
                    return;
                }
            }
            catch (Exception ex)
            {
                ConversationErrorOccured?.Invoke(conversationId, new ErrorInfo("Relay connection failed", ex.Message));
                return;
            }

            // Load the stored conversation
            var storedConversation = AcpConversationStorage.Load(m_ProviderId, agentSessionId);
            if (storedConversation == null)
            {
                // Conversation not found in storage - this can happen if:
                // 1. The storage was cleared
                // 2. The file was deleted externally
                // 3. The session ID is stale from a previous installation
                // 4. The session had no messages (empty session that wasn't persisted)
                //
                // Check if there's a tracked relay session we can resume.
                // This happens when a session was created but never used (no messages),
                // then Unity disconnected/reconnected. The relay keeps the session alive
                // and we can resume it via resumeSessionId.
                var trackedChannelId = AcpSessionTracker.instance.GetChannelId(agentSessionId);
                if (!string.IsNullOrEmpty(trackedChannelId))
                {
                    InternalLog.Log($"[AcpProvider] Resuming tracked empty session {agentSessionId} (channelId: {trackedChannelId})");

                    // Mark current session for deferred release if any
                    if (m_CurrentSession != null)
                    {
                        AcpSessionCleanupManager.MarkForRelease(m_ActiveSessionId);
                        CurrentSession = null;
                    }

                    // Resume the existing relay session with a new channelId
                    // The relay will reuse the existing session via resumeSessionId
                    ResetStreamingState();
                    m_ActiveSessionId = AcpSessionRegistry.GenerateSessionId(m_ProviderId);
                    CurrentSession = AcpSessionRegistry.Acquire(m_ActiveSessionId, m_ProviderId, agentSessionId);

                    // Remove from tracker since we're now properly connected
                    AcpSessionTracker.instance.Remove(agentSessionId);
                    return;
                }

                // No tracked session - start completely fresh
                Debug.LogWarning($"[AcpProvider] Conversation {agentSessionId} not found in storage or tracker, starting fresh session");
                ResetStreamingState();
                RestoreToolCallStateFromConversation(Conversation);

                PromptStateChanged?.Invoke(conversationId, Assistant.PromptState.NotConnected);
                ConversationLoaded?.Invoke(Conversation);
                ConversationChanged?.Invoke(Conversation);

                return;
            }

            // Mark current session for deferred release if any
            // The cleanup manager will release it when the turn completes (or immediately if safe)
            if (m_CurrentSession != null)
            {
                AcpSessionCleanupManager.MarkForRelease(m_ActiveSessionId);
                CurrentSession = null;  // Unbind events, cleanup manager handles actual release
            }

            // Convert stored conversation to runtime format before passing to session
            var runtimeConversation = ConvertStoredToRuntimeConversation(storedConversation, conversationId);

            // Start a new session with resume, passing the converted conversation
            // Session initialization happens in background - ProviderStateObserver tracks readiness
            m_ActiveSessionId = AcpSessionRegistry.GenerateSessionId(m_ProviderId);
            CurrentSession = AcpSessionRegistry.Acquire(m_ActiveSessionId, m_ProviderId, agentSessionId, runtimeConversation);

            ResetStreamingState();
            RestoreToolCallStateFromConversation(Conversation);

            // Don't set Ready here - let OnSessionInitialized handle it when the resumed session
            // sends session/initialized. Setting Ready prematurely causes prompts to fail.
            PromptStateChanged?.Invoke(conversationId, Assistant.PromptState.NotConnected);

            // Fire the loaded event to populate the UI
            ConversationLoaded?.Invoke(Conversation);

            // Fire the changed event to ensure UI updates properly
            ConversationChanged?.Invoke(Conversation);
        }

        public void ConversationRefresh(AssistantConversationId conversationId)
        {
        }

        public Task RecoverIncompleteMessage(AssistantConversationId conversationId)
            => Task.CompletedTask;

        public Task ConversationFavoriteToggle(AssistantConversationId conversationId, bool isFavorite)
            => Task.CompletedTask;

        public Task ConversationDeleteAsync(AssistantConversationId conversationId, CancellationToken ct = default)
        {
            if (!conversationId.IsValid)
                return Task.CompletedTask;

            // Delete from persistent storage
            AcpConversationStorage.Delete(m_ProviderId, conversationId.Value);

            // Raise the deletion event to update UI
            ConversationDeleted?.Invoke(conversationId);

            return Task.CompletedTask;
        }

        public Task ConversationRename(AssistantConversationId conversationId, string newName, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RefreshConversationsAsync(CancellationToken ct = default)
        {
            // Load all stored ACP conversations from disk across ALL providers
            var metadata = AcpConversationStorage.LoadAllMetadata(m_ProviderId);

            // Convert to AssistantConversationInfo using metadata's ToConversationInfo method
            var conversationInfos = metadata.Select(m => m.ToConversationInfo());

            // Fire the event to populate the UI
            ConversationsRefreshed?.Invoke(conversationInfos);

            return Task.CompletedTask;
        }

        public Task SendFeedback(AssistantMessageId messageId, bool flagMessage, string feedbackText, bool upVote)
            => Task.CompletedTask;

        public Task<FeedbackData?> LoadFeedback(AssistantMessageId messageId, CancellationToken ct = default)
            => Task.FromResult<FeedbackData?>(null);

        public Task<int?> FetchMessageCost(AssistantMessageId messageId, CancellationToken ct = default)
            => Task.FromResult<int?>(null);

        public Task RevertMessage(AssistantMessageId messageId)
            => Task.CompletedTask;

        public Task SendEditRunCommand(AssistantMessageId messageId, string updatedCode)
            => Task.CompletedTask;

        public void SuspendConversationRefresh() { }
        public void ResumeConversationRefresh() { }

        public void DisconnectWorkflow()
        {
            CancelPendingToolCalls(completeStreamingMessage: true);
            m_ToolCallBlocks.Clear();
            CurrentSession = null;
        }

        public Task RefreshProjectOverview(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        // === Storage event handlers ===

        void OnConversationSaved(string sessionId, string providerId)
        {
            // Refresh conversations whenever any ACP conversation is saved
            // This ensures the history panel stays up-to-date across all providers
            TaskUtils.WithExceptionLogging(RefreshConversationsAsync());
        }

        void OnStorageCleared()
        {
            // Refresh the history panel with empty list
            ConversationsRefreshed?.Invoke(Array.Empty<AssistantConversationInfo>());

            // If we have an active conversation, start a new one since it was cleared
            if (Conversation != null && m_CurrentSession != null)
            {
                // Clear session state for the old conversation only
                UnityEditor.SessionState.EraseString("AssistantUserSession_LastActiveConversationId");

                // Fire deletion event for the old conversation to clear it from the UI
                var oldConversationId = Conversation.Id;
                ConversationDeleted?.Invoke(oldConversationId);

                ResetStreamingState();

                // Create a fresh conversation for the existing session
                m_CurrentSession.ClearConversation();

                // Fire ConversationCreated - since old was deleted, this should now set as active
                ConversationCreated?.Invoke(Conversation);

                // Re-save the provider ID so domain reload knows which provider to use
                UnityEditor.SessionState.SetString("AssistantUserSession_LastActiveProviderId", m_ProviderId);
            }
            else
            {
                // No active conversation - just clear the session state
                UnityEditor.SessionState.EraseString("AssistantUserSession_LastActiveConversationId");
                UnityEditor.SessionState.EraseString("AssistantUserSession_LastActiveProviderId");
            }
        }

        void ResetStreamingState()
        {
            m_CurrentStreamingMessageIndex = -1;
            m_CurrentResponseBlockIndex = -1;
            m_CurrentThoughtBlockIndex = -1;
            m_IsReasoningPhaseActive = false;
            m_CancelRequested = false;
            m_PendingPermissions.Clear();
            m_ToolCallBlocks.Clear();
        }

        // === Cleanup ===

        public void Dispose()
        {
            if (m_Disposed)
                return;

            m_Disposed = true;

            // Unbind and clear session via property setter
            CurrentSession = null;

            // Unsubscribe from storage events
            AcpConversationStorage.OnSessionSaved -= OnConversationSaved;
            AcpConversationStorage.OnStorageCleared -= OnStorageCleared;

            // Clear any pending cleanup releases (we're shutting down, release immediately)
            AcpSessionCleanupManager.Clear();

            if (m_ActiveSessionId.IsValid)
            {
                TaskUtils.WithExceptionLogging(AcpSessionRegistry.ReleaseAsync(m_ActiveSessionId));
            }

            // Reset provider state to Unity/Ready to clear any lingering banners
            // (e.g., if window was closed during initialization)
            ProviderStateObserver.Reset();
        }

        /// <summary>
        /// Injects default agents.md content as a VirtualAttachment if the setting is enabled
        /// and no provider-specific or generic agents file exists in the working directory.
        /// For Claude provider, checks for claude.md first, then falls back to agents.md.
        /// </summary>
        static void TryInjectDefaultAgentsMd(AssistantPrompt prompt, string providerId)
        {
            // Check if feature is enabled
            if (!GatewayProjectPreferences.IncludeDefaultAgentsMd)
                return;

            // Get working directory and check if agents.md already exists
            var workingDir = GatewayProjectPreferences.GetWorkingDir(providerId);
            if (string.IsNullOrEmpty(workingDir))
                return;

            try
            {
                // For Claude provider, check for claude.md first
                if (providerId == Unity.Relay.Editor.Acp.AcpConstants.ProviderId_ClaudeCode)
                {
                    var claudeMdPath = System.IO.Path.Combine(workingDir, "claude.md");
                    if (System.IO.File.Exists(claudeMdPath))
                        return; // User has custom claude.md, don't inject default
                }

                // Check for generic agents.md
                var agentsMdPath = System.IO.Path.Combine(workingDir, "agents.md");
                if (System.IO.File.Exists(agentsMdPath))
                    return; // User has custom agents.md, don't inject default
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AcpProvider] Failed to check for agents files: {ex.Message}");
                return;
            }

            // Read default agents.md content
            var defaultContent = TryReadDefaultAgentsMd();
            if (string.IsNullOrEmpty(defaultContent))
                return;

            // Format with header to make it clear this is auto-attached
            var formattedContent = $"# Default Agent Guidelines\n\n{defaultContent}\n\n---\n";

            // Create VirtualAttachment and add to prompt
            var attachment = new VirtualAttachment(
                payload: formattedContent,
                type: "Document",  
                displayName: "default-agents.md",
                metadata: null
            );

            prompt.VirtualAttachments.Insert(0, attachment); // Insert at start so it comes first
        }

        static string TryReadDefaultAgentsMd()
        {
            // Use AssetDatabase to find the DefaultAgents.md file in the package
            var guids = AssetDatabase.FindAssets("DefaultAgents t:TextAsset", new[] { "Packages/com.unity.ai.assistant/Editor/Assistant/Acp" });

            if (guids.Length == 0)
            {
                Debug.LogWarning("[AcpProvider] Could not find DefaultAgents.md in package");
                return null;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);

            if (textAsset == null)
            {
                Debug.LogWarning($"[AcpProvider] Failed to load DefaultAgents.md from {assetPath}");
                return null;
            }

            return textAsset.text;
        }
    }
}
