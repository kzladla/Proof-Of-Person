using System;
using System.Text.RegularExpressions;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Backend.Socket.Tools;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    [FunctionCallRenderer(typeof(RunCommandTool), nameof(RunCommandTool.ExecuteCommand))]
    class RunCommandFunctionCallElement : VisualElement, IFunctionCallRenderer, IAssistantUIContextAware
    {
        const string k_FunctionDisplayName = "Run Command";
        
        public virtual string Title => k_FunctionDisplayName;
        public virtual string TitleDetails { get; private set; }
        public virtual bool Expanded => true;
        public AssistantUIContext Context { get; set; }

        // Regex patterns for log parsing
        static readonly Regex k_LogPrefixPattern = new Regex(@"^\[(Log|Warning|Error)\]\s*", RegexOptions.Compiled);
        static readonly Regex k_InstanceIdPattern = new Regex(@"\s*\[ID:-?\d+\]", RegexOptions.Compiled);
        
        static readonly Color k_WarningColor = new Color(1f, 0.92f, 0.016f); // Yellow
        static readonly Color k_ErrorColor = new Color(1f, 0.32f, 0.29f);    // Red
        
        Foldout m_CodeFoldout;
        Foldout m_ResultFoldout;
        CodeBlockElement m_CodeBlock;
        VisualElement m_LogsContainer;

        public void OnCallRequest(AssistantFunctionCall functionCall)
        {
            // Extract parameters
            var code = functionCall.Parameters?["code"]?.ToString();
            var title = functionCall.Parameters?["title"]?.ToString();

            // Set title details
            TitleDetails = !string.IsNullOrEmpty(title) ? $"({title})" : string.Empty;

            // Create code foldout
            m_CodeFoldout = new Foldout
            {
                text = "Code",
                value = false
            };
            m_CodeFoldout.AddToClassList("mui-run-command-code-foldout");
            Add(m_CodeFoldout);

            // Create and configure code block
            m_CodeBlock = new CodeBlockElement();
            m_CodeBlock.Initialize(Context);
            m_CodeBlock.SetCodeType("csharp");
            m_CodeBlock.ShowHeader(false);

            if (!string.IsNullOrEmpty(code))
            {
                m_CodeBlock.SetCode(code);
            }

            m_CodeFoldout.Add(m_CodeBlock);

            // Create result foldout
            m_ResultFoldout = new Foldout
            {
                text = "Output",
                value = true,
            };
            m_ResultFoldout.SetDisplay(false);
            m_ResultFoldout.AddToClassList("mui-run-command-result-foldout");
            Add(m_ResultFoldout);

            // Create container with code-block-like background
            var resultContainer = new VisualElement();
            resultContainer.AddToClassList("mui-code-block-content-root");
            resultContainer.AddToClassList("mui-execution-result-container");
            m_ResultFoldout.Add(resultContainer);

            // Create logs container inside result container
            m_LogsContainer = new VisualElement();
            m_LogsContainer.style.backgroundColor = StyleKeyword.None;
            resultContainer.Add(m_LogsContainer);
        }

        public void OnCallSuccess(string functionId, Guid callId, IFunctionCaller.CallResult result)
        {
            var typedResult = result.GetTypedResult<RunCommandTool.ExecutionOutput>();

            // Display execution logs if present
            if (!string.IsNullOrEmpty(typedResult.ExecutionLogs))
                DisplayFormattedLogs(typedResult.ExecutionLogs);
        }

        public void OnCallError(string functionId, Guid callId, string error)
        {
            if (!string.IsNullOrEmpty(error))
                DisplayFormattedLogs(error);
        }        
        
        void DisplayFormattedLogs(string logs)
        {
            m_ResultFoldout.SetDisplay(true);
            m_LogsContainer.Clear();
            
            var lines = logs.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var (displayText, logType) = ParseLogLine(line);
                
                var label = new Label(displayText);
                label.AddToClassList("mui-execution-log-label");
                
                // Apply color based on log type
                switch (logType)
                {
                    case LogType.Warning:
                        label.style.color = k_WarningColor;
                        break;
                    case LogType.Error:
                        label.style.color = k_ErrorColor;
                        break;
                }
                
                m_LogsContainer.Add(label);
            }
        }
        
        (string displayText, LogType logType) ParseLogLine(string line)
        {
            var logType = LogType.Log;
            var displayText = line;
            
            // Extract log type from prefix [Log], [Warning], [Error]
            var prefixMatch = k_LogPrefixPattern.Match(line);
            if (prefixMatch.Success)
            {
                var typeStr = prefixMatch.Groups[1].Value;
                logType = typeStr switch
                {
                    "Warning" => LogType.Warning,
                    "Error" => LogType.Error,
                    _ => LogType.Log
                };
                
                // Remove the prefix from display
                displayText = line.Substring(prefixMatch.Length);
            }
            
            // Remove Instance ID pattern [ID:...]
            displayText = k_InstanceIdPattern.Replace(displayText, string.Empty);
            
            return (displayText.Trim(), logType);
        }
    }
}
