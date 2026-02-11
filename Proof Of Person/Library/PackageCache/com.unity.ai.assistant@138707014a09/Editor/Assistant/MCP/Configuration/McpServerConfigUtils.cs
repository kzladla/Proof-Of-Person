using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.AI.Assistant.Editor.Mcp.Transport.Models;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.Mcp.Configuration
{
    /// <summary>
    /// Manager for project-based MCP configuration
    /// </summary>
    static class McpServerConfigUtils
    {
        static readonly string k_ConfigDirectory = "UserSettings";
        static readonly string k_ConfigFileName = "mcp.json";

        /// <summary>
        /// Prefix character that hides a server from the UI.
        /// Servers with names starting with this character are not loaded.
        /// </summary>
        const char k_HiddenServerPrefix = '~';

        /// <summary>
        /// Name of the example server that demonstrates JSON syntax.
        /// The ~ prefix hides it from the UI.
        /// </summary>
        const string k_ExampleServerName = "~ExampleServer (prefix server name with ~ to hide)";

        /// <summary>
        /// Get the full path to the project's MCP config directory
        /// </summary>
        static string GetConfigDirectoryPath()
        {
            return Path.Combine(Application.dataPath, "..", k_ConfigDirectory);
        }

        /// <summary>
        /// Get the full path to the project's MCP config file
        /// </summary>
        public static string GetConfigFilePath()
        {
            return Path.Combine(GetConfigDirectoryPath(), k_ConfigFileName);
        }

        /// <summary>
        /// Check if project has MCP config file
        /// </summary>
        public static bool HasConfigFile()
        {
            return File.Exists(GetConfigFilePath());
        }

        /// <summary>
        /// Load project config with fallback to default settings.
        /// Automatically deduplicates server names if duplicates are found.
        /// Returns a result indicating success/failure with error details.
        /// </summary>
        public static ConfigLoadResult<McpProjectConfig> LoadConfig()
        {
            var result = McpConfigFileHelper.LoadConfig(GetConfigFilePath(), CreateDefaultConfig);

            if (!result.Success)
                return result;

            if (DeduplicateServerNames(result.Config.Servers))
            {
                // Duplicates were found and fixed - save the corrected config
                SaveConfig(result.Config);
                InternalLog.Log("[MCP] Duplicate server names detected and automatically renamed", LogFilter.McpClient);
            }

            return result;
        }

        /// <summary>
        /// Checks if a server should be hidden from the UI.
        /// Servers with names starting with ~ are hidden.
        /// </summary>
        public static bool IsHiddenServer(McpServerEntry server)
        {
            return !string.IsNullOrEmpty(server?.Name) && server.Name[0] == k_HiddenServerPrefix;
        }

        /// <summary>
        /// Save project config with fallback to default settings
        /// </summary>
        public static void SaveConfig(McpProjectConfig config)
        {
            McpConfigFileHelper.SaveConfig(GetConfigFilePath(), config);
        }

        /// <summary>
        /// Create default project config with an example server to demonstrate JSON syntax.
        /// The example server is filtered out when loading, so it won't appear in the UI.
        /// </summary>
        public static McpProjectConfig CreateDefaultConfig()
        {
            var config = new McpProjectConfig();

            // Add an example server to demonstrate the JSON syntax to users.
            // This server is filtered out when loading and won't appear in the UI.
            config.Servers = new[]
            {
                new McpServerEntry()
                {
                    Name = k_ExampleServerName,
                    Command = "your-mcp-server-command",
                    Args = new[] { "--your-arg", "value" },
                    Environment = new Dictionary<string, string>
                    {
                        { "EXAMPLE_VAR", "example_value" }
                    },
                    Transport = "stdio"
                }
            };

            return config;
        }

        /// <summary>
        /// Checks for duplicate server names and renames them by appending a number suffix.
        /// </summary>
        /// <param name="servers">The array of server entries to check</param>
        /// <returns>True if any duplicates were found and renamed, false otherwise</returns>
        static bool DeduplicateServerNames(McpServerEntry[] servers)
        {
            if (servers == null || servers.Length <= 1)
                return false;

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var madeChanges = false;

            foreach (var server in servers)
            {
                if (string.IsNullOrEmpty(server.Name))
                {
                    server.Name = "UnnamedServer";
                    madeChanges = true;
                }

                var originalName = server.Name;

                if (usedNames.Contains(server.Name))
                {
                    // Find a unique name by appending a number
                    var counter = 1;
                    string newName;
                    do
                    {
                        newName = $"{originalName}_{counter}";
                        counter++;
                    } while (usedNames.Contains(newName));

                    server.Name = newName;
                    madeChanges = true;
                }

                usedNames.Add(server.Name);
            }

            return madeChanges;
        }

        /// <summary>
        /// Open the config file in the system's default editor
        /// </summary>
        public static void OpenConfigFileInEditor()
        {
            string configPath = GetConfigFilePath();

            if (!File.Exists(configPath))
                SaveConfig(CreateDefaultConfig());

            try
            {
                // Normalize path to avoid issues with "../" references
                var normalizedPath = Path.GetFullPath(configPath);

                // Try to open in system default editor first
                var processInfo = new ProcessStartInfo
                {
                    FileName = normalizedPath,
                    UseShellExecute = true
                };
                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"Failed to open config file in default editor: {ex.Message}");

                // Fallback: reveal in file explorer with normalized path
                var normalizedPath = Path.GetFullPath(configPath);
                EditorUtility.RevealInFinder(normalizedPath);
            }
        }

    }
}
