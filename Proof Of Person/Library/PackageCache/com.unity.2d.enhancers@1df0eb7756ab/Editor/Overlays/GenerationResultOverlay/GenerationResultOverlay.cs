using System;
using Unity.U2D.AI.Editor.AIBridge;
using Unity.U2D.AI.Editor.Styles;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.U2D.Sprites;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.Overlay
{
    internal interface IGenerationResultOverlay : IDisposable
    {
        event Action<Uri> OnSelectionChanged;
        bool requestShow { get; set; }
        void SetAIEditorBridge(IAIEditorBridge bridge);
        void SetGenerationSelection(Uri assetUri);
        VisualElement visualElement { get; }
    }

    [Overlay(typeof(ISpriteEditor), overlayId, nameof(GenerationResultOverlay),
        defaultLayout = Layout.Panel,
        maxHeight = 10000, defaultHeight = 400,
        maxWidth = 10000, defaultWidth = 300,
        minWidth = 110, minHeight = 110,
        defaultDockZone = DockZone.LeftColumn,
        defaultDockPosition = DockPosition.Bottom)]
    internal class GenerationResultOverlay : UnityEditor.Overlays.Overlay, ITransientOverlay, IGenerationResultOverlay
    {
        public const string overlayId = "com.unity.2d.enhancers/GenerationResultOverlay";
        const string k_Uxml = "Packages/com.unity.2d.enhancers/Editor/Overlays/GenerationResultOverlay/GenerationResultOverlay.uxml";

        public event Action<Uri> OnSelectionChanged;

        public VisualElement visualElement => m_Main;

        VisualElement m_Main;
        VisualElement m_Root;
        bool m_RequestShow;
        IAIEditorBridge m_ResultsEditorBridge;

        void CreateGenerateResultElement()
        {
            displayName = TextContent.generationResultOverlayDisplayName;

            m_Main = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).CloneTree();
            m_Main.tooltip = TextContent.generationResultOverlayTooltip;
            rootVisualElement.tooltip = TextContent.generationResultOverlayTooltip;
            StyleReference.AddSpriteEditorOverlayStyle(m_Main);
        }

        public void SetAIEditorBridge(IAIEditorBridge bridge)
        {
            m_ResultsEditorBridge?.Dispose();
            m_ResultsEditorBridge = bridge;

            m_ResultsEditorBridge.AddGenerationSelectionHandler(uri => OnSelectionChanged?.Invoke(uri));
        }

        public void SetGenerationSelection(Uri assetUri)
        {
            m_ResultsEditorBridge.SetGenerationSelection(assetUri);
        }

        public void Dispose()
        {
            m_ResultsEditorBridge?.Dispose();
        }

        public override void OnCreated()
        {
            CreateGenerateResultElement();
            displayed = false;

            m_Root = new VisualElement
            {
                style = { flexGrow = 1, flexShrink = 1 }
            };
            m_Root.Add(m_Main);
            displayName = TextContent.generationResultOverlayDisplayName;
        }

        public override void OnWillBeDestroyed()
        {
            Dispose();
            base.OnWillBeDestroyed();
        }

        public override VisualElement CreatePanelContent()
        {
            displayed = m_RequestShow;
            return m_Root;
        }

        public bool visible => m_RequestShow;

        public bool requestShow
        {
            get => m_RequestShow;
            set
            {
                m_RequestShow = value;
                displayed = value;
            }
        }
    }
}
