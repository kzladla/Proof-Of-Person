using System;
using Newtonsoft.Json;

namespace Unity.AI.Assistant.Editor.Mcp.Transport.Models
{
    /// <summary>
    /// Project-scoped MCP configuration stored in .mcp/config.json
    /// </summary>
    [Serializable]
    class McpProjectConfig
    {
        [JsonProperty("enabled")]
        public bool Enabled;

        [JsonProperty("path")]
        public string Path = "";

        [JsonProperty("servers")]
        public McpServerEntry[] Servers = Array.Empty<McpServerEntry>();
    }
}
