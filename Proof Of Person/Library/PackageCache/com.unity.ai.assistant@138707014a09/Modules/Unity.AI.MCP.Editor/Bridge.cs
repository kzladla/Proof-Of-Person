using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.MCP.Editor.Connection;
using Unity.AI.MCP.Editor.Helpers;
using Unity.AI.MCP.Editor.Models;
using Unity.AI.MCP.Editor.Security;
using Unity.AI.MCP.Editor.Settings;
using Unity.AI.MCP.Editor.ToolRegistry;
using Unity.AI.MCP.Editor.UI;
using Unity.AI.Assistant.Editor.Acp;
using Unity.Relay;
using Unity.Relay.Editor;
using UnityEditor;
using UnityEngine;
using ClientInfo = Unity.AI.MCP.Editor.Models.ClientInfo;

namespace Unity.AI.MCP.Editor
{
    /// <summary>
    /// Unity MCP Bridge - Uses named pipes (Windows) / Unix sockets (Mac/Linux)
    /// Cleanly separates connection management from messaging
    /// </summary>
    class Bridge : IDisposable
    {
        // Connection layer
        IConnectionListener listener;
        bool isRunning;
        readonly object startStopLock = new();
        readonly object clientsLock = new();
        readonly Dictionary<string, IConnectionTransport> identityToTransportMap = new(); // identity key -> transport
        readonly Dictionary<IConnectionTransport, string> transportToIdentityMap = new(); // transport -> identity key (reverse lookup)
        CancellationTokenSource cts;
        Task listenerTask;

        // Command processing
        int processingCommands;
        readonly object lockObj = new();
        readonly Dictionary<string, (string commandJson, TaskCompletionSource<string> tcs, IConnectionTransport client)> commandQueue = new();

        // Lifecycle
        bool initScheduled;
        bool ensureUpdateHooked;
        bool isStarting;
        double nextStartAt;
        double nextHeartbeatAt;
        int heartbeatSeq;

        // Tools
        string s_CurrentToolsHash;
        McpToolInfo[] s_ToolsSnapshot;

        // Connection info
        string currentConnectionPath;

        // Security validation
        ValidationConfig validationConfig;

        // Approval dialog management
        bool isApprovalDialogShowing;
        readonly object approvalDialogLock = new();

        // Pending approval tracking - one per identity
        static readonly Dictionary<string, TaskCompletionSource<bool>> pendingApprovalsByIdentity = new();
        static readonly object pendingApprovalsLock = new();

        // ACP session tokens for auto-approval - maps transport to token
        readonly Dictionary<IConnectionTransport, string> pendingAcpTokens = new();
        readonly object acpTokenLock = new();

        // Command deduplication - request ID tracking
        readonly Dictionary<string, Task<string>> inFlightCommands = new();
        readonly Dictionary<string, (string result, DateTime expiry)> completedCommands = new();
        static readonly TimeSpan ResultCacheDuration = TimeSpan.FromMinutes(5);
        double nextCacheCleanupAt;

        /// <summary>
        /// Event fired when a client connects or disconnects.
        /// This event is always invoked on the main thread via EditorApplication.delayCall.
        /// </summary>
        public static event Action OnClientConnectionChanged;

        /// <summary>
        /// Diagnostic events for testing and observability.
        /// Most events fire immediately (synchronously) from background threads.
        /// OnDialogShown fires on main thread via EditorApplication.delayCall.
        /// Event handlers should be thread-safe or marshal to main thread if needed.
        /// </summary>
        public static event Action<string> OnConnectionAttempt;  // Fired immediately when AcceptClientAsync returns (connectionId)
        public static event Action<string, ValidationStatus> OnValidationComplete;  // Fired immediately after validation (connectionId, status)
        public static event Action<string, bool> OnDialogScheduled;  // Fired immediately when dialog decision made (connectionId, willShow)
        public static event Action<string> OnDialogShown;  // Fired on main thread after dialog opens (connectionId)


        /// <summary>
        /// The currently shown approval dialog, if any.
        /// Used by tests to access the actual dialog instance.
        /// </summary>
        public static ConnectionApprovalDialog CurrentApprovalDialog { get; internal set; }

        public bool IsRunning => isRunning;
        public string CurrentConnectionPath => currentConnectionPath;

        /// <summary>
        /// Get the set of currently active identity keys.
        /// Used by ConnectionRegistry to filter active connections.
        /// </summary>
        public IEnumerable<string> GetActiveIdentityKeys()
        {
            lock (clientsLock)
            {
                return new List<string>(identityToTransportMap.Keys);
            }
        }

        public string GetClientInfo()
        {
            return ConnectionRegistry.instance.GetClientInfo(GetActiveIdentityKeys());
        }

        /// <summary>
        /// Disconnect any active connections matching the given identity.
        /// Used when revoking a previously-approved connection from settings.
        /// Server will see connection loss and attempt to reconnect, at which point
        /// it will receive approval_denied during the handshake if status is Rejected.
        /// </summary>
        public void DisconnectConnectionByIdentity(ConnectionIdentity identity)
        {
            if (identity == null || string.IsNullOrEmpty(identity.CombinedIdentityKey))
                return;

            lock (clientsLock)
            {
                if (identityToTransportMap.TryGetValue(identity.CombinedIdentityKey, out var transport))
                {
                    McpLog.LogDelayed($"Disconnecting connection with identity: {identity.CombinedIdentityKey}");
                    try
                    {
                        transport.Dispose(); // Close connection - server will try to reconnect and get denied
                    }
                    catch (Exception ex)
                    {
                        McpLog.LogDelayed($"Error disconnecting transport: {ex.Message}", LogType.Warning);
                    }
                }
                else
                {
                    McpLog.LogDelayed($"No active connection found for identity: {identity.CombinedIdentityKey}");
                }
            }
        }

        /// <summary>
        /// Disconnect all active connections.
        /// Used when removing all connections from settings.
        /// </summary>
        public void DisconnectAll()
        {
            IConnectionTransport[] toClose;
            lock (clientsLock)
            {
                toClose = identityToTransportMap.Values.ToArray();
            }

            McpLog.LogDelayed($"Disconnecting all connections ({toClose.Length} active)");
            foreach (var transport in toClose)
            {
                try
                {
                    transport.Dispose();
                }
                catch (Exception ex)
                {
                    McpLog.LogDelayed($"Error disconnecting transport: {ex.Message}", LogType.Warning);
                }
            }
        }

        /// <summary>
        /// Complete a pending approval for a connection with the given identity.
        /// Called from settings UI when user accepts/denies a pending connection.
        /// </summary>
        /// <param name="identityKey">The combined identity key (server+client)</param>
        /// <param name="approved">True to approve, false to deny</param>
        public static void CompletePendingApproval(string identityKey, bool approved)
        {
            if (string.IsNullOrEmpty(identityKey))
                return;

            lock (pendingApprovalsLock)
            {
                if (pendingApprovalsByIdentity.TryGetValue(identityKey, out var tcs))
                {
                    McpLog.LogDelayed($"Completing pending approval for identity {identityKey}: {(approved ? "approved" : "denied")}");
                    tcs.TrySetResult(approved);
                    pendingApprovalsByIdentity.Remove(identityKey);
                }
                else
                {
                    McpLog.LogDelayed($"No pending approval found for identity {identityKey}");
                }
            }
        }

        public Bridge(bool autoScheduleStart = true)
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += _ => ScheduleInitRetry();
            McpToolRegistry.ToolsChanged += OnToolsChanged;

            // Subscribe to MCP session events from Relay for auto-approval
            RelayService.Instance.OnMcpSessionRegister += OnMcpSessionRegister;
            RelayService.Instance.OnMcpSessionUnregister += OnMcpSessionUnregister;

            // Initialize MCP tool approval handler (for Codex which doesn't use native ACP permissions)
            McpToolApprovalHandler.Initialize();

            // Catch any registrations that happened before we subscribed (race condition fix)
            // McpSessionBuffer subscribes via [InitializeOnLoadMethod] and buffers all registrations
            foreach (var registration in McpSessionBuffer.GetAll())
            {
                OnMcpSessionRegister(registration);
            }

            // Load validation configuration
            validationConfig = ValidatedConfigs.Unity;

            if (autoScheduleStart)
            {
                // Defer start until the editor is idle and not compiling
                ScheduleInitRetry();

                // Add a safety net update hook in case delayCall is missed during reload churn
                if (!ensureUpdateHooked)
                {
                    ensureUpdateHooked = true;
                    EditorApplication.update += EnsureStartedOnEditorIdle;
                }
            }
        }

        public void Start()
        {
            lock (startStopLock)
            {
                if (isRunning && listener != null)
                {
                    McpLog.Log($"UnityMCPBridge already running on {currentConnectionPath}");
                    return;
                }

                Stop();

                // Reload validation configuration (settings may have changed)
                validationConfig = ValidatedConfigs.Unity;

                try
                {
                    // Create platform-specific listener
                    listener = ConnectionFactory.CreateListener();

                    // Get connection path for this project
                    currentConnectionPath = ServerDiscovery.GetConnectionPath();

                    LogBreadcrumb("Start");

                    // Start listening
                    listener.Start(currentConnectionPath);

                    isRunning = true;
                    string connectionType = ConnectionFactory.GetConnectionTypeName();
                    string platform = Application.platform.ToString();
                    McpLog.Log($"MCP Bridge V2 started using {connectionType} at {currentConnectionPath} (OS={platform})");

                    // Save discovery files
                    ServerDiscovery.SaveConnectionInfo(currentConnectionPath);
                    heartbeatSeq++;
                    ServerDiscovery.SaveStatusFile(currentConnectionPath, "ready");
                    nextHeartbeatAt = EditorApplication.timeSinceStartup + 0.5f;

                    // Start background listener with cooperative cancellation
                    cts = new CancellationTokenSource();
                    listenerTask = Task.Run(() => ListenerLoopAsync(cts.Token));
                    EditorApplication.update += ProcessCommands;

                    // Ensure lifecycle events are (re)subscribed
                    try { AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload; } catch { }
                    try { AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload; } catch { }
                    try { AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload; } catch { }
                    try { AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload; } catch { }
                    try { EditorApplication.quitting -= Stop; } catch { }
                    try { EditorApplication.quitting += Stop; } catch { }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to start MCP Bridge: {ex.Message}");
                    isRunning = false;
                }
            }
        }

        public void Stop()
        {
            Task toWait = null;
            lock (startStopLock)
            {
                if (!isRunning)
                    return;

                try
                {
                    isRunning = false;

                    // Delete discovery files
                    ServerDiscovery.DeleteDiscoveryFiles();

                    // Clear all gateway connections (they're ephemeral and won't survive anyway)
                    EditorApplication.delayCall += () =>
                    {
                        ConnectionRegistry.instance.ClearAllGatewayConnections();
                    };

                    // Quiesce background listener
                    var cancel = cts;
                    cts = null;
                    try { cancel?.Cancel(); } catch { }

                    try { listener?.Stop(); } catch { }
                    try { listener?.Dispose(); } catch { }
                    listener = null;

                    toWait = listenerTask;
                    listenerTask = null;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error stopping UnityMCPBridge: {ex.Message}");
                }
            }

            // Close all active clients
            IConnectionTransport[] toClose;
            lock (clientsLock)
            {
                toClose = identityToTransportMap.Values.ToArray();
                identityToTransportMap.Clear();
                transportToIdentityMap.Clear();
            }
            foreach (var c in toClose)
            {
                try { c.Close(); c.Dispose(); } catch { }
            }

            // Unblock any pending command waiters since ProcessCommands won't run after Stop()
            lock (lockObj)
            {
                foreach (var kvp in commandQueue.Values)
                {
                    kvp.tcs.TrySetResult(JsonConvert.SerializeObject(new
                    {
                        status = "error",
                        error = "Bridge stopped"
                    }));
                }
                commandQueue.Clear();
            }

            if (toWait != null)
            {
                // Wait for listener task to complete (increased timeout for slower CI machines)
                // The listener task should complete quickly after cancellation, but on Linux
                // the accept() call may take a moment to return after the socket is closed
                try { toWait.Wait(1000); } catch { }
            }

            try { EditorApplication.update -= ProcessCommands; } catch { }
            McpLog.Log("UnityMCPBridge stopped.");
        }

        public void Dispose()
        {
            try { Stop(); } catch { }
            try { EditorApplication.update -= EnsureStartedOnEditorIdle; } catch { }
            try { EditorApplication.update -= ProcessCommands; } catch { }
            try { EditorApplication.quitting -= Stop; } catch { }
            try { AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload; } catch { }
            try { AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload; } catch { }
            try { McpToolRegistry.ToolsChanged -= OnToolsChanged; } catch { }
        }

        void ScheduleInitRetry()
        {
            if (initScheduled) return;
            initScheduled = true;
            nextStartAt = EditorApplication.timeSinceStartup + 0.20f;
            if (!ensureUpdateHooked)
            {
                ensureUpdateHooked = true;
                EditorApplication.update += EnsureStartedOnEditorIdle;
            }
            EditorApplication.delayCall += InitializeAfterCompilation;
        }

        // Safety net: ensure the bridge starts shortly after domain reload when editor is idle
        void EnsureStartedOnEditorIdle()
        {
            // Do nothing while compiling
            if (IsCompiling()) return;

            // If already running, remove the hook
            if (isRunning)
            {
                EditorApplication.update -= EnsureStartedOnEditorIdle;
                ensureUpdateHooked = false;
                return;
            }
            // Debounced start: wait until the scheduled time
            if (nextStartAt > 0 && EditorApplication.timeSinceStartup < nextStartAt) return;
            if (isStarting) return;

            isStarting = true;
            // Attempt start; if it succeeds, remove the hook to avoid overhead
            try { Start(); }
            finally { isStarting = false; }

            if (isRunning)
            {
                EditorApplication.update -= EnsureStartedOnEditorIdle;
                ensureUpdateHooked = false;
            }
        }

        /// <summary>
        /// Initialize the MCP bridge after Unity is fully loaded and compilation is complete.
        /// This prevents repeated restarts during script compilation that cause port hopping.
        /// </summary>
        void InitializeAfterCompilation()
        {
            initScheduled = false;

            // Play-mode friendly: allow starting in play mode; only defer while compiling
            if (IsCompiling())
            {
                ScheduleInitRetry();
                return;
            }

            if (!isRunning)
            {
                Start();
                // If a race prevented start, retry later
                if (!isRunning) ScheduleInitRetry();
            }
        }

        async Task ListenerLoopAsync(CancellationToken token)
        {
            while (isRunning && !token.IsCancellationRequested)
            {
                try
                {
                    IConnectionTransport clientTransport = await listener.AcceptClientAsync(token);

                    // Fire diagnostic event immediately (not via delayCall)
                    // Event handlers can marshal to main thread if needed, but event fires synchronously
                    var connId = clientTransport.ConnectionId;
                    OnConnectionAttempt?.Invoke(connId);

                    // Fire and forget each client connection
                    _ = Task.Run(() => HandleClientAsync(clientTransport, token), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (isRunning && !token.IsCancellationRequested)
                    {
                        McpLog.LogDelayed($"Listener error: {ex.Message}", LogType.Error);
                    }
                }
            }
        }

        async Task HandleClientAsync(IConnectionTransport transport, CancellationToken token)
        {
            using (transport)
            {
                // Set up disconnect handler
                transport.OnDisconnected += () =>
                {
                    lock (clientsLock)
                    {
                        if (transportToIdentityMap.TryGetValue(transport, out var identityKey))
                        {
                            var record = ConnectionRegistry.instance.GetConnectionByIdentity(identityKey);
                            var clientInfo = record?.Info?.ClientInfo;
                            if (clientInfo != null)
                            {
                                string displayName = string.IsNullOrEmpty(clientInfo.Title) ? clientInfo.Name : clientInfo.Title;
                                McpLog.LogDelayed($"Client disconnected: {displayName} v{clientInfo.Version}");
                            }
                        }
                    }
                };

                try
                {
                    McpLog.LogDelayed($"Client connected: {transport.ConnectionId}");

                    // Track whether we can skip validation and go straight to handshake
                    ValidationDecision decision = null;
                    bool skipValidation = false;

                    // Read ACP token FIRST (before expensive validation)
                    // This allows gateway connections to skip validation entirely
                    string acpToken = await TryReadAcpTokenAsync(transport, token);
                    bool hasAcpToken = !string.IsNullOrEmpty(acpToken);

                    if (hasAcpToken)
                    {
                        McpLog.LogDelayed($"[ACP Token] Received approval token from client");

                        var tokenResult = McpSessionTokenRegistry.ValidateAndConsume(acpToken);
                        if (tokenResult.IsValid)
                        {
                            var gatewayPolicy = MCPSettingsManager.Settings.connectionPolicies.gateway;

                            if (gatewayPolicy.allowed && !gatewayPolicy.requiresApproval)
                            {
                                // FAST PATH: Valid gateway + auto-approve policy
                                // Skip expensive validation (SHA256, signatures) since the ACP token authenticates this connection
                                McpLog.LogDelayed($"[Gateway Fast Path] Skipping validation for session: {tokenResult.SessionId}, provider: {tokenResult.Provider ?? "unknown"}");

                                // Create minimal connection info without crypto operations
                                // Gateway connections display the provider name, not process info
                                var minimalInfo = new ConnectionInfo
                                {
                                    ConnectionId = transport.ConnectionId,
                                    Timestamp = DateTime.UtcNow,
                                    Server = new ProcessInfo
                                    {
                                        ProcessId = transport.GetClientProcessId() ?? 0,
                                        ProcessName = "gateway-connection"
                                    }
                                };

                                OnValidationComplete?.Invoke(transport.ConnectionId, ValidationStatus.Accepted);

                                var acceptedDecision = new ValidationDecision
                                {
                                    Status = ValidationStatus.Accepted,
                                    Reason = "Auto-approved via AI Gateway (fast path)",
                                    Connection = minimalInfo
                                };

                                var sessionId = tokenResult.SessionId;
                                var provider = tokenResult.Provider;
                                EditorApplication.delayCall += () =>
                                {
                                    ConnectionRegistry.instance.RecordGatewayConnection(acceptedDecision, sessionId, provider);
                                };

                                skipValidation = true;
                            }
                            else if (!gatewayPolicy.allowed)
                            {
                                McpLog.LogDelayed($"Connection rejected: Gateway connections not allowed by policy", LogType.Warning);
                                try
                                {
                                    string denialMsg = MessageProtocol.CreateApprovalDeniedMessage(
                                        "Gateway connections are not allowed by current policy");
                                    byte[] denialBytes = Encoding.UTF8.GetBytes(denialMsg);
                                    await transport.WriteAsync(denialBytes, token);
                                }
                                catch (Exception ex)
                                {
                                    McpLog.LogDelayed($"Failed to send denial message: {ex.Message}", LogType.Warning);
                                }
                                return;
                            }
                            else
                            {
                                // Gateway requires approval - store token for approval flow
                                lock (acpTokenLock)
                                {
                                    pendingAcpTokens[transport] = acpToken;
                                }
                            }
                        }
                        else
                        {
                            // Invalid token - store for later, fall through to full validation
                            lock (acpTokenLock)
                            {
                                pendingAcpTokens[transport] = acpToken;
                            }
                        }
                    }

                    // Validate connection (before handshake) - skip if gateway fast path was taken
                    if (!skipValidation)
                    {
                        // Run expensive validation (SHA256, signatures) on background thread but AWAIT completion
                        // This ensures we don't send handshake until truly ready to accept commands
                        if (validationConfig != null && validationConfig.Enabled && validationConfig.Mode != ValidationMode.Disabled)
                        {
                            try
                            {
                                var validationStart = DateTime.Now;

                                // Run validation on background thread (doesn't block async handler)
                                // First connection: ~250-900ms (expensive crypto)
                                // Subsequent connections: <10ms (cache hit)
                                decision = await Task.Run(() => ConnectionValidator.ValidateConnection(transport, validationConfig));
                                var validationMs = (DateTime.Now - validationStart).TotalMilliseconds;
                                McpLog.LogDelayed($"[TIMING] Validation took {validationMs:F0}ms");

                                // Fire diagnostic event immediately (synchronously)
                                OnValidationComplete?.Invoke(transport.ConnectionId, decision.Status);

                                // Log comprehensive connection info
                                LogConnectionDecision(decision);

                                // Handle rejection - don't send handshake or enter message loop
                                if (!decision.IsAccepted)
                                {
                                    McpLog.LogDelayed($"Connection rejected: {decision.Reason}", LogType.Warning);
                                    return;
                                }

                                // Log warning if validation failed but connection allowed (LogOnly mode)
                                if (decision.Status == ValidationStatus.Warning)
                                {
                                    McpLog.LogDelayed($"Connection allowed with warning: {decision.Reason}", LogType.Warning);
                                }
                            }
                            catch (Exception ex)
                            {
                                McpLog.LogDelayed($"Validation exception: {ex.Message}\n{ex.StackTrace}", LogType.Error);

                                // In LogOnly mode, allow connection despite error
                                if (validationConfig.Mode != ValidationMode.Strict)
                                {
                                    McpLog.LogDelayed("Allowing connection despite validation error (LogOnly mode)", LogType.Warning);
                                }
                                else
                                {
                                    return; // Reject in Strict mode - don't send handshake
                                }
                            }
                        }
                        else
                        {
                            // Validation is disabled - log warning as this should only happen in development/testing
                            McpLog.LogDelayed(
                                "Connection validation is DISABLED - connections will not appear in MCP Settings UI. " +
                                "This should only be used for automated tests. " +
                                "Re-enable validation by re-running any test.",
                                LogType.Warning);
                        }

                        // Determine connection origin and get applicable policy
                        // Note: ACP token was already read at start of connection handling
                        if (decision != null)
                        {
                            string storedToken = null;
                            lock (acpTokenLock)
                            {
                                pendingAcpTokens.TryGetValue(transport, out storedToken);
                            }

                            var tokenResult = McpSessionTokenRegistry.ValidateAndConsume(storedToken);
                            bool isGateway = tokenResult.IsValid;

                            var policy = isGateway
                                ? MCPSettingsManager.Settings.connectionPolicies.gateway
                                : MCPSettingsManager.Settings.connectionPolicies.direct;

                            // Check if origin is allowed
                            if (!policy.allowed)
                            {
                                string origin = isGateway ? "Gateway" : "Direct MCP";
                                McpLog.LogDelayed($"Connection rejected: {origin} connections not allowed by policy", LogType.Warning);

                                try
                                {
                                    string denialMsg = MessageProtocol.CreateApprovalDeniedMessage(
                                        $"{origin} connections are not allowed by current policy");
                                    byte[] denialBytes = Encoding.UTF8.GetBytes(denialMsg);
                                    await transport.WriteAsync(denialBytes, token);
                                }
                                catch (Exception ex)
                                {
                                    McpLog.LogDelayed($"Failed to send denial message: {ex.Message}", LogType.Warning);
                                }

                                return; // Close connection
                            }

                            // Gateway auto-approve case is handled by the fast path at start of connection handling.
                            // If we reach here with a gateway connection, it requires approval, so clean up the token.
                            if (isGateway)
                            {
                                lock (acpTokenLock)
                                {
                                    pendingAcpTokens.Remove(transport);
                                }
                            }

                            // If approval is required, continue with approval flow
                            if (policy.requiresApproval)
                            {
                                // Check if there's an existing connection record with this identity
                                var existingRecord = ConnectionRegistry.instance.FindMatchingConnection(decision.Connection);

                                // If connection was previously accepted, allow through without dialog
                                if (existingRecord != null && (existingRecord.Status == ValidationStatus.Accepted || existingRecord.Status == ValidationStatus.Warning))
                                {
                                    McpLog.LogDelayed($"Connection auto-approved: previously accepted by user");

                                    // Don't show dialog - proceed directly to handshake
                                    // Update the existing record with new connection info
                                    EditorApplication.delayCall += () =>
                                    {
                                        ConnectionRegistry.instance.RecordConnection(decision);
                                    };

                                    // Skip approval flow - continue to handshake below
                                }

                                // If connection was previously rejected, send denial and close immediately
                                else if (existingRecord != null && existingRecord.Status == ValidationStatus.Rejected)
                                {
                                    McpLog.LogDelayed($"Connection rejected: previously denied by user", LogType.Warning);

                                    // Send approval_denied message to server so it stops retrying
                                    try
                                    {
                                        string denialMsg = MessageProtocol.CreateApprovalDeniedMessage("Connection previously denied by user");
                                        byte[] denialBytes = Encoding.UTF8.GetBytes(denialMsg);
                                        await transport.WriteAsync(denialBytes, token);
                                        McpLog.LogDelayed("Sent approval_denied message to client");
                                    }
                                    catch (Exception ex)
                                    {
                                        McpLog.LogDelayed($"Failed to send approval_denied: {ex.Message}", LogType.Warning);
                                    }

                                    return; // Close connection without handshake
                                }
                                else
                                {
                                    // Status is Pending or dialog needs to be shown

                                    // Get identity key for tracking pending approvals
                                    var identity = ConnectionIdentity.FromConnectionInfo(decision.Connection);
                                    string identityKey = identity?.CombinedIdentityKey;

                                    if (string.IsNullOrEmpty(identityKey))
                                    {
                                        McpLog.LogDelayed("Connection rejected: unable to determine identity key", LogType.Warning);
                                        return;
                                    }

                                    // Check if there's already a pending approval for this identity
                                    TaskCompletionSource<bool> approvalTcs;
                                    bool isReconnect = false;
                                    lock (pendingApprovalsLock)
                                    {
                                        if (pendingApprovalsByIdentity.TryGetValue(identityKey, out approvalTcs))
                                        {
                                            isReconnect = true;
                                            McpLog.LogDelayed($"Reconnected during pending approval - reusing existing approval");
                                        }
                                        else
                                        {
                                            // Create new approval TCS for first connection
                                            approvalTcs = new TaskCompletionSource<bool>();
                                            pendingApprovalsByIdentity[identityKey] = approvalTcs;
                                        }
                                    }

                                    // Record connection as Pending before showing dialog
                                    var pendingDecision = new ValidationDecision
                                    {
                                        Status = ValidationStatus.Pending,
                                        Reason = "Awaiting user approval",
                                        Connection = decision.Connection
                                    };

                                    // Record pending connection (must run on main thread - ScriptableSingleton access)
                                    var decisionToRecord = pendingDecision;
                                    EditorApplication.delayCall += () =>
                                    {
                                        ConnectionRegistry.instance.RecordConnection(decisionToRecord);
                                    };

                                    // For new connections (not reconnects), check if approval dialog is already showing
                                    if (!isReconnect)
                                    {
                                        lock (approvalDialogLock)
                                        {
                                            if (isApprovalDialogShowing)
                                            {
                                                McpLog.LogDelayed("Connection rejected: approval dialog already showing for another connection", LogType.Warning);

                                                // Update to rejected status
                                                ConnectionRegistry.instance.UpdateConnectionStatus(
                                                    decision.Connection.ConnectionId,
                                                    ValidationStatus.Rejected,
                                                    "Connection rejected: approval dialog already showing for another connection"
                                                );
                                                return; // Deny this connection - another one is waiting for approval
                                            }

                                            isApprovalDialogShowing = true;
                                        }
                                    }

                                    var heartbeatCts = new CancellationTokenSource();

                                    // Set up disconnect handler for immediate notification
                                    Action disconnectHandler = () =>
                                    {
                                        // Don't auto-deny - just stop heartbeats and cleanup
                                        // Connection stays Pending, user can decide in settings
                                        heartbeatCts.Cancel();

                                        // Remove from pending dictionary
                                        lock (pendingApprovalsLock)
                                        {
                                            pendingApprovalsByIdentity.Remove(identityKey);
                                        }
                                    };

                                    try
                                    {
                                        // Register disconnect handler for immediate notification
                                        transport.OnDisconnected += disconnectHandler;

                                        // Send first approval_pending message IMMEDIATELY (before dialog shows)
                                        // This ensures client knows we're processing and doesn't timeout
                                        try
                                        {
                                            // Check if transport is still connected before trying to write
                                            if (!transport.IsConnected)
                                            {
                                                McpLog.LogDelayed("Connection closed before approval flow started");
                                                transport.OnDisconnected -= disconnectHandler;
                                                lock (approvalDialogLock) { isApprovalDialogShowing = false; }

                                                lock (pendingApprovalsLock) { pendingApprovalsByIdentity.Remove(identityKey); }

                                                return;
                                            }

                                            string firstHeartbeat = MessageProtocol.CreateApprovalPendingMessage();
                                            byte[] firstHeartbeatBytes = Encoding.UTF8.GetBytes(firstHeartbeat);
                                            await transport.WriteAsync(firstHeartbeatBytes, token);
                                            McpLog.LogDelayed("Sent initial approval_pending message to client");
                                        }
                                        catch (ObjectDisposedException)
                                        {
                                            // Transport was disposed during write - this is a benign race condition in tests
                                            McpLog.LogDelayed("Connection closed during approval_pending send (transport disposed)");
                                            transport.OnDisconnected -= disconnectHandler;
                                            lock (approvalDialogLock) { isApprovalDialogShowing = false; }

                                            lock (pendingApprovalsLock) { pendingApprovalsByIdentity.Remove(identityKey); }

                                            return;
                                        }
                                        catch (Exception ex)
                                        {
                                            McpLog.LogDelayed($"Failed to send initial approval_pending: {ex.Message}", LogType.Warning);
                                            transport.OnDisconnected -= disconnectHandler;
                                            lock (approvalDialogLock) { isApprovalDialogShowing = false; }

                                            lock (pendingApprovalsLock) { pendingApprovalsByIdentity.Remove(identityKey); }

                                            return;
                                        }

                                        // Show approval dialog on main thread (HandleClientAsync is on background thread)
                                        // Only show if dialog hasn't been shown before for this identity
                                        bool dialogAlreadyShown = ConnectionRegistry.instance.WasDialogShown(decision.Connection);

                                        // Fire diagnostic event immediately (synchronously)
                                        OnDialogScheduled?.Invoke(transport.ConnectionId, !dialogAlreadyShown);

                                        if (!dialogAlreadyShown)
                                        {
                                            var dialogScheduledAt = DateTime.Now;
                                            var eventConnId = transport.ConnectionId;
                                            EditorApplication.delayCall += () =>
                                            {
                                                try
                                                {
                                                    var delayMs = (DateTime.Now - dialogScheduledAt).TotalMilliseconds;

                                                    if (!approvalTcs.Task.IsCompleted)
                                                    {
                                                        var showStart = DateTime.Now;

                                                        // ShowApprovalDialog returns the dialog it created - store it directly
                                                        CurrentApprovalDialog = ConnectionApprovalDialog.ShowApprovalDialog(decision, approvalTcs);
                                                        var showMs = (DateTime.Now - showStart).TotalMilliseconds;

                                                        // Mark dialog as shown for this connection identity
                                                        ConnectionRegistry.instance.MarkDialogShown(decision.Connection);

                                                        // Fire diagnostic event after dialog opens
                                                        OnDialogShown?.Invoke(eventConnId);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    McpLog.LogDelayed($"Error showing approval dialog: {ex.Message}", LogType.Error);
                                                    approvalTcs.TrySetResult(false); // Deny on error
                                                }
                                            };
                                        }
                                        else
                                        {
                                            McpLog.LogDelayed("Dialog already shown for this identity - awaiting approval via settings");
                                        }

                                        // Start heartbeat loop to keep client from timing out
                                        // Send heartbeats every 2.5 seconds after the initial one
                                        _ = Task.Run(async () =>
                                        {
                                            try
                                            {
                                                while (!approvalTcs.Task.IsCompleted && !heartbeatCts.Token.IsCancellationRequested)
                                                {
                                                    await Task.Delay(2500, heartbeatCts.Token); // Send heartbeat every 2.5 seconds

                                                    if (!approvalTcs.Task.IsCompleted && transport.IsConnected)
                                                    {
                                                        try
                                                        {
                                                            string heartbeatMsg = MessageProtocol.CreateApprovalPendingMessage();
                                                            byte[] heartbeatBytes = Encoding.UTF8.GetBytes(heartbeatMsg);
                                                            await transport.WriteAsync(heartbeatBytes, heartbeatCts.Token);
                                                            McpLog.LogDelayed("Sent approval_pending heartbeat to client");
                                                        }
                                                        catch (Exception writeEx)
                                                        {
                                                            // Write failed - connection dropped
                                                            // Don't cancel TCS - keep it alive so user can still decide
                                                            McpLog.LogDelayed($"Heartbeat write failed: {writeEx.Message} - connection dropped, dialog stays open");
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                            catch (OperationCanceledException)
                                            {
                                                // Expected when approval completes or connection closes
                                            }
                                            catch (Exception ex)
                                            {
                                                McpLog.LogDelayed($"Heartbeat error: {ex.Message} - connection remains pending");

                                                // Don't auto-deny - let the connection stay pending
                                            }
                                        }, heartbeatCts.Token);

                                        // Await user approval (no timeout - wait indefinitely)
                                        // TCS completes only when user clicks Accept/Deny in dialog
                                        bool approved = await approvalTcs.Task;

                                        // Clean up: stop heartbeat, unregister disconnect handler, clear dialog flag, remove from pending
                                        heartbeatCts.Cancel();
                                        transport.OnDisconnected -= disconnectHandler;
                                        lock (approvalDialogLock) { isApprovalDialogShowing = false; }

                                        lock (pendingApprovalsLock)
                                        {
                                            pendingApprovalsByIdentity.Remove(identityKey);
                                        }

                                        if (!approved)
                                        {
                                            McpLog.LogDelayed("Connection denied by user", LogType.Warning);

                                            // Update connection status to rejected (use identity to find the right record)
                                            var record = ConnectionRegistry.instance.FindMatchingConnection(identity);
                                            if (record != null)
                                            {
                                                ConnectionRegistry.instance.UpdateConnectionStatus(
                                                    record.Info.ConnectionId,
                                                    ValidationStatus.Rejected,
                                                    "Denied by user"
                                                );
                                            }

                                            // Send approval_denied message if still connected
                                            if (transport.IsConnected)
                                            {
                                                try
                                                {
                                                    string denialMsg = MessageProtocol.CreateApprovalDeniedMessage("Connection denied by user");
                                                    byte[] denialBytes = Encoding.UTF8.GetBytes(denialMsg);
                                                    await transport.WriteAsync(denialBytes, token);
                                                    McpLog.LogDelayed("Sent approval_denied message to client");
                                                }
                                                catch (Exception ex)
                                                {
                                                    McpLog.LogDelayed($"Failed to send approval_denied: {ex.Message}", LogType.Warning);
                                                }
                                            }

                                            return; // Don't send handshake, close connection
                                        }

                                        McpLog.LogDelayed("Connection approved by user");

                                        // Update connection status to accepted (use identity to find the right record)
                                        var approvedRecord = ConnectionRegistry.instance.FindMatchingConnection(identity);
                                        if (approvedRecord != null)
                                        {
                                            ConnectionRegistry.instance.UpdateConnectionStatus(
                                                approvedRecord.Info.ConnectionId,
                                                ValidationStatus.Accepted,
                                                "Approved by user"
                                            );
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        McpLog.LogDelayed($"Approval flow exception: {ex.Message}", LogType.Error);
                                        heartbeatCts.Cancel();
                                        transport.OnDisconnected -= disconnectHandler;
                                        lock (approvalDialogLock) { isApprovalDialogShowing = false; }

                                        lock (pendingApprovalsLock)
                                        {
                                            pendingApprovalsByIdentity.Remove(identityKey);
                                        }

                                        // Update connection status to rejected on error
                                        ConnectionRegistry.instance.UpdateConnectionStatus(
                                            decision.Connection.ConnectionId,
                                            ValidationStatus.Rejected,
                                            $"Approval flow exception: {ex.Message}"
                                        );
                                        return; // Deny on error
                                    }
                                }
                            } // End of if (policy.requiresApproval)
                            else
                            {
                                // Auto-approve without dialog (approval not required)
                                var decisionToRecord = decision;
                                EditorApplication.delayCall += () =>
                                {
                                    ConnectionRegistry.instance.RecordConnection(decisionToRecord);
                                };
                            }
                        } // End of if (decision != null)
                    } // End of if (!skipValidation)

                    // Send handshake AFTER validation completes
                    // Handshake signals "I'm ready to accept commands"
                    // Check if transport is still connected before sending (validation may have taken long enough for client timeout)
                    if (!transport.IsConnected)
                    {
                        McpLog.LogDelayed("Connection closed before handshake could be sent", LogType.Warning);
                        return;
                    }

                    try
                    {
                        await MessageProtocol.SendHandshakeAsync(transport);
                        McpLog.LogDelayed("Sent handshake (unity-mcp protocol v2.0)");
                    }
                    catch (Exception ex)
                    {
                        McpLog.LogDelayed($"Handshake failed: {ex.Message}", LogType.Warning);
                        return;
                    }

                    // Register bidirectional identity <-> transport mapping
                    string connectionIdentityKey = null;
                    if (decision != null && decision.Connection != null)
                    {
                        var identity = ConnectionIdentity.FromConnectionInfo(decision.Connection);
                        if (identity != null && !string.IsNullOrEmpty(identity.CombinedIdentityKey))
                        {
                            connectionIdentityKey = identity.CombinedIdentityKey;
                            lock (clientsLock)
                            {
                                identityToTransportMap[connectionIdentityKey] = transport;
                                transportToIdentityMap[transport] = connectionIdentityKey;
                            }
                            McpLog.LogDelayed($"Registered identity mapping: {connectionIdentityKey} -> {transport.ConnectionId}");
                        }
                    }
                    else
                    {
                        // Validation disabled or no decision - use connection ID as identity
                        // This ensures tests work when validation is disabled
                        connectionIdentityKey = $"no-validation-{transport.ConnectionId}";
                        lock (clientsLock)
                        {
                            identityToTransportMap[connectionIdentityKey] = transport;
                            transportToIdentityMap[transport] = connectionIdentityKey;
                        }
                        McpLog.LogDelayed($"Registered connection without validation: {connectionIdentityKey}");
                    }

                    // Notify listeners on main thread that a client connected
                    // Fire this regardless of validation state - connection is established
                    EditorApplication.delayCall += () => OnClientConnectionChanged?.Invoke();

                    while (isRunning && !token.IsCancellationRequested && transport.IsConnected)
                    {
                        try
                        {
                            // Read message
                            string commandText = await MessageProtocol.ReadMessageAsync(transport);

                            string commandId = Guid.NewGuid().ToString();
                            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                            // Handle ping specially
                            if (commandText.Trim() == "ping")
                            {
                                string pingResponse = JsonConvert.SerializeObject(new
                                {
                                    status = "success",
                                    result = new { message = "pong" }
                                });
                                await MessageProtocol.WriteMessageAsync(transport, pingResponse);
                                continue;
                            }

                            lock (lockObj)
                            {
                                commandQueue[commandId] = (commandText, tcs, transport);
                            }

                            string response = await tcs.Task.ConfigureAwait(false);
                            await MessageProtocol.WriteMessageAsync(transport, response);
                        }
                        catch (Exception ex)
                        {
                            string msg = ex.Message ?? string.Empty;
                            bool isBenign = msg.Contains("closed", StringComparison.OrdinalIgnoreCase)
                                || msg.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                                || ex is TimeoutException;

                            if (isBenign)
                                McpLog.LogDelayed($"Client handler: {msg}");
                            else
                                McpLog.LogDelayed($"Client handler error: {msg}", LogType.Error);
                            break;
                        }
                    }
                }
                finally
                {
                    bool clientRemoved = false;
                    lock (clientsLock)
                    {
                        // Clean up bidirectional mapping
                        if (transportToIdentityMap.TryGetValue(transport, out var identityKey))
                        {
                            identityToTransportMap.Remove(identityKey);
                            transportToIdentityMap.Remove(transport);
                            clientRemoved = true;
                        }
                    }

                    // Clean up any pending ACP tokens
                    lock (acpTokenLock)
                    {
                        pendingAcpTokens.Remove(transport);
                    }

                    // Notify listeners on main thread that a client disconnected
                    if (clientRemoved)
                    {
                        EditorApplication.delayCall += () => OnClientConnectionChanged?.Invoke();
                    }
                }
            }
        }

        void ProcessCommands()
        {
            if (!isRunning) return;
            // Reentrancy guard
            if (Interlocked.Exchange(ref processingCommands, 1) == 1) return;

            try
            {
                // Heartbeat
                double now = EditorApplication.timeSinceStartup;
                if (now >= nextHeartbeatAt)
                {
                    ServerDiscovery.SaveStatusFile(currentConnectionPath, "ready");
                    nextHeartbeatAt = now + 0.5f;
                }

                // Cache cleanup for completed commands (every 60 seconds)
                if (now >= nextCacheCleanupAt)
                {
                    CleanExpiredCommandResults();
                    nextCacheCleanupAt = now + 60;
                }

                // Snapshot commands
                List<(string id, string text, TaskCompletionSource<string> tcs, IConnectionTransport client)> work;
                lock (lockObj)
                {
                    work = commandQueue.Select(kvp => (kvp.Key, kvp.Value.commandJson, kvp.Value.tcs, kvp.Value.client)).ToList();
                }

                foreach (var item in work)
                {
                    string id = item.id;
                    string commandText = item.text;
                    TaskCompletionSource<string> tcs = item.tcs;
                    IConnectionTransport client = item.client;

                    try
                    {
                        // Special case handling
                        if (string.IsNullOrEmpty(commandText))
                        {
                            tcs.SetResult(JsonConvert.SerializeObject(new { status = "error", error = "Empty command received" }));
                            lock (lockObj) { commandQueue.Remove(id); }
                            continue;
                        }

                        commandText = commandText.Trim();

                        // Non-JSON direct commands handling (like ping)
                        if (commandText == "ping")
                        {
                            tcs.SetResult(JsonConvert.SerializeObject(new { status = "success", result = new { message = "pong" } }));
                            lock (lockObj) { commandQueue.Remove(id); }
                            continue;
                        }

                        // Check if the command is valid JSON before attempting to deserialize
                        if (!IsValidJson(commandText))
                        {
                            tcs.SetResult(JsonConvert.SerializeObject(new
                            {
                                status = "error",
                                error = "Invalid JSON format",
                                receivedText = commandText.Length > 50 ? commandText[..50] + "..." : commandText
                            }));
                            lock (lockObj) { commandQueue.Remove(id); }
                            continue;
                        }

                        // Normal JSON command processing
                        Command command = JsonConvert.DeserializeObject<Command>(commandText);
                        if (command == null)
                        {
                            tcs.SetResult(JsonConvert.SerializeObject(new
                            {
                                status = "error",
                                error = "Command deserialized to null"
                            }));
                            lock (lockObj) { commandQueue.Remove(id); }
                        }
                        else
                        {
                            // Remove from queue BEFORE starting async execution to prevent
                            // re-processing on subsequent Update frames
                            lock (lockObj) { commandQueue.Remove(id); }

                            // Deduplication: check if this requestId is already being processed or completed
                            if (!string.IsNullOrEmpty(command.requestId))
                            {
                                // Check for in-flight duplicate
                                Task<string> existingTask;
                                lock (lockObj)
                                {
                                    inFlightCommands.TryGetValue(command.requestId, out existingTask);
                                }

                                if (existingTask != null)
                                {
                                    // Wait for existing task and return its result
                                    _ = WaitForExistingAndComplete(existingTask, tcs, command.requestId);
                                    continue;
                                }

                                // Check for completed duplicate
                                lock (lockObj)
                                {
                                    if (completedCommands.TryGetValue(command.requestId, out var cached) &&
                                        cached.expiry > DateTime.UtcNow)
                                    {
                                        tcs.SetResult(cached.result);
                                        continue;
                                    }
                                }
                            }

                            // Start execution and track the result Task
                            Task<string> resultTask = ExecuteCommandWithHeartbeatAsync(command, client);

                            // Complete the TCS when execution finishes (bridges to background I/O thread)
                            _ = resultTask.ContinueWith(t =>
                            {
                                if (t.IsFaulted)
                                    tcs.SetResult(JsonConvert.SerializeObject(new { status = "error", error = t.Exception?.InnerException?.Message ?? "Unknown error" }));
                                else
                                    tcs.SetResult(t.Result);
                            }, TaskScheduler.Default);

                            // Track by requestId for deduplication
                            if (!string.IsNullOrEmpty(command.requestId))
                            {
                                lock (lockObj)
                                {
                                    inFlightCommands[command.requestId] = resultTask;
                                }

                                // When complete, move to cache (fire-and-forget continuation)
                                string reqId = command.requestId; // Capture for closure
                                _ = resultTask.ContinueWith(t =>
                                {
                                    lock (lockObj)
                                    {
                                        inFlightCommands.Remove(reqId);
                                        if (t.IsCompletedSuccessfully)
                                        {
                                            completedCommands[reqId] = (t.Result, DateTime.UtcNow + ResultCacheDuration);
                                        }
                                    }
                                }, TaskScheduler.Default);
                            }
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error processing command: {ex.Message}\\n{ex.StackTrace}");
                        tcs.SetResult(JsonConvert.SerializeObject(new
                        {
                            status = "error",
                            error = ex.Message,
                            commandType = "Unknown"
                        }));
                        lock (lockObj) { commandQueue.Remove(id); }
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref processingCommands, 0);
            }
        }

        async Task<string> ExecuteCommandAsync(Command command, IConnectionTransport client, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(command.type))
                {
                    return JsonConvert.SerializeObject(new
                    {
                        status = "error",
                        error = "Command type cannot be empty"
                    });
                }

                // Handle ping command for connection verification
                if (command.type.Equals("ping", StringComparison.OrdinalIgnoreCase))
                {
                    return JsonConvert.SerializeObject(new { status = "success", result = new { message = "pong" } });
                }

                if (command.type.Equals("set_client_info", StringComparison.OrdinalIgnoreCase))
                {
                    string name = command.@params?.Value<string>("name") ?? "unknown";
                    string version = command.@params?.Value<string>("version") ?? "unknown";
                    string title = command.@params?.Value<string>("title");

                    var clientInfo = new ClientInfo
                    {
                        Name = name,
                        Version = version,
                        Title = title,
                        ConnectionId = client.ConnectionId
                    };

                    // Update ConnectionRegistry with client info (must run on main thread)
                    string identityKey = null;
                    lock (clientsLock)
                    {
                        if (transportToIdentityMap.TryGetValue(client, out identityKey))
                        {
                            EditorApplication.delayCall += () =>
                            {
                                ConnectionRegistry.instance.UpdateClientInfo(identityKey, clientInfo);
                            };
                        }
                    }

                    string displayName = string.IsNullOrEmpty(title) ? name : title;
                    McpLog.Log($"MCP client info: {displayName} v{version}");

                    return JsonConvert.SerializeObject(new
                    {
                        status = "success",
                        result = new { message = "Client info received" }
                    });
                }

                if (command.type.Equals("get_available_tools", StringComparison.OrdinalIgnoreCase))
                {
                    string requestedHash = command.@params?.Value<string>("hash");

                    // Recompute when hash is missing or differs from request
                    if (string.IsNullOrEmpty(s_CurrentToolsHash) || s_CurrentToolsHash != requestedHash)
                    {
                        ComputeToolsSnapshotAndHash();
                        McpLog.Log($"Tools changed: hash={s_CurrentToolsHash}, count={s_ToolsSnapshot?.Length ?? 0}");
                    }
                    // No logging for unchanged case - it's periodic polling noise

                    if (requestedHash == s_CurrentToolsHash)
                    {
                        return JsonConvert.SerializeObject(new
                        {
                            status = "success",
                            result = new { unchanged = true, hash = s_CurrentToolsHash }
                        });
                    }

                    var response = JsonConvert.SerializeObject(new
                    {
                        status = "success",
                        result = new
                        {
                            hash = s_CurrentToolsHash,
                            tools = s_ToolsSnapshot ?? Array.Empty<McpToolInfo>()
                        }
                    });
                    McpLog.Log($"Sending tools response with {s_ToolsSnapshot?.Length ?? 0} tools");
                    return response;
                }

                // Handle MCP tool approval requests (from Codex via MCP)
                if (command.type.Equals("mcp/request_tool_approval", StringComparison.OrdinalIgnoreCase))
                {
                    return await HandleMcpToolApprovalAsync(command);
                }

                // Use JObject for parameters as the handlers expect this
                JObject paramsObject = command.@params ?? new JObject();

                // Route command through the registry
                object result = await McpToolRegistry.ExecuteToolAsync(command.type, paramsObject);
                if (result == null)
                    result = Response.Success("Operation completed.");

                // Standard success response format
                return JsonConvert.SerializeObject(new { status = "success", result });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing command '{command?.type ?? "Unknown"}': {ex.Message}\\n{ex.StackTrace}");
                return JsonConvert.SerializeObject(new
                {
                    status = "error",
                    error = ex.Message,
                    command = command?.type ?? "Unknown"
                });
            }
        }

        async Task<string> ExecuteCommandWithHeartbeatAsync(Command command, IConnectionTransport client)
        {
            using var heartbeatCts = new CancellationTokenSource();
            Task heartbeatTask = null;

            try
            {
                // Send immediate acknowledgement that command is being processed
                try
                {
                    if (client.IsConnected)
                    {
                        string ackMsg = MessageProtocol.CreateCommandInProgressMessage();
                        byte[] ackBytes = Encoding.UTF8.GetBytes(ackMsg);
                        await client.WriteAsync(ackBytes, heartbeatCts.Token);
                    }
                }
                catch (Exception ackEx)
                {
                    McpLog.LogDelayed($"Failed to send command_in_progress acknowledgement: {ackEx.Message}");
                    // Continue anyway - the command should still execute
                }

                // Start heartbeat loop to keep client from timing out
                // Send heartbeats every 1.5 seconds (within 2s timeout window)
                heartbeatTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!heartbeatCts.Token.IsCancellationRequested)
                        {
                            await Task.Delay(1500, heartbeatCts.Token);

                            if (client.IsConnected && !heartbeatCts.Token.IsCancellationRequested)
                            {
                                string heartbeatMsg = MessageProtocol.CreateCommandInProgressMessage();
                                byte[] heartbeatBytes = Encoding.UTF8.GetBytes(heartbeatMsg);
                                await client.WriteAsync(heartbeatBytes, heartbeatCts.Token);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when command execution completes
                    }
                    catch (Exception ex)
                    {
                        McpLog.LogDelayed($"Command heartbeat error: {ex.Message}");
                    }
                }, heartbeatCts.Token);

                // Execute the command
                return await ExecuteCommandAsync(command, client);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing command async: {ex.Message}\n{ex.StackTrace}");
                return JsonConvert.SerializeObject(new
                {
                    status = "error",
                    error = ex.Message,
                    command = command?.type ?? "Unknown"
                });
            }
            finally
            {
                // Stop heartbeat BEFORE returning to ensure no heartbeats sent after response
                heartbeatCts.Cancel();

                // Wait for heartbeat task to stop (with timeout to avoid hanging)
                if (heartbeatTask != null)
                {
                    await Task.WhenAny(heartbeatTask, Task.Delay(500));
                }
            }
        }

        /// <summary>
        /// Handle MCP tool approval requests from Codex.
        /// Routes through RelayService.OnMcpToolApprovalRequest if registered, otherwise auto-approves.
        /// </summary>
        async Task<string> HandleMcpToolApprovalAsync(Command command)
        {
            var token = command.@params?.Value<string>("token");
            var toolName = command.@params?.Value<string>("toolName");
            var args = command.@params?["args"]?.ToString() ?? "{}";
            var toolCallId = command.@params?.Value<string>("toolCallId") ?? Guid.NewGuid().ToString();

            McpLog.Log($"[MCP Approval] Received tool approval request: {toolName}");

            // Look up session by token
            var sessionInfo = McpSessionTokenRegistry.FindByMcpToken(token);
            if (!sessionInfo.HasValue)
            {
                // No valid session - auto-approve (standalone MCP connection or expired token)
                McpLog.Log($"[MCP Approval] No valid session for token - auto-approving: {toolName}");
                return JsonConvert.SerializeObject(new
                {
                    status = "success",
                    result = new { approved = true, reason = "No active session (auto-approved)" }
                });
            }

            var (sessionId, provider) = sessionInfo.Value;
            McpLog.Log($"[MCP Approval] Session found: {sessionId} (provider: {provider})");

            try
            {
                // Route through RelayService to the approval handler
                var request = new RelayService.McpToolApprovalRequest(sessionId, provider, toolName, args, toolCallId);
                var response = await RelayService.Instance.RequestMcpToolApprovalAsync(request);

                McpLog.Log($"[MCP Approval] Tool {toolName}: {(response.Approved ? "approved" : "rejected")} - {response.Reason}");

                return JsonConvert.SerializeObject(new
                {
                    status = "success",
                    result = new { approved = response.Approved, reason = response.Reason, alwaysAllow = response.AlwaysAllow }
                });
            }
            catch (Exception ex)
            {
                McpLog.Warning($"[MCP Approval] Error processing approval for {toolName}: {ex.Message}");
                return JsonConvert.SerializeObject(new
                {
                    status = "success",
                    result = new { approved = false, reason = $"Approval error: {ex.Message}" }
                });
            }
        }

        void OnBeforeAssemblyReload()
        {
            // Stop cleanly before reload
            try { Stop(); } catch { }
            // Avoid file I/O or heavy work here
        }

        void OnAfterAssemblyReload()
        {
            ServerDiscovery.SaveStatusFile(currentConnectionPath ?? ServerDiscovery.GetConnectionPath(), "idle");
            LogBreadcrumb("Idle");
            // Schedule a safe restart after reload to avoid races during compilation
            ScheduleInitRetry();
        }

        void OnToolsChanged(McpToolRegistry.ToolChangeEventArgs args)
        {
            // Notify connected clients about tool changes
            // This allows MCP clients to refresh their tool list without reconnecting
            if (isRunning && args != null)
            {
                var changeType = args.ChangeType.ToString().ToLowerInvariant();
                var message = args.ChangeType == McpToolRegistry.ToolChangeType.Refreshed
                    ? "Tools were refreshed"
                    : $"Tool '{args.ToolName}' was {changeType}";
                McpLog.Log($"[UnityMCPBridge] {message}");
            }
            // Invalidate cached tools hash/snapshot so it will be recomputed on next request
            s_CurrentToolsHash = null;
            s_ToolsSnapshot = null;
        }

        public void InvalidateToolsCache()
        {
            s_CurrentToolsHash = null;
            s_ToolsSnapshot = null;
        }

        /// <summary>
        /// Wait for an existing in-flight task and complete the TCS with its result.
        /// Used for command deduplication when a duplicate requestId is detected.
        /// </summary>
        async Task WaitForExistingAndComplete(Task<string> existingTask, TaskCompletionSource<string> tcs, string requestId)
        {
            try
            {
                string result = await existingTask;
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                McpLog.LogDelayed($"Error waiting for existing command {requestId}: {ex.Message}");
                tcs.SetResult(JsonConvert.SerializeObject(new
                {
                    status = "error",
                    error = $"Deduplication error: {ex.Message}"
                }));
            }
        }

        /// <summary>
        /// Clean expired entries from the completed commands cache.
        /// </summary>
        void CleanExpiredCommandResults()
        {
            lock (lockObj)
            {
                var expiredKeys = completedCommands
                    .Where(kvp => kvp.Value.expiry <= DateTime.UtcNow)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    completedCommands.Remove(key);
                }

                if (expiredKeys.Count > 0)
                {
                    McpLog.Log($"[Command Cache] Cleaned {expiredKeys.Count} expired entries");
                }
            }
        }

        void ComputeToolsSnapshotAndHash()
        {
            s_ToolsSnapshot = McpToolRegistry.GetAvailableTools();
            var tools = s_ToolsSnapshot ?? Array.Empty<McpToolInfo>();
            var minimal = new object[tools.Length];
            for (int i = 0; i < tools.Length; i++)
            {
                minimal[i] = new { tools[i].name, tools[i].description, tools[i].inputSchema };
            }
            var json = JsonConvert.SerializeObject(minimal, Formatting.None);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
            s_CurrentToolsHash = Convert.ToBase64String(hashBytes);
        }

        static bool IsValidJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if ((text.StartsWith("{") && text.EndsWith("}")) || (text.StartsWith("[") && text.EndsWith("]")))
            {
                try { JToken.Parse(text); return true; } catch { return false; }
            }
            return false;
        }

        void LogConnectionDecision(ValidationDecision decision)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Connection Info ===");

            // Server info
            sb.AppendLine("MCP Server:");
            var server = decision.Connection?.Server;
            if (server != null)
            {
                sb.AppendLine($"  PID: {server.ProcessId}");
                sb.AppendLine($"  Name: {server.ProcessName ?? "unknown"}");
                sb.AppendLine($"  Executable: {server.Identity?.Path ?? "unknown"}");
                if (server.Identity != null)
                {
                    sb.AppendLine($"  Hash: {server.Identity.SHA256Hash?.Substring(0, 16) ?? "unknown"}...");
                    sb.AppendLine($"  Signed: {(server.Identity.IsSigned ? "Yes" : "No")}");
                    if (server.Identity.IsSigned)
                    {
                        sb.AppendLine($"  Publisher: {server.Identity.SignaturePublisher ?? "unknown"}");
                        sb.AppendLine($"  Signature Valid: {(server.Identity.SignatureValid ? "Yes" : "No")}");
                    }
                }
            }
            else
            {
                sb.AppendLine("  Unable to determine server info");
            }

            // Validation status
            sb.AppendLine($"  Validation: {decision.Status}");
            sb.AppendLine($"  Reason: {decision.Reason}");

            // Client info
            sb.AppendLine("MCP Client:");
            var client = decision.Connection?.Client;
            if (client != null)
            {
                sb.AppendLine($"  Name: {client.ProcessName ?? "unknown"}");
                sb.AppendLine($"  PID: {client.ProcessId}");
                sb.AppendLine($"  Executable: {client.Identity?.Path ?? "unknown"}");
                if (decision.Connection.ClientChainDepth > 0)
                {
                    sb.AppendLine($"  Chain depth: {decision.Connection.ClientChainDepth} (walked up {decision.Connection.ClientChainDepth} level{(decision.Connection.ClientChainDepth == 1 ? "" : "s")})");
                }
            }
            else
            {
                sb.AppendLine("  Unable to determine (parent may have exited or permissions denied)");
            }

            sb.Append("======================");

            // Use LogDelayed since this is called from HandleClientAsync background thread
            McpLog.LogDelayed(sb.ToString());
        }

        static bool IsCompiling()
        {
            if (EditorApplication.isCompiling) return true;
            try
            {
                Type pipeline = Type.GetType("UnityEditor.Compilation.CompilationPipeline, UnityEditor");
                var prop = pipeline?.GetProperty("isCompiling", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null) return (bool)prop.GetValue(null);
            }
            catch { }
            return false;
        }

        static void LogBreadcrumb(string stage) => McpLog.Log($"[{stage}]");

        /// <summary>
        /// Handle MCP session token registration from Relay.
        /// Called when an AI Gateway session starts and pre-registers a token for auto-approval.
        /// </summary>
        void OnMcpSessionRegister(McpSessionRegistration registration)
        {
            McpSessionTokenRegistry.RegisterSession(registration);
        }

        /// <summary>
        /// Handle MCP session token unregistration from Relay.
        /// Called when an AI Gateway session ends.
        /// </summary>
        void OnMcpSessionUnregister(string sessionId)
        {
            McpSessionTokenRegistry.UnregisterSession(sessionId);

            // Also clean up any gateway connections for this session
            // This removes them from the non-persisted list (for UI cleanup)
            EditorApplication.delayCall += () =>
            {
                ConnectionRegistry.instance.RemoveGatewayConnectionsForSession(sessionId);
            };
        }

        /// <summary>
        /// Try to read an ACP token message from the client with a short timeout.
        /// MCP clients from AI Gateway send this token immediately after connecting.
        /// Returns the token if present, null otherwise.
        /// </summary>
        async Task<string> TryReadAcpTokenAsync(IConnectionTransport transport, CancellationToken ct)
        {
            try
            {
                // Try to read a newline-delimited message with a short timeout
                // If no token is sent within timeout, proceed with normal flow
                const byte newlineDelimiter = 0x0A; // '\n'
                const int maxBytes = 1024;
                const int timeoutMs = 100; // Short timeout - token should arrive quickly

                var messageData = await transport.ReadUntilDelimiterAsync(newlineDelimiter, maxBytes, timeoutMs, ct);
                if (messageData == null || messageData.Length == 0)
                {
                    return null;
                }

                var messageText = Encoding.UTF8.GetString(messageData).Trim();

                // Try to parse as JSON
                try
                {
                    var message = JObject.Parse(messageText);
                    var type = message.Value<string>("type");

                    if (type == "set_acp_token")
                    {
                        var paramsObj = message["params"] as JObject;
                        return paramsObj?.Value<string>("token");
                    }
                }
                catch (JsonException)
                {
                    // Not valid JSON, ignore
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                // Timeout - no token sent, proceed with normal flow
                return null;
            }
            catch (TimeoutException)
            {
                // Timeout - no token sent, proceed with normal flow
                return null;
            }
            catch (Exception ex)
            {
                McpLog.LogDelayed($"[ACP Token] Error reading token: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get information about currently connected clients
        /// </summary>
        public ClientInfo[] GetConnectedClients()
        {
            var activeRecords = ConnectionRegistry.instance.GetActiveConnections(GetActiveIdentityKeys());
            return activeRecords
                .Select(r => r.Info?.ClientInfo)
                .Where(ci => ci != null)
                .ToArray();
        }

        /// <summary>
        /// Get the count of currently connected clients
        /// </summary>
        public int GetConnectedClientCount()
        {
            lock (clientsLock)
            {
                return identityToTransportMap.Count;
            }
        }
    }
}
