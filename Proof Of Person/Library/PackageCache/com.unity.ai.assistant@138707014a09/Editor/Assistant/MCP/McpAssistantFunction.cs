using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.ApplicationModels;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Mcp.Transport;
using Unity.AI.Assistant.Editor.Mcp.Transport.Models;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Editor.Mcp
{
    class McpAssistantFunction : ICachedFunction
    {
        McpServerEntry m_Server;
        McpTool m_Tool;
        IUnityMcpHttpClient m_Client;

        public FunctionDefinition FunctionDefinition { get; }

        public McpAssistantFunction(McpServerEntry server, McpTool tool, IUnityMcpHttpClient client)
        {
            m_Server = server ?? throw new ArgumentNullException(nameof(server));
            m_Tool = tool ?? throw new ArgumentNullException(nameof(tool));
            m_Client = client ?? throw new ArgumentNullException(nameof(client));

            var param = new List<ParameterDefinition>();

            if (tool.InputSchema is { Properties: not null })
            {
                foreach (var prop in tool.InputSchema.Properties)
                {
                    var properties = prop.Value as JObject;

                    if(properties is null)
                        continue;
                    
                    param.Add(new ParameterDefinition(
                        properties["description"]?.Value<string>() ?? "no parameter description provided",
                        prop.Key,
                        properties["type"]?.Value<string>() ?? "string",
                        tool.InputSchema.Properties
                    ));
                }
            }

            FunctionDefinition = new FunctionDefinition(tool.Description, tool.Name)
            {
                Namespace = "MCP",
                FunctionId = $"{server.Name}-mcp-" + tool.Name,
                Parameters = param,
                AssistantMode = AssistantMode.Any,
                Tags = new List<string>() { "mcp" }
            };
        }

        public async Task<object> InvokeAsync(ToolExecutionContext context)
        {
            InternalLog.Log($"Calling: {context.Call.FunctionId}");

            // Unwrap parameters that may be incorrectly wrapped by the LLM as {"value": actualValue}
            var parameters = UnwrapParameters(context.Call.Parameters);

            var res = await m_Client.CallMcpToolAsync(
                m_Server,
                m_Tool.Name,
                parameters);

            if (!res.IsSuccess)
                throw new Exception(res.ErrorMessage);

            return res.Content;
        }

        static JObject UnwrapParameters(JObject parameters)
        {
            if (parameters == null)
                return null;

            var result = new JObject();

            foreach (var property in parameters.Properties())
                result[property.Name] = UnwrapValue(property.Value);

            return result;
        }

        static JToken UnwrapValue(JToken value)
        {
            // If the value is a JObject with exactly one property named "value", extract it.
            // This handles the LLM bug where parameters are wrapped as {"value": actualValue}
            // instead of just actualValue.
            if (value is JObject obj && obj.Count == 1 && obj.TryGetValue("value", out var innerValue))
                return innerValue;

            return value;
        }
    }
}
