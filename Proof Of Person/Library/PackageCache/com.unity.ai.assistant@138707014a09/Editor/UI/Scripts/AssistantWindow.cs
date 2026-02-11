using System;
using System.IO;
using System.Reflection;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Backend.Socket;
using Unity.AI.Assistant.Editor.Config;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    class AssistantWindow : EditorWindow, IAssistantHostWindow, IHasCustomMenu
    {
        const string k_WindowName = "Assistant";

        static Vector2 s_MinSize = new(400, 400);

        internal IAssistantProvider AssistantInstance => m_AssistantInstance;
        Assistant.Editor.Assistant m_AssistantInstance;

        internal AssistantUIContext m_Context;
        internal AssistantView m_View;
        AssistantWindowUiContainer m_AssistantWindowUiContainer;

        static IAssistantBackend s_InternalBackendOverride = null;

        public Action FocusLost { get; set; }

        [MenuItem("Window/AI/Assistant")]
        public static AssistantWindow ShowWindow()
        {
            var editor = GetWindow<AssistantWindow>();

            editor.Show();
            editor.minSize = s_MinSize;

            return editor;
        }

        void CreateGUI()
        {
            var iconPath =
                EditorGUIUtility.isProSkin
                    ? "Sparkle.png"
                    : "Sparkle_dark.png";

            var path = Path.Combine(AssistantUIConstants.BasePath, AssistantUIConstants.UIEditorPath,
                AssistantUIConstants.AssetFolder, "icons", iconPath);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            titleContent = new GUIContent(k_WindowName, icon);

            var configuration = new AssistantConfiguration(
                backend: s_InternalBackendOverride ?? new AssistantRelayBackend());

            s_InternalBackendOverride = null;

            m_AssistantInstance = new Assistant.Editor.Assistant(configuration);

            // Create and initialize a context for this window, will be unique for every active set of assistant UI / elements
            m_Context = new AssistantUIContext(AssistantInstance);

            m_Context.WindowDockingState = () => docked;

            m_View = new AssistantView(this);

            m_View.Initialize(m_Context);
            m_View.style.flexGrow = 1;
            m_View.style.minWidth = s_MinSize.x;
            rootVisualElement.Add(m_View);

            m_View.InitializeThemeAndStyle();
            m_View.InitializeState();

            // Initialize the view to be used to display user interactions
            m_AssistantWindowUiContainer = new AssistantWindowUiContainer(m_View);
            var permissionPolicy = new SettingsPermissionsPolicyProvider();

            // TODO:
            // The only reason this cannot be configured in the constructor is because the EditorToolPermissions needs
            // an AssistantUIContext which needs a Assistant which needs a ToolInteractionAndPermissionBridge which
            // needs a EditorToolPermissions ...
            configuration.Bridge = new ToolInteractionAndPermissionBridge(
                new EditorToolPermissions(m_Context, m_AssistantWindowUiContainer, permissionPolicy),
                new ToolInteractions(m_AssistantWindowUiContainer));

            m_AssistantInstance.Reconfigure(configuration);
        }

        internal void InternalConfigureBackend(IAssistantBackend backend)
        {
            s_InternalBackendOverride = backend;

            Close();
            CreateWindow<AssistantWindow>();
        }

        void OnDestroy()
        {
            m_View?.Deinit();
            m_AssistantWindowUiContainer?.Dispose();
            m_AssistantWindowUiContainer = null;

            // TODO: https://jira.unity3d.com/browse/ASST-2178
            m_AssistantInstance?.Backend?.ActiveWorkflow?.Dispose();
            m_AssistantInstance = null;
        }

        void OnLostFocus()
        {
            FocusLost?.Invoke();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
#if ASSISTANT_DEV_TOOLS_PRESENT
            menu.AddItem(new GUIContent("Assistant Dev Tools"), false, () =>
            {
                var devWindowType = Type.GetType("Unity.AI.Assistant.DeveloperTools.AssistantDevelopmentWindow, Unity.AI.Assistant.DeveloperTools");
                devWindowType?.GetMethod("ShowWindow", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            });
#endif
        }
    }
}
