using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Backend.Socket.Tools;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.AI.Assistant.Tools.Editor
{
    static class ProjectTool
    {
        const string k_GetProjectDataID = "Unity.GetProjectData";
        const string k_GetProjectOverviewID = "Unity.GetProjectOverview";
        const string k_SaveFileID = "Unity.SaveFile";
        
        [Serializable]
        public class GetProjectDataOutput
        {
            public string SceneInfo = string.Empty;
            public string ProjectDependency = string.Empty;
            public string ProjectSetting = string.Empty;
            public string ProjectVersion = string.Empty;
            public string ProjectTaxonomy = string.Empty;
        }

        // Tool ID "Unity.GetProjectData" is directly used by backend, do not change without backend update.
        [AgentTool("Returns basic project data to generate project overview markdown file.",
            k_GetProjectDataID,
            assistantMode: AssistantMode.Agent | AssistantMode.Ask,
            tags: FunctionCallingUtilities.k_ProjectOverviewTag,
            GatewayMcpAvailable = true
        )]
        public static async Task<GetProjectDataOutput> GetProjectData(
            ToolExecutionContext context,
            [Parameter("Optional: Specify the depth of the extracted scene hierarchy, all scene objects will be extracted if not specified.")]
            int maxSceneDepth = int.MaxValue
        )
        {
            var output = new GetProjectDataOutput();
            await context.Permissions.CheckFileSystemAccess(IToolPermissions.ItemOperation.Read, Application.dataPath);

            var sb = new StringBuilder();

            sb.AppendLine("# Scenes");

            // Scene 
            var scenes = EditorBuildSettings.scenes;
            foreach (var scene in scenes)
            {
                if (scene.enabled)
                {
                    sb.AppendLine($"The entry scene of the game is: {scene.path}.");
                    break;
                }
            }

            sb.Append("This Unity project contains the following scenes:");

            // All scenes in project
            string[] guids = AssetDatabase.FindAssets("t:Scene");
            for (var i = 0; i < guids.Length; i++)
            {
                string guid = guids[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                {
                    string hierarchy = GetSceneHierarchy(path, maxSceneDepth);

                    sb.AppendLine();
                    sb.AppendLine($"## Scene {i + 1}");
                    sb.AppendLine($"Scene name: {path}");
                    sb.AppendLine("Scene hierarchy:");
                    sb.Append(hierarchy);
                }
            }
            output.SceneInfo = sb.ToString();
            sb.Clear();

            // Dependencies
            var projectDependency = await GetUnityDependenciesTool.GetUnityDependencies();

            sb.AppendLine();
            sb.AppendLine("# Project Dependency");
            sb.AppendLine("These are the installed packages of the project:");

            foreach (var dependency in projectDependency)
                sb.AppendLine($"- {dependency.Key}: {dependency.Value}");
            output.ProjectDependency = sb.ToString();
            sb.Clear();

            // Static project settings
            sb.AppendLine();
            sb.AppendLine("# Project Setting");
            sb.AppendLine("These are the basic project settings:");

            var projectSettings = GetStaticProjectSettingsTool.GetStaticProjectSettings();
            foreach (var setting in projectSettings)
                sb.AppendLine($"- {setting.Key}: {setting.Value}");
            output.ProjectSetting = sb.ToString();
            sb.Clear();

            // Version
            sb.AppendLine();
            sb.AppendLine("# Project Version");
            sb.AppendLine($"The Unity project version is {GetUnityVersionTool.GetUnityVersion()}");
            output.ProjectVersion = sb.ToString();
            sb.Clear();

            // Folder hierarchy
            sb.AppendLine();
            sb.AppendLine("# Project Taxonomy / Folder Hierarchy is:");
            sb.Append(ProjectHierarchyExporter.GetFullAssetsHierarchyMarkdown());
            output.ProjectTaxonomy = sb.ToString();

            return output;
        }

        [AgentTool("Returns project overview markdown file content.",
            k_GetProjectOverviewID,
            assistantMode: AssistantMode.Agent | AssistantMode.Ask,
            tags: FunctionCallingUtilities.k_ProjectOverviewTag
        )]
        public static async Task<string> GetProjectOverview(ToolExecutionContext context)
        {
            var fullPath = Path.Combine(Application.dataPath, "Project_Overview.md");
            var result = "";

            await context.Permissions.CheckFileSystemAccess(IToolPermissions.ItemOperation.Read, fullPath);

            if (File.Exists(fullPath))
            {
                result = await File.ReadAllTextAsync(fullPath);
            }

            return result;
        }

        static string GetSceneHierarchy(string scenePath, int maxDepth)
        {
            if (string.IsNullOrEmpty(scenePath))
                throw new ArgumentException("Scene path cannot be empty.");

            var sceneHierarchy = SceneHierarchyExporter.GetFullSceneHierarchyMarkdown(scenePath, maxDepth);

            return sceneHierarchy;
        }

        [AgentTool(
            "Save text file (markdown, json etc.) with file content, if the file already exists, the content will be overwritten. File paths can be relative to Unity project root (e.g., \"Assets/Project_Overview.md\") or absolute.",
            k_SaveFileID,
            ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_ProjectOverviewTag)]
        public static async Task<string> SaveFile(
            ToolExecutionContext context,
            [Parameter("Path to the file to create. Can be relative to Unity project root (e.g., \"Assets/Project_Overview.md\") or absolute.")]
            string filePath,
            [Parameter("The entire content of the new file. Include proper whitespace, indentation, and ensure resulting code is correct.")]
            string fileContent,
            [Parameter("Text file type (i.e. markdown, json etc.) for syntax highlighting (defaults to 'markdown')")]
            string fileType = "markdown")
        {
            try
            {
                // Resolve file path (handle relative paths from Unity project root)
                var resolvedPath = ResolvePath(filePath);
                await context.Permissions.CheckFileSystemAccess(
                    File.Exists(resolvedPath)?
                        IToolPermissions.ItemOperation.Modify:
                        IToolPermissions.ItemOperation.Create,
                    resolvedPath
                );

                // Ensure parent directory exists for new files
                var directory = Path.GetDirectoryName(resolvedPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    await context.Permissions.CheckFileSystemAccess(
                        IToolPermissions.ItemOperation.Create,
                        directory
                    );
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(resolvedPath, fileContent);

                AssetDatabase.Refresh();

                string message = $"Successfully created file {resolvedPath}";
                return message;
            }
            catch (Exception ex)
            {
                InternalLog.LogException(ex);
                throw;
            }
        }

        static string ResolvePath(string filePath)
        {
            if (Path.IsPathRooted(filePath))
                return filePath;

            // Handle Unity project relative paths
            var projectPath = Directory.GetCurrentDirectory();
            return Path.Combine(projectPath, filePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    static class ProjectHierarchyExporter
    {
        public static string GetFullAssetsHierarchyMarkdown()
        {
            // Collect all assets under Assets/
            var guids = AssetDatabase.FindAssets("", new[] { "Assets" });
            var assetPaths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToList();

            // Prepare the hierarchy
            var hierarchy = new AssetTools.AssetHierarchy();
            var rootFolders = new Dictionary<string, AssetTools.AssetFolder>(StringComparer.OrdinalIgnoreCase);

            // Build folder tree + asset listings
            var processedPaths = new HashSet<string>();

            foreach (var path in assetPaths)
            {
                if (!processedPaths.Add(path))
                    continue;

                var parts = path.Split('/');
                if (parts.Length == 0)
                    continue;

                var rootName = parts[0]; // "Assets"

                if (!rootFolders.TryGetValue(rootName, out var rootFolder))
                {
                    rootFolder = new AssetTools.AssetFolder { Name = rootName };
                    rootFolders[rootName] = rootFolder;
                }

                // Build folder structure
                var folder = FileUtils.GetOrCreateFolder(rootFolder, parts, 1, path);

                if (AssetDatabase.IsValidFolder(path))
                    continue;

                var mainObject = AssetDatabase.LoadMainAssetAtPath(path);
                if (mainObject == null)
                    continue;

                var assetInfo = new AssetTools.AssetInfo
                {
                    MainAsset = new AssetTools.InstanceInfo
                    {
                        Name = Path.GetFileName(path),
                        Type = mainObject.GetType(),
                        InstanceID = mainObject.GetInstanceID()
                    }
                };

                folder.Assets.Add(assetInfo);

                var subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                if (subs == null || subs.Length == 0)
                    continue;

                foreach (var sub in subs)
                {
                    if (sub == null) continue;
                    assetInfo.SubAssets.Add(new AssetTools.InstanceInfo
                    {
                        Name = string.IsNullOrEmpty(sub.name) ? sub.GetType().Name : sub.name,
                        Type = sub.GetType(),
                        InstanceID = sub.GetInstanceID()
                    });
                }
            }

            hierarchy.Roots = rootFolders.Values.ToList();

            // Export using your Markdown exporter
            return AssetResultMarkdownExporter.ToMarkdownTree(hierarchy, includeID: false);
        }
    }

    static class SceneHierarchyExporter
    {
        static int s_MaxDepth;
        
        public static string GetFullSceneHierarchyMarkdown(string scenePath, int maxDepth)
        {
            s_MaxDepth = maxDepth;
            var hierarchy = BuildSceneHierarchy(scenePath);

            return SceneObjectResultMarkdownExporter.ToMarkdownTree(hierarchy, false, false);
        }

        static SceneTools.SceneHierarchy BuildSceneHierarchy(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                throw new ArgumentException("Scene path cannot be empty.", nameof(scenePath));

            if (!File.Exists(scenePath))
                throw new FileNotFoundException($"Scene file not found at path: {scenePath}", scenePath);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                throw new ArgumentException($"The file at '{scenePath}' is not a valid Unity scene asset.", nameof(scenePath));

            var existing = SceneManager.GetSceneByPath(scenePath);
            bool wasAlreadyLoaded = existing.IsValid() && existing.isLoaded;

            var scene = wasAlreadyLoaded ? existing : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException($"Scene '{scenePath}' failed to load.");

            try
            {
                var hierarchy = new SceneTools.SceneHierarchy();
                foreach (var rootGO in scene.GetRootGameObjects())
                {
                    if (rootGO == null)
                        continue; 
                    hierarchy.Roots.Add(BuildSceneObject(rootGO, 0));
                }
                return hierarchy;
            }
            finally
            {
                if (!wasAlreadyLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static SceneTools.SceneObject BuildSceneObject(GameObject go, int depth)
        {
            var obj = new SceneTools.SceneObject { Name = go.name, InstanceID = go.GetInstanceID(), };

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null)
                    continue;

                obj.Components.Add(new SceneTools.ComponentInfo
                {
                    Type = comp.GetType()
                });
            }

            if (depth < s_MaxDepth)
            {
                var childCount = go.transform.childCount;
                for (var i = 0; i < childCount; i++)
                {
                    var child = go.transform.GetChild(i).gameObject;
                    obj.Children.Add(BuildSceneObject(child, depth + 1));
                }
            }
            
            return obj;
        }
    }
}
