using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Data;

namespace Unity.Relay.Editor.Acp
{
    /// <summary>
    /// Configuration for starting an ACP agent session.
    /// </summary>
    class AcpSessionConfig
    {
        /// <summary>
        /// Unique identifier for this agent session.
        /// </summary>
        public AssistantConversationId SessionId { get; set; }

        /// <summary>
        /// Agent type to use (providerId), e.g. "claude-code", "gemini".
        /// </summary>
        public string AgentType { get; set; } = AcpConstants.DefaultProviderId;

        /// <summary>
        /// Command to execute (used for subprocess agents like gemini).
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Command line arguments.
        /// </summary>
        public string[] Args { get; set; }

        /// <summary>
        /// Working directory for the agent process.
        /// </summary>
        public string WorkingDir { get; set; }

        /// <summary>
        /// Environment variables. For secure entries (API keys), the value may be empty
        /// and the actual value is stored in the system keychain.
        /// </summary>
        public Dictionary<string, string> Env { get; set; }

        /// <summary>
        /// Names of environment variables whose values are stored securely in the system keychain.
        /// The relay will look up these values from keytar before starting the agent.
        /// </summary>
        public List<string> SecureEnvVarNames { get; set; }

        /// <summary>
        /// Agent's session ID for resuming a previous session.
        /// </summary>
        public string ResumeSessionId { get; set; }

    }
}
