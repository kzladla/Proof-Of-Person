#if ASSISTANT_FEATURE_MCP_CLIENT
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Mcp.Manager;
using Unity.AI.Assistant.Editor.Service;
using UnityEditor;

namespace Unity.AI.Assistant.Editor.Mcp
{
    /// <summary>
    /// Handles MCP initialization when Unity starts
    /// </summary>
    static class McpInitializer
    {
        [InitializeOnLoadMethod]
        static async Task InitializeMcpServices()
        {
            await AssistantGlobal.Services.RegisterService(new McpServerManagerService());
        }
    }
}
#endif
