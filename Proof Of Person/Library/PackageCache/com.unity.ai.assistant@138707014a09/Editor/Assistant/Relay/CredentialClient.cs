using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Utils;
using Unity.Relay;
using UnityEditor;

namespace Unity.Relay.Editor
{
    /// <summary>
    /// Client for secure credential storage through the Relay server.
    /// Uses platform-native secure storage (macOS Keychain, Windows Credential Manager, Linux libsecret).
    ///
    /// This client only handles storing and deleting credentials. Reading is done by the relay
    /// when starting an agent session (based on the secureEnvVarNames list).
    /// </summary>
    class CredentialClient : IDisposable
    {
        static CredentialClient s_Instance;
        bool m_Disposed;

        /// <summary>
        /// Initialize CredentialClient on editor load to ensure it subscribes to relay events early.
        /// </summary>
        [InitializeOnLoadMethod]
        static void InitializeOnLoad()
        {
            // Access Instance to trigger construction and relay event subscription
            // This ensures migration runs when the relay connects
            EditorApplication.delayCall += () =>
            {
                var _ = Instance;
                InternalLog.Log("[CredentialClient] Initialized and subscribed to relay events");
            };
        }

        readonly System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<JObject>> m_PendingRequests = new();
        int m_RequestIdCounter;

        /// <summary>
        /// Gets the singleton instance of the CredentialClient.
        /// </summary>
        public static CredentialClient Instance
        {
            get
            {
                if (s_Instance == null || s_Instance.m_Disposed)
                {
                    s_Instance = new CredentialClient();
                }
                return s_Instance;
            }
        }

        /// <summary>
        /// Gets whether the client is connected to the relay.
        /// </summary>
        public bool IsConnected => RelayService.Instance.IsConnected;

        /// <summary>
        /// Cached availability status (null if not yet checked).
        /// </summary>
        bool? m_IsAvailable;

        CredentialClient()
        {
            SubscribeToRelayEvents();
        }

        void SubscribeToRelayEvents()
        {
            var client = RelayService.Instance.Client;
            if (client != null)
            {
                client.OnCredentialResponse += HandleCredentialResponse;
            }

            RelayService.Instance.Connected += OnRelayConnected;
            RelayService.Instance.Disconnected += OnRelayDisconnected;
        }

        void OnRelayConnected()
        {
            var client = RelayService.Instance.Client;
            if (client != null)
            {
                client.OnCredentialResponse += HandleCredentialResponse;
            }

            // Clear cached availability on reconnect
            m_IsAvailable = null;

            InternalLog.Log("[CredentialClient] Relay connected - checking for credential migration...");

            // Trigger migration
            EditorApplication.delayCall += () => _ = TryMigrateCredentialsAsync();
        }

        void OnRelayDisconnected()
        {
            var client = RelayService.Instance.Client;
            if (client != null)
            {
                client.OnCredentialResponse -= HandleCredentialResponse;
            }

            // Cancel all pending requests
            foreach (var tcs in m_PendingRequests.Values)
            {
                tcs.TrySetCanceled();
            }
            m_PendingRequests.Clear();
        }

        void HandleCredentialResponse(string json)
        {
            try
            {
                var msg = JObject.Parse(json);
                var requestId = msg["id"]?.ToString();

                if (!string.IsNullOrEmpty(requestId) && m_PendingRequests.TryRemove(requestId, out var tcs))
                {
                    tcs.TrySetResult(msg);
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[CredentialClient] Error handling response: {ex.Message}");
            }
        }

        string GenerateRequestId()
        {
            return $"cred-{Interlocked.Increment(ref m_RequestIdCounter)}";
        }

        async Task<JObject> SendRequestAsync(object message, string requestId, int timeoutMs = 10000)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to relay server");
            }

            var tcs = new TaskCompletionSource<JObject>();
            m_PendingRequests[requestId] = tcs;

            try
            {
                var json = JsonConvert.SerializeObject(message);
                var sent = await RelayService.Instance.Client.SendRawMessageAsync(json);

                if (!sent)
                {
                    throw new InvalidOperationException("Failed to send message to relay");
                }

                using var cts = new CancellationTokenSource(timeoutMs);
                await using (cts.Token.Register(() => tcs.TrySetCanceled()))
                {
                    return await tcs.Task;
                }
            }
            finally
            {
                m_PendingRequests.TryRemove(requestId, out _);
            }
        }

        /// <summary>
        /// Check if secure credential storage is available on this platform.
        /// Only caches positive results - failures will retry on next call.
        /// </summary>
        /// <returns>True if secure storage is available.</returns>
        public async Task<bool> IsAvailableAsync()
        {
            // Only use cache if we've confirmed availability (don't cache failures)
            if (m_IsAvailable == true)
                return true;

            if (!IsConnected)
                return false;

            try
            {
                var requestId = GenerateRequestId();
                var message = new
                {
                    type = RelayConstants.RELAY_CREDENTIAL_AVAILABLE,
                    id = requestId
                };

                var response = await SendRequestAsync(message, requestId);
                var available = response["available"]?.Value<bool>() ?? false;

                // Only cache positive results - allows retry if service wasn't ready
                if (available)
                    m_IsAvailable = true;

                return available;
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[CredentialClient] Failed to check availability: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Store a credential securely.
        /// </summary>
        /// <param name="agentType">The agent type (e.g., "claude-code", "gemini").</param>
        /// <param name="name">The credential name (e.g., "ANTHROPIC_API_KEY").</param>
        /// <param name="value">The credential value.</param>
        /// <returns>True if the credential was stored successfully.</returns>
        public async Task<bool> StoreAsync(string agentType, string name, string value)
        {
            if (!IsConnected)
            {
                InternalLog.LogWarning("[CredentialClient] Not connected to relay server");
                return false;
            }

            try
            {
                var requestId = GenerateRequestId();
                var message = new
                {
                    type = RelayConstants.RELAY_CREDENTIAL_STORE,
                    id = requestId,
                    agentType,
                    name,
                    value
                };

                var response = await SendRequestAsync(message, requestId);
                return response["success"]?.Value<bool>() ?? false;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[CredentialClient] Error storing credential: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete a credential.
        /// </summary>
        /// <param name="agentType">The agent type.</param>
        /// <param name="name">The credential name.</param>
        /// <returns>True if the credential was deleted.</returns>
        public async Task<bool> DeleteAsync(string agentType, string name)
        {
            if (!IsConnected)
            {
                InternalLog.LogWarning("[CredentialClient] Not connected to relay server");
                return false;
            }

            try
            {
                var requestId = GenerateRequestId();
                var message = new
                {
                    type = RelayConstants.RELAY_CREDENTIAL_DELETE,
                    id = requestId,
                    agentType,
                    name
                };

                var response = await SendRequestAsync(message, requestId);
                return response["success"]?.Value<bool>() ?? false;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[CredentialClient] Error deleting credential: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (m_Disposed) return;

            var client = RelayService.Instance.Client;
            if (client != null)
            {
                client.OnCredentialResponse -= HandleCredentialResponse;
            }

            RelayService.Instance.Connected -= OnRelayConnected;
            RelayService.Instance.Disconnected -= OnRelayDisconnected;

            // Cancel all pending requests
            foreach (var tcs in m_PendingRequests.Values)
            {
                tcs.TrySetCanceled();
            }
            m_PendingRequests.Clear();

            m_Disposed = true;
        }

        // ===== Migration Logic =====

        /// <summary>
        /// Migrate credentials from plaintext EditorPrefs to secure storage.
        /// This is a one-time migration that runs when the relay connects.
        /// </summary>
        async Task TryMigrateCredentialsAsync()
        {
            // Check if migration has already been done
            if (GatewayPreferences.IsSecureStorageMigrated())
            {
                return;
            }

            // Check if secure storage is available
            if (!await IsAvailableAsync())
            {
                InternalLog.LogWarning("[CredentialClient] Secure storage not available, skipping migration");
                return;
            }

            InternalLog.Log("[CredentialClient] Starting credential migration to secure storage...");

            var migratedCount = 0;

            // Migrate credentials for each known agent type
            foreach (var agentType in GatewayPreferences.AgentTypes.Keys)
            {
                try
                {
                    var migrated = await MigrateAgentCredentialsAsync(agentType);
                    migratedCount += migrated;
                }
                catch (Exception ex)
                {
                    InternalLog.LogError($"[CredentialClient] Failed to migrate credentials for {agentType}: {ex.Message}");
                }
            }

            // Mark migration as complete
            GatewayPreferences.SetSecureStorageMigrated(true);

            if (migratedCount > 0)
            {
                InternalLog.Log($"[CredentialClient] Migrated {migratedCount} credentials to secure storage");
            }
            else
            {
                InternalLog.Log("[CredentialClient] Credential migration complete (no credentials to migrate)");
            }
        }

        /// <summary>
        /// Migrate credentials for a specific agent type.
        /// Returns the number of credentials migrated.
        /// </summary>
        async Task<int> MigrateAgentCredentialsAsync(string agentType)
        {
            var envVars = GatewayPreferences.LoadEnvironmentVariablesList(agentType);
            if (envVars == null || envVars.Count == 0)
                return 0;

            var migratedCount = 0;
            var updatedVars = new List<GatewayPreferences.EnvVar>();

            foreach (var v in envVars.Where(v => !string.IsNullOrEmpty(v.Name)))
            {
                if (GatewayPreferences.IsSensitiveEnvVar(v.Name) && !string.IsNullOrEmpty(v.Value) &&
                    await StoreAsync(agentType, v.Name, v.Value))
                {
                    InternalLog.Log($"[CredentialClient] Migrated {agentType}/{v.Name} to secure storage");
                    migratedCount++;
                    updatedVars.Add(v with { Value = "", IsSecure = true });
                }
                else
                {
                    updatedVars.Add(v);
                }
            }

            if (migratedCount > 0)
                GatewayPreferences.SaveEnvironmentVariables(agentType, updatedVars);

            return migratedCount;
        }
    }
}
