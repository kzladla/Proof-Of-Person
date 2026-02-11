using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Relay.Editor;
using Unity.Relay.Editor.Acp;
using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// Information about an agent type for the Gateway settings.
    /// </summary>
    class GatewayAgentTypeInfo
    {
        public string ProviderId { get; set; }
        public string DisplayName { get; set; }
        public string[] EnvVarPresets { get; set; }
        public string CliPathEnvVar { get; set; }
        public string HelpText { get; set; }
    }

    /// <summary>
    /// Manages Gateway preferences for ACP agent configuration.
    /// Sensitive data (API keys) is stored securely in the system keychain via the Relay.
    /// EditorPrefs stores the env var list with IsSecure=true for secure entries.
    /// The Relay looks up actual secure values from keytar when starting an agent.
    /// </summary>
    static class GatewayPreferences
    {
        const string k_SelectedAgentTypeKey = "Unity.AI.Assistant.Gateway.SelectedAgentType";
        const string k_EnvVarsPrefsPrefix = "Unity.AI.Assistant.Gateway.EnvVars.";

        /// <summary>
        /// Environment variable names that contain sensitive data (API keys).
        /// These should be stored in secure storage, not EditorPrefs.
        /// </summary>
        static readonly HashSet<string> s_SensitiveEnvVars = new(StringComparer.OrdinalIgnoreCase)
        {
            AcpConstants.EnvVar_AnthropicApiKey,
            AcpConstants.EnvVar_OpenAiApiKey,
            AcpConstants.EnvVar_GeminiApiKey,
            AcpConstants.EnvVar_CursorApiKey
        };

        /// <summary>
        /// Check if an environment variable name is sensitive (e.g., API key).
        /// </summary>
        public static bool IsSensitiveEnvVar(string name)
        {
            return s_SensitiveEnvVars.Contains(name) ||
                   name.EndsWith("_API_KEY", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith("_SECRET", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith("_TOKEN", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Available agent types for the Gateway.
        /// </summary>
        public static readonly Dictionary<string, GatewayAgentTypeInfo> AgentTypes = new()
        {
            {
                AcpConstants.ProviderId_ClaudeCode, new GatewayAgentTypeInfo
                {
                    ProviderId = AcpConstants.ProviderId_ClaudeCode,
                    DisplayName = AcpConstants.ProviderName_ClaudeCode,
                    EnvVarPresets = new[] { AcpConstants.EnvVar_AnthropicApiKey, "ANTHROPIC_BASE_URL" },
                    CliPathEnvVar = AcpConstants.EnvVar_ClaudeCodeExecutable,
                    HelpText = $"Requires {AcpConstants.EnvVar_AnthropicApiKey}. Uses @anthropics/claude-code SDK."
                }
            },
            {
                AcpConstants.ProviderId_Codex, new GatewayAgentTypeInfo
                {
                    ProviderId = AcpConstants.ProviderId_Codex,
                    DisplayName = AcpConstants.ProviderName_Codex,
                    EnvVarPresets = new[] { AcpConstants.EnvVar_OpenAiApiKey },
                    CliPathEnvVar = AcpConstants.EnvVar_CodexCliPath,
                    HelpText = $"Requires {AcpConstants.EnvVar_OpenAiApiKey}. Uses OpenAI Codex CLI."
                }
            },
            {
                AcpConstants.ProviderId_Gemini, new GatewayAgentTypeInfo
                {
                    ProviderId = AcpConstants.ProviderId_Gemini,
                    DisplayName = "Gemini CLI",
                    EnvVarPresets = new[] { AcpConstants.EnvVar_GeminiApiKey },
                    CliPathEnvVar = AcpConstants.EnvVar_GeminiCliPath,
                    HelpText = $"Requires {AcpConstants.EnvVar_GeminiApiKey}. Uses gemini CLI."
                }
            },
            {
                AcpConstants.ProviderId_Cursor, new GatewayAgentTypeInfo
                {
                    ProviderId = AcpConstants.ProviderId_Cursor,
                    DisplayName = AcpConstants.ProviderName_Cursor,
                    EnvVarPresets = new[] { AcpConstants.EnvVar_CursorApiKey },
                    CliPathEnvVar = AcpConstants.EnvVar_CursorCliPath,
                    HelpText = $"Set {AcpConstants.EnvVar_CursorApiKey} or run cursor-agent login."
                }
            }
        };

        internal static event Action SelectedAgentTypeChanged;
        internal static event Action<string> EnvironmentVariablesChanged;

        /// <summary>
        /// The currently selected agent type (e.g., "claude-code", "codex", "gemini", "cursor").
        /// </summary>
        public static string SelectedAgentType
        {
            get => EditorPrefs.GetString(k_SelectedAgentTypeKey, AcpConstants.DefaultProviderId);
            set
            {
                if (SelectedAgentType != value)
                {
                    EditorPrefs.SetString(k_SelectedAgentTypeKey, value);
                    SelectedAgentTypeChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Get the EditorPrefs key for environment variables of a given agent type.
        /// </summary>
        static string GetEnvVarsPrefsKey(string agentType) => k_EnvVarsPrefsPrefix + agentType;

        /// <summary>
        /// Placeholder value returned for secure entries in LoadEnvironmentVariables.
        /// Used to indicate a value exists in secure storage without exposing the actual value.
        /// </summary>
        public const string SecurePlaceholder = "<secure>";

        /// <summary>
        /// Load environment variables for an agent type as a dictionary.
        /// For secure entries (IsSecure=true), returns a placeholder value.
        /// The relay looks up actual secure values from keytar when starting an agent.
        /// Falls back to system environment for missing values.
        /// </summary>
        public static Dictionary<string, string> LoadEnvironmentVariables(string agentType, IEnumerable<string> envVarNamesHint = null)
        {
            var env = new Dictionary<string, string>();

            // Load from EditorPrefs
            var json = EditorPrefs.GetString(GetEnvVarsPrefsKey(agentType), "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var wrapper = JsonConvert.DeserializeObject<EnvVarList>(json);
                    foreach (var envVar in wrapper?.Vars ?? Enumerable.Empty<EnvVar>())
                    {
                        if (!string.IsNullOrEmpty(envVar.Name))
                            env[envVar.Name] = envVar.IsSecure ? SecurePlaceholder : envVar.Value;
                    }
                }
                catch
                {
                    // Ignore parse errors, will use defaults
                }
            }

            // Fill in missing vars from system environment using hints or defaults
            var names = envVarNamesHint?.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();
            if (names == null || names.Length == 0)
                names = GetDefaultEnvVarNames(agentType);

            foreach (var varName in names)
            {
                if (!env.ContainsKey(varName) || string.IsNullOrEmpty(env[varName]))
                {
                    var sysValue = Environment.GetEnvironmentVariable(varName);
                    if (!string.IsNullOrEmpty(sysValue))
                        env[varName] = sysValue;
                }
            }

            return env;
        }

        /// <summary>
        /// Load environment variables as a list (for UI binding).
        /// </summary>
        public static List<EnvVar> LoadEnvironmentVariablesList(string agentType)
        {
            var json = EditorPrefs.GetString(GetEnvVarsPrefsKey(agentType), "");
            if (string.IsNullOrEmpty(json))
                return CreateDefaultEnvVars(agentType);

            try
            {
                return JsonConvert.DeserializeObject<EnvVarList>(json)?.Vars ?? CreateDefaultEnvVars(agentType);
            }
            catch
            {
                return CreateDefaultEnvVars(agentType);
            }
        }

        /// <summary>
        /// Save environment variables for an agent type.
        /// </summary>
        public static void SaveEnvironmentVariables(string agentType, List<EnvVar> vars)
        {
            EditorPrefs.SetString(GetEnvVarsPrefsKey(agentType), JsonConvert.SerializeObject(new EnvVarList(vars)));
            EnvironmentVariablesChanged?.Invoke(agentType);
        }

        /// <summary>
        /// Get default environment variable names for an agent type.
        /// </summary>
        public static string[] GetDefaultEnvVarNames(string agentType)
        {
            if (AgentTypes.TryGetValue(agentType, out var info))
                return info.EnvVarPresets ?? Array.Empty<string>();
            return Array.Empty<string>();
        }

        /// <summary>
        /// Create initial environment variable list for an agent type, pre-populated from system environment.
        /// </summary>
        public static List<EnvVar> CreateDefaultEnvVars(string agentType) =>
            GetDefaultEnvVarNames(agentType)
                .Select(name => new EnvVar(name, Environment.GetEnvironmentVariable(name) ?? ""))
                .ToList();

        /// <summary>
        /// Get the CLI path environment variable name for an agent type.
        /// </summary>
        public static string GetCliPathEnvVarName(string agentType)
        {
            if (AgentTypes.TryGetValue(agentType, out var info))
                return info.CliPathEnvVar;
            return null;
        }

        /// <summary>
        /// Represents an environment variable with optional secure storage.
        /// If IsSecure is true, the actual value is stored in the system keychain
        /// and the Value field will be empty - the relay fetches the real value from keytar.
        /// </summary>
        public record EnvVar(string Name, string Value = "", bool IsSecure = false);

        record EnvVarList(List<EnvVar> Vars);

        // ===== Secure Credential Storage =====
        // Secure values are stored in the system keychain via the relay.
        // EditorPrefs stores the env var list with IsSecure=true for secure entries (Value is empty).
        // The relay looks up actual values from keytar when starting an agent session.

        /// <summary>
        /// Save environment variables, storing sensitive values securely in the system keychain.
        /// Waits for secure storage to succeed before clearing values from EditorPrefs.
        /// </summary>
        public static async Task SaveEnvironmentVariablesWithSecureStorageAsync(string agentType, List<EnvVar> vars)
        {
            var validVars = vars.Where(v => !string.IsNullOrEmpty(v.Name)).ToList();

            // Track which vars were successfully stored securely
            var securelyStored = new HashSet<string>();

            // Attempt to store sensitive values in keychain
            if (CredentialClient.Instance.IsConnected)
            {
                var storeTasksWithNames = validVars
                    .Where(v => IsSensitiveEnvVar(v.Name) && !string.IsNullOrEmpty(v.Value))
                    .Select(async v =>
                    {
                        var success = await CredentialClient.Instance.StoreAsync(agentType, v.Name, v.Value);
                        return (v.Name, success);
                    })
                    .ToList();

                var results = await Task.WhenAll(storeTasksWithNames);
                foreach (var (name, success) in results)
                {
                    if (success)
                        securelyStored.Add(name);
                }
            }

            // Build list for EditorPrefs:
            // - Only clear value if secure storage succeeded
            // - Keep value in EditorPrefs as backup if storage failed (will be migrated later)
            var varsToSave = validVars
                .Select(v =>
                {
                    if (securelyStored.Contains(v.Name))
                        return v with { Value = "", IsSecure = true };
                    if (v.IsSecure && IsSensitiveEnvVar(v.Name))
                        return v with { Value = "", IsSecure = true }; // Already secure, keep it that way
                    return v with { IsSecure = false };
                })
                .ToList();

            EditorPrefs.SetString(GetEnvVarsPrefsKey(agentType), JsonConvert.SerializeObject(new EnvVarList(varsToSave)));
            EnvironmentVariablesChanged?.Invoke(agentType);
        }

        /// <summary>
        /// Save environment variables, storing sensitive values securely in the system keychain.
        /// Fire-and-forget version for UI callbacks - secure storage happens in background.
        /// </summary>
        public static void SaveEnvironmentVariablesWithSecureStorage(string agentType, List<EnvVar> vars)
        {
            _ = SaveEnvironmentVariablesWithSecureStorageAsync(agentType, vars);
        }

        /// <summary>
        /// Delete a secure credential from the keychain.
        /// </summary>
        public static void DeleteSecureCredential(string agentType, string name)
        {
            if (CredentialClient.Instance.IsConnected)
            {
                _ = CredentialClient.Instance.DeleteAsync(agentType, name);
            }
        }

        // ===== Migration Tracking =====

        const string k_SecureStorageMigratedKey = "Unity.AI.Assistant.Gateway.SecureStorageMigrated";

        /// <summary>
        /// Check if migration to secure storage has been completed.
        /// </summary>
        public static bool IsSecureStorageMigrated()
        {
            return EditorPrefs.GetBool(k_SecureStorageMigratedKey, false);
        }

        /// <summary>
        /// Mark secure storage migration as completed.
        /// </summary>
        public static void SetSecureStorageMigrated(bool migrated)
        {
            EditorPrefs.SetBool(k_SecureStorageMigratedKey, migrated);
        }
    }
}
