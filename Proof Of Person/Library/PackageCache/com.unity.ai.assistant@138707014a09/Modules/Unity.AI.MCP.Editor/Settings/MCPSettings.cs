using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.AI.MCP.Editor.Models;
using Unity.AI.MCP.Editor.Constants;

namespace Unity.AI.MCP.Editor.Settings
{
    [Serializable]
    class ConnectionOriginPolicy
    {
        public bool allowed = true;
        public bool requiresApproval = true;
    }

    [Serializable]
    class ConnectionPolicies
    {
        public ConnectionOriginPolicy gateway = new() { allowed = true, requiresApproval = false };
        public ConnectionOriginPolicy direct = new() { allowed = false, requiresApproval = true };
    }

    [Serializable]
    class MCPSettings
    {
        // General settings
        public bool bridgeEnabled = true;
        public bool batchModeEnabled = true;
        public bool debugLogsEnabled;
        public string validationLevel = ToolDescriptions.ValidationLevels[1]; // Default to "standard"
        public bool processValidationEnabled = true;
        public ConnectionPolicies connectionPolicies = new();

        [SerializeField]
        List<MCPClientState> clientStates = new();

        [SerializeField]
        List<string> disabledTools = new();

        public void UpdateClientState(string clientName, McpStatus status, string message = "")
        {
            var existing = clientStates.FirstOrDefault(c => c.clientName == clientName);
            if (existing != null)
            {
                existing.status = status;
                existing.statusMessage = message;
                existing.lastUpdated = DateTime.Now;
            }
            else
            {
                clientStates.Add(new MCPClientState
                {
                    clientName = clientName,
                    status = status,
                    statusMessage = message,
                    lastUpdated = DateTime.Now
                });
            }
        }

        public MCPClientState GetClientState(string clientName)
        {
            for (int i = 0; i < clientStates.Count; i++)
            {
                if (clientStates[i].clientName == clientName)
                    return clientStates[i];
            }
            return null;
        }

        public bool IsToolEnabled(string toolName)
        {
            return !disabledTools.Contains(toolName);
        }

        public void SetToolEnabled(string toolName, bool enabled)
        {
            bool changed = false;

            if (enabled)
            {
                changed = disabledTools.Remove(toolName);
            }
            else
            {
                if (!disabledTools.Contains(toolName))
                {
                    disabledTools.Add(toolName);
                    changed = true;
                }
            }

            // Notify registry about tool availability change
            if (changed)
            {
                UnityEditor.EditorApplication.delayCall += () => {
                    ToolRegistry.McpToolRegistry.NotifyToolAvailabilityChanged(toolName);
                };
            }
        }
    }

    [Serializable]
    class MCPClientState
    {
        public string clientName;
        public McpStatus status = McpStatus.NotConfigured;
        public string statusMessage = "";
        public DateTime lastUpdated = DateTime.Now;
    }
}