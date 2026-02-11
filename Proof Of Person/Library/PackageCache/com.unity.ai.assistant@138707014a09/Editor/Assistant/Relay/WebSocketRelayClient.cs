using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Utils;
using UnityEngine;

namespace Unity.Relay
{
    /// <summary>
    /// Information about an MCP session registration from the AI Gateway.
    /// Bundles sessionId, token, and provider for cleaner parameter passing.
    /// </summary>
    record McpSessionRegistration(string SessionId, string Token, string Provider = null)
    {
        public bool IsValid => !string.IsNullOrEmpty(SessionId) && !string.IsNullOrEmpty(Token);
    }

    /// <summary>
    /// WebSocket client for bi-directional communication with the Relay server
    /// </summary>
    class WebSocketRelayClient : IDisposable
    {
        ClientWebSocket m_WebSocket;
        CancellationTokenSource m_CancellationTokenSource;
        readonly Dictionary<string, TaskCompletionSource<WebSocketMessage>> m_PendingRequests;
        bool m_Disposed;
        string m_LastReceivedRawMessage; // Used for re-parsing messages with additional fields

        protected bool m_IsConnected;

        // Events for bi-directional communication
        public event Action OnConnected;
        public event Action OnDisconnected;

        // Events for relay-specific messages
        public event Action OnReplayComplete;

        // Events for MCP session token management (for auto-approval)
        public event Action<McpSessionRegistration> OnMcpSessionRegister;
        public event Action<string> OnMcpSessionUnregister;  // sessionId

        // Events for AI Assistant protocol - non-gateway messages only
        public event Action<string> OnAssistantMessage;

        // Events for gateway protocol messages (gateway/*)
        public event Action<string> OnGatewayMessage;

        // Events for credential storage responses
        public event Action<string> OnCredentialResponse;

        // Events for message logging (developer tools)
        public event Action<string> OnMessageSent;
        public event Action<string> OnMessageReceived;

        public virtual bool IsConnected => m_IsConnected && m_WebSocket?.State == WebSocketState.Open;

        /// <summary>
        /// Creates a new WebSocketRelayClient instance
        /// </summary>
        public WebSocketRelayClient()
        {
            m_PendingRequests = new Dictionary<string, TaskCompletionSource<WebSocketMessage>>();
            InitializeWebSocket();
        }

        void InitializeWebSocket()
        {
            try
            {
                m_WebSocket = new ClientWebSocket();
                m_WebSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30); // Client-side keep-alive

                // Create new cancellation token source
                m_CancellationTokenSource?.Dispose();
                m_CancellationTokenSource = new CancellationTokenSource();
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[WebSocketRelayClient] Failed to initialize: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Connect to the WebSocket server
        /// </summary>
        /// <param name="serverAddress">WebSocket server address (e.g., ws://127.0.0.1:9001)</param>
        /// <param name="timeoutMs">Connection timeout in milliseconds (default 3000ms)</param>
        public async Task<bool> ConnectAsync(string serverAddress, int timeoutMs = 3000)
        {
            if (m_Disposed)
                return false;

            try
            {
                // If WebSocket is not in None state, we need to recreate it
                if (m_WebSocket.State != WebSocketState.None)
                {
                    // Recreating WebSocket connection
                    m_WebSocket?.Dispose();
                    InitializeWebSocket();
                }

                var uri = new Uri(serverAddress);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(m_CancellationTokenSource.Token);

                // Use Task.WhenAny pattern for timeout - timeout task not linked to cts for deterministic behavior
                var connectTask = m_WebSocket.ConnectAsync(uri, cts.Token);
                var timeoutTask = Task.Delay(timeoutMs);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == timeoutTask)
                {
                    cts.Cancel(); // abort connect attempt
                    m_WebSocket?.Dispose();
                    InitializeWebSocket();
                    return false;
                }

                await connectTask.ConfigureAwait(false); // Propagate any exceptions

                if (m_WebSocket.State == WebSocketState.Open)
                {
                    m_IsConnected = true;
                    // Connected successfully

                    // Start listening for messages
                    _ = Task.Run(ListenForMessages);

                    OnConnected?.Invoke();
                    return true;
                }
                else
                {
                    // Connection failures are expected during retry loop - caller handles logging
                    return false;
                }
            }
            catch (Exception ex) when (IsExpectedConnectionException(ex))
            {
                // Expected connection failures (server not ready, timeout, etc.) - caller handles retry/logging
                m_IsConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Listen for incoming WebSocket messages
        /// </summary>
        async Task ListenForMessages()
        {
            try
            {
                while (!m_Disposed &&
                       m_WebSocket?.State == WebSocketState.Open &&
                       m_CancellationTokenSource?.Token.IsCancellationRequested == false)
                {
                    var buffer = new ArraySegment<byte>(new byte[1024]);
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;

                    // Accumulate message fragments until EndOfMessage is true
                    do
                    {
                        if (m_Disposed || m_WebSocket == null || m_CancellationTokenSource == null)
                            return;

                        result = await m_WebSocket.ReceiveAsync(buffer, m_CancellationTokenSource.Token);

                        if (buffer.Array == null)
                            continue;

                        ms.Write(buffer.Array, buffer.Offset, result.Count);

                    } while (result == null || !result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        byte[] bytes = ms.ToArray();
                        var messageText = Encoding.UTF8.GetString(bytes);

                        // Log all received messages for developer tools
                        OnMessageReceived?.Invoke(messageText);

                        // Store raw message for handlers that need additional fields
                        m_LastReceivedRawMessage = messageText;

                        // Try to parse as relay protocol message
                        // If it's not a relay message, HandleRelayMessage will return false
                        WebSocketMessage message = null;
                        try
                        {
                            message = AssistantJsonHelper.Deserialize<WebSocketMessage>(messageText);
                        }
                        catch (JsonException) { }

                        bool handledByRelayProtocol = message != null && await HandleRelayMessage(message);

                        if (!handledByRelayProtocol)
                        {
                            // Route based on $type prefix
                            if (IsGatewayMessage(messageText))
                                OnGatewayMessage?.Invoke(messageText);
                            else
                                OnAssistantMessage?.Invoke(messageText);
                        }

                        // Clear raw message after processing
                        m_LastReceivedRawMessage = null;
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        m_IsConnected = false;
                        OnDisconnected?.Invoke();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't log expected cleanup exceptions
                if (!IsExpectedCleanupException(ex))
                {
                    InternalLog.LogError($"[WebSocketRelayClient] Error listening for messages: {ex.Message}");
                }

                m_IsConnected = false;
                OnDisconnected?.Invoke();
            }
        }

        /// <summary>
        /// Check if message has a gateway/* $type prefix.
        /// Uses simple string search to avoid JSON parsing overhead.
        /// </summary>
        static bool IsGatewayMessage(string json)
        {
            // Look for "$type":"gateway/ or "$type": "gateway/
            return json.Contains("\"$type\":\"gateway/") ||
                   json.Contains("\"$type\": \"gateway/");
        }

        /// <summary>
        /// Check if exception is expected during connection attempts (server not ready, timeout, etc.)
        /// </summary>
        static bool IsExpectedConnectionException(Exception ex)
        {
            return ex is WebSocketException ||
                   ex is OperationCanceledException ||
                   ex is SocketException ||
                   ex is HttpRequestException ||
                   ex is IOException;
        }

        /// <summary>
        /// Check if exception is expected during cleanup (not an actual error)
        /// </summary>
        bool IsExpectedCleanupException(Exception ex)
        {
            // These exceptions are expected during normal cleanup/disposal
            return ex is OperationCanceledException ||
                   ex is ObjectDisposedException ||
                   (ex is WebSocketException wsEx &&
                    (wsEx.Message.Contains("Aborted") || wsEx.Message.Contains("closed"))) ||
                   ex.Message.Contains("Aborted") ||
                   m_CancellationTokenSource?.Token.IsCancellationRequested == true ||
                   m_Disposed;
        }

        /// <summary>
        /// Handle incoming messages (responses and server-initiated messages)
        /// </summary>
        Task<bool> HandleRelayMessage(WebSocketMessage message)
        {
            switch (message.type)
            {
                case RelayConstants.RELAY_PONG:
                    // Handle ping response
                    if (m_PendingRequests.TryGetValue(message.id, out var tcs))
                    {
                        m_PendingRequests.Remove(message.id);
                        tcs.SetResult(message);
                        return Task.FromResult(true);
                    }
                    return Task.FromResult(false);

                case RelayConstants.RELAY_RECOVER_MESSAGES_COMPLETED:
                    // Replay complete signal
                    InternalLog.Log("[WebSocketRelayClient] Replay complete signal received");
                    OnReplayComplete?.Invoke();
                    return Task.FromResult(true);

                case RelayConstants.RELAY_MESSAGE_PARSE_ERROR:
                    // Server couldn't parse a message
                    InternalLog.LogWarning($"[WebSocketRelayClient] Server parse error: {message.message}");
                    return Task.FromResult(true);

                case RelayConstants.RELAY_UNKNOWN_MESSAGE_TYPE:
                    // Server received unknown message type
                    InternalLog.LogWarning($"[WebSocketRelayClient] Server unknown message type: {message.message}");
                    return Task.FromResult(true);

                case RelayConstants.RELAY_MCP_SESSION_REGISTER:
                    // MCP session token pre-registration for auto-approval
                    HandleMcpSessionRegister(message);
                    return Task.FromResult(true);

                case RelayConstants.RELAY_MCP_SESSION_UNREGISTER:
                    // MCP session token unregistration
                    HandleMcpSessionUnregister(message);
                    return Task.FromResult(true);

                // Credential storage responses
                case RelayConstants.RELAY_CREDENTIAL_STORE_RESPONSE:
                case RelayConstants.RELAY_CREDENTIAL_RETRIEVE_RESPONSE:
                case RelayConstants.RELAY_CREDENTIAL_DELETE_RESPONSE:
                case RelayConstants.RELAY_CREDENTIAL_LIST_RESPONSE:
                case RelayConstants.RELAY_CREDENTIAL_RETRIEVE_ALL_RESPONSE:
                case RelayConstants.RELAY_CREDENTIAL_AVAILABLE_RESPONSE:
                    // Forward credential responses to subscribers
                    if (!string.IsNullOrEmpty(m_LastReceivedRawMessage))
                    {
                        OnCredentialResponse?.Invoke(m_LastReceivedRawMessage);
                    }
                    return Task.FromResult(true);

                default:
                    // Unknown message type, let AI Assistant protocol handle it
                    return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Handle MCP session token registration for auto-approval.
        /// Fires OnMcpSessionRegister event for subscribers to handle.
        /// </summary>
        void HandleMcpSessionRegister(WebSocketMessage message)
        {
            try
            {
                // Re-parse the message to get additional fields
                if (!string.IsNullOrEmpty(m_LastReceivedRawMessage))
                {
                    var json = JObject.Parse(m_LastReceivedRawMessage);
                    var sessionId = json.Value<string>("sessionId");
                    var token = json.Value<string>("token");
                    var provider = json.Value<string>("provider");  // "claude-code", "gemini", etc.

                    var registration = new McpSessionRegistration(sessionId, token, provider);
                    if (registration.IsValid)
                    {
                        OnMcpSessionRegister?.Invoke(registration);
                    }
                    else
                    {
                        InternalLog.LogWarning("[WebSocketRelayClient] MCP session register missing sessionId or token");
                    }
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[WebSocketRelayClient] Failed to parse MCP session register: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle MCP session token unregistration.
        /// Fires OnMcpSessionUnregister event for subscribers to handle.
        /// </summary>
        void HandleMcpSessionUnregister(WebSocketMessage message)
        {
            try
            {
                if (!string.IsNullOrEmpty(m_LastReceivedRawMessage))
                {
                    var json = JObject.Parse(m_LastReceivedRawMessage);
                    var sessionId = json.Value<string>("sessionId");

                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        OnMcpSessionUnregister?.Invoke(sessionId);
                    }
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[WebSocketRelayClient] Failed to parse MCP session unregister: {ex.Message}");
            }
        }


        /// <summary>
        /// Send a ping to test connection
        /// </summary>
        public async Task<bool> PingAsync()
        {
            if (!IsConnected)
                return false;

            try
            {
                var requestId = Guid.NewGuid().ToString();
                var message = new WebSocketMessage
                {
                    type = RelayConstants.RELAY_PING,
                    id = requestId
                };

                var response = await SendRequestAsync(message);
                return response?.type == RelayConstants.RELAY_PONG;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[WebSocketRelayClient] Ping error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Send RELAY_BLOCK_INCOMING_CLOUD_MESSAGES signal to server
        /// </summary>
        public async Task<bool> SendWaitingDomainReloadAsync()
        {
            if (!IsConnected)
                return false;

            try
            {
                var message = new WebSocketMessage
                {
                    type = RelayConstants.RELAY_BLOCK_INCOMING_CLOUD_MESSAGES,
                    timestamp = DateTime.UtcNow.ToString("O")
                };

                InternalLog.Log($"[WebSocketRelayClient] Sending {RelayConstants.RELAY_BLOCK_INCOMING_CLOUD_MESSAGES} signal to server");

                // Send message without waiting for a response from the relay
                var json = AssistantJsonHelper.Serialize(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                await m_WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, m_CancellationTokenSource.Token);

                return true;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[WebSocketRelayClient] {RelayConstants.RELAY_BLOCK_INCOMING_CLOUD_MESSAGES} error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Request relay server to replay incomplete message through normal streaming route
        /// </summary>
        public async Task<bool> ReplayIncompleteMessageAsync()
        {
            if (!IsConnected)
            {
                InternalLog.LogWarning("[WebSocketRelayClient] Not connected, cannot replay incomplete message");
                return false;
            }

            try
            {
                // Send request without waiting for response
                // The actual replay will flow through OnAssistantMessage event
                // Completion is signaled via RELAY_RECOVER_MESSAGES_COMPLETED message
                var message = new WebSocketMessage
                {
                    type = RelayConstants.RELAY_RECOVER_MESSAGES,
                    id = Guid.NewGuid().ToString()
                };

                var json = AssistantJsonHelper.Serialize(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                await m_WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, m_CancellationTokenSource.Token);

                InternalLog.Log($"[WebSocketRelayClient] Replay request sent - waiting for {RelayConstants.RELAY_RECOVER_MESSAGES_COMPLETED} signal");
                return true;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[WebSocketRelayClient] ReplayIncompleteMessage error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Send shutdown signal to server (triggers server shutdown)
        /// </summary>
        public async Task<bool> ShutdownServerAsync()
        {
            if (!IsConnected)
                return false;

            try
            {
                var requestId = Guid.NewGuid().ToString();
                var message = new WebSocketMessage
                {
                    type = RelayConstants.RELAY_SHUTDOWN,
                    id = requestId
                };

                InternalLog.Log($"[WebSocketRelayClient] Sending {RelayConstants.RELAY_SHUTDOWN} signal to server");

                // Send message without waiting for response (fire-and-forget for Unity quit scenarios)
                var json = AssistantJsonHelper.Serialize(message);
                var bytes = Encoding.UTF8.GetBytes(json);
                await m_WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, m_CancellationTokenSource.Token);

                return true;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[WebSocketRelayClient] Shutdown error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Send raw message data
        /// </summary>
        public async Task<bool> SendRawMessageAsync(string messageJson, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                InternalLog.LogError("[WebSocketRelayClient] Not connected to server");
                return false;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(messageJson);
                await m_WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
                OnMessageSent?.Invoke(messageJson);
                return true;
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected behavior when user cancels a conversation - not an error
                InternalLog.Log($"[WebSocketRelayClient] Send cancelled");
                return false;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[WebSocketRelayClient] Send raw message error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Send a request and wait for response
        /// </summary>
        async Task<WebSocketMessage> SendRequestAsync(WebSocketMessage message, int timeoutSeconds = 10)
        {
            var tcs = new TaskCompletionSource<WebSocketMessage>();
            m_PendingRequests[message.id] = tcs;

            try
            {
                var json = AssistantJsonHelper.Serialize(message);
                var bytes = Encoding.UTF8.GetBytes(json);

                await m_WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, m_CancellationTokenSource.Token);

                // Wait for response with timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), m_CancellationTokenSource.Token);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask != tcs.Task)
                {
                    InternalLog.LogError($"[WebSocketRelayClient] Request timeout: {message.type}");
                    return null;
                }

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[WebSocketRelayClient] Send request error: {ex.Message}");
                return null;
            }
            finally
            {
                m_PendingRequests.Remove(message.id);
            }
        }

        /// <summary>
        /// Dispose and close the WebSocket connection
        /// </summary>
        public void Dispose()
        {
            if (!m_Disposed)
            {
                // Capture connection state before cleanup for logging
                bool wasConnected = m_IsConnected;

                try
                {
                    m_CancellationTokenSource?.Cancel();

                    if (m_WebSocket?.State == WebSocketState.Open)
                    {
                        m_WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disposing", CancellationToken.None).Wait(1000);
                    }

                    m_WebSocket?.Dispose();
                    m_CancellationTokenSource?.Dispose();

                    // Only log if we had an actual connection (not during retry cleanup)
                    if (wasConnected)
                        InternalLog.Log("[WebSocketRelayClient] Disconnected from server");
                }
                catch (Exception ex)
                {
                    InternalLog.LogError($"[WebSocketRelayClient] Error during disposal: {ex.Message}");
                }

                m_Disposed = true;
                m_IsConnected = false;
            }
        }
    }

    /// <summary>
    /// WebSocket message format for communication
    /// </summary>
    [Serializable]
    class WebSocketMessage
    {
        public string type;
        public string id;
        public string clientId;
        public string message;
        public string timestamp;
    }
}
