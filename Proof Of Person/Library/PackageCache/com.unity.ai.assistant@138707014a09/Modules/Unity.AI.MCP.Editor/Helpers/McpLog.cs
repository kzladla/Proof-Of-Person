using System.Diagnostics;
using Unity.AI.MCP.Editor.Settings;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.AI.MCP.Editor.Helpers
{
    static class McpLog
    {
        const string Prefix = "<b><color=#2EA3FF>UNITY-MCP</color></b>:";

        static bool IsDebugEnabled()
        {
            try { return MCPSettingsManager.Settings.debugLogsEnabled; } catch { return false; }
        }

        public static void Log(string message)
        {
            if (!IsDebugEnabled()) return;
            Debug.Log($"{Prefix} {message}");
        }

        public static void Warning(string message)
        {
            if (!IsDebugEnabled()) return;
            Debug.LogWarning($"<color=#cc7a00>{Prefix} {message}</color>");
        }

        public static void Error(string message)
        {
            if (!IsDebugEnabled()) return;
            Debug.LogError($"<color=#cc3333>{Prefix} {message}</color>");
        }

        /// <summary>
        /// Log from a background thread - delays execution to main thread via EditorApplication.delayCall
        /// </summary>
        public static void LogDelayed(string message, LogType logType = LogType.Log)
        {
            // Capture stack trace at call site before deferring to main thread
            var stackTrace = new StackTrace(1, true);
            EditorApplication.delayCall += () =>
            {
                var messageWithStack = $"{message}\n{stackTrace}";
                switch (logType)
                {
                    case LogType.Warning:
                        Warning(messageWithStack);
                        break;
                    case LogType.Error:
                        Error(messageWithStack);
                        break;
                    default:
                        Log(messageWithStack);
                        break;
                }
            };
        }
    }
}
