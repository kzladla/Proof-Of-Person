using System.Collections.Generic;
using Newtonsoft.Json;

namespace Unity.Relay.Editor.Acp
{
    /// <summary>
    /// Provider metadata coming from the relay (gateway/providers).
    /// </summary>
    class AcpProviderDescriptor
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        /// <summary>
        /// Optional version string for the provider SDK/CLI.
        /// </summary>
        [JsonProperty("version")]
        public string Version { get; set; }

        /// <summary>
        /// True if this is a custom/forked version of the upstream provider.
        /// </summary>
        [JsonProperty("isCustom")]
        public bool IsCustom { get; set; }

        /// <summary>
        /// Optional env var names (UI hints only).
        /// </summary>
        [JsonProperty("envVarNames")]
        public string[] EnvVarNames { get; set; }

        [JsonProperty("install")]
        public AcpProviderInstall Install { get; set; }

        [JsonProperty("postInstall")]
        public AcpPostInstallInfo PostInstall { get; set; }

        public AcpInstallStep GetInstallStep(string platform)
        {
            if (string.IsNullOrEmpty(platform))
                return null;

            if (Install?.Platforms == null)
                return null;

            return Install.Platforms.TryGetValue(platform, out var step) ? step : null;
        }
    }

    class AcpProviderInstall
    {
        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("platforms")]
        public Dictionary<string, AcpInstallStep> Platforms { get; set; }
    }

    class AcpInstallStep
    {
        [JsonProperty("display")]
        public string Display { get; set; }

        [JsonProperty("exec")]
        public AcpInstallExec Exec { get; set; }
    }

    class AcpInstallExec
    {
        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("args")]
        public string[] Args { get; set; }
    }

    /// <summary>
    /// Post-install information for API key configuration.
    /// </summary>
    class AcpPostInstallInfo
    {
        /// <summary>
        /// Rich text message with links (using &lt;a href="..."&gt; format).
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; }

        /// <summary>
        /// Primary environment variable name to set (e.g., "ANTHROPIC_API_KEY").
        /// </summary>
        [JsonProperty("envVarName")]
        public string EnvVarName { get; set; }
    }

    /// <summary>
    /// Version information for a provider (gateway/provider_versions).
    /// </summary>
    class AcpProviderVersionInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("isCustom")]
        public bool IsCustom { get; set; }
    }
}
