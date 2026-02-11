using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using Unity.AI.MCP.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.MCP.Editor.Helpers
{
    /// <summary>
    /// Previously managed installation of MCP server files to centralized user location.
    /// Currently disabled - relay binaries are now bundled directly in RelayApp~/.
    /// Legacy code kept for potential future use with relay binary updates.
    /// </summary>
    [InitializeOnLoad]
    static class ServerInstaller
    {
        static ServerInstaller()
        {
            // Disabled: Relay binaries are now bundled directly in Packages/com.unity.ai.assistant/RelayApp~/
            // TODO: Port this functionality to handle relay binary updates if needed
            // InstallOrUpdateServer();
        }

        /// <summary>
        /// [Currently unused - Investigate Porting]
        /// Install or update the MCP server to the centralized location.
        /// This was used when the MCP server was a separate component installed to ~/.unity/mcp/.
        /// Now relay binaries are bundled in RelayApp~/, but this code may be useful for
        /// implementing relay binary updates in the future.
        /// </summary>
        static void InstallOrUpdateServer()
        {
            try
            {
                // Legacy: These constants no longer exist
                // string packageServerPath = Path.GetFullPath(MCPConstants.serverPath);
                // string packageReleasePath = Path.Combine(packageServerPath, "release");
                // string packageJsonPath = Path.Combine(packageServerPath, MCPConstants.serverPackageJson);

                // New approach would use:
                string relayAppPath = Path.GetFullPath(MCPConstants.relayAppPath);

                // Verify relay binaries exist
                if (!Directory.Exists(relayAppPath))
                {
                    McpLog.Warning($"Relay app directory not found at {relayAppPath}");
                    return;
                }

                // For future: Could implement version checking and updates here
                // Currently relay binaries are bundled and don't need installation

                McpLog.Log($"Relay binaries available at {relayAppPath}");
            }
            catch (Exception ex)
            {
                McpLog.Warning($"Could not verify relay installation: {ex.Message}");
            }
        }

        /// <summary>
        /// [Currently unused - Investigate Porting]
        /// Copy platform-specific files from release directory to installation directory.
        /// Kept for potential future use with relay binary updates.
        /// </summary>
        static void CopyReleaseFiles(string sourceDir, string targetDir)
        {
            bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(filePath);

                // Skip system files
                if (fileName == ".DS_Store")
                    continue;

                // Platform-specific relay executables
                if (fileName == "relay_win.exe")
                {
                    if (isWindows)
                        CopyFile(filePath, targetDir, fileName);
                    continue;
                }

                if (fileName == "relay_linux")
                {
                    if (isLinux)
                        CopyFile(filePath, targetDir, fileName);
                    continue;
                }

                // Mac app bundles
                if (fileName.StartsWith("relay_mac_") && fileName.EndsWith(".app"))
                {
                    if (isMac)
                        CopyDirectory(filePath, Path.Combine(targetDir, fileName));
                    continue;
                }
            }
        }

        /// <summary>
        /// [Currently unused - Investigate Porting]
        /// Copy a single file with logging.
        /// </summary>
        static void CopyFile(string sourcePath, string targetDir, string fileName)
        {
            string targetPath = Path.Combine(targetDir, fileName);
            File.Copy(sourcePath, targetPath, true);
            McpLog.Log($"Copied {fileName} to {targetDir}");
        }

        /// <summary>
        /// [Currently unused - Investigate Porting]
        /// Copy a directory recursively (for app bundles).
        /// </summary>
        static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, true);

            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(targetDir, fileName), true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(targetDir, dirName));
            }
        }

        /// <summary>
        /// [Currently unused - Investigate Porting]
        /// Extract Mac app bundle from zip using ditto to preserve code signing.
        /// </summary>
        static void ExtractMacAppBundle(string zipPath, string targetDir)
        {
            string appBundleName = "unity_mcp.app";
            string targetAppPath = Path.Combine(targetDir, appBundleName);

            try
            {
                // Remove existing app bundle if present
                if (Directory.Exists(targetAppPath))
                {
                    Directory.Delete(targetAppPath, true);
                    McpLog.Log($"Removed existing {appBundleName}");
                }

                // Use ditto to extract zip while preserving code signing
                // ditto -xk <source.zip> <destination>
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ditto",
                    Arguments = $"-xk \"{zipPath}\" \"{targetDir}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        throw new Exception("Failed to start ditto process");
                    }

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        throw new Exception($"ditto failed with exit code {process.ExitCode}: {error}");
                    }

                    McpLog.Log($"Extracted {appBundleName} using ditto");
                }

                // Verify the app bundle was created
                if (!Directory.Exists(targetAppPath))
                {
                    throw new Exception($"App bundle not found at {targetAppPath} after extraction");
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"Failed to extract Mac app bundle: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// [Currently unused - Investigate Porting]
        /// Read version string from package.json.
        /// </summary>
        static string ReadVersion(string packageJsonPath)
        {
            string json = File.ReadAllText(packageJsonPath);
            var jsonObj = JObject.Parse(json);
            return jsonObj["version"]?.ToString() ?? "0.0.0";
        }

        /// <summary>
        /// [Currently unused - Investigate Porting]
        /// Compare semantic versions (e.g., "0.1.0" vs "0.2.0").
        /// Returns true if packageVersion is newer than installedVersion.
        /// </summary>
        static bool IsNewerVersion(string packageVersion, string installedVersion)
        {
            try
            {
                var pkgVersion = new Version(packageVersion);
                var instVersion = new Version(installedVersion);

                return pkgVersion > instVersion;
            }
            catch
            {
                // If parsing fails, assume we need to update
                return true;
            }
        }
    }
}
