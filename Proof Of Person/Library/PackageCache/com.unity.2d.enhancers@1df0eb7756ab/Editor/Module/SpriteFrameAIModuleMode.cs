using System;
using System.Collections.Generic;
using System.IO;
using Unity.U2D.AI.Editor.AIBridge;
using Unity.U2D.AI.Editor.AssetPostProcessor;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Events.UI;
using Unity.U2D.AI.Editor.Undo;
using Unity.U2D.AI.Editor.Overlay;
using UnityEditor.U2D.Common;
using UnityEditor.U2D.Sprites;
using Unity.U2D.AI.Editor.ImageManipulation;
using Unity.U2D.AI.Editor.Overlay.Unity.U2D.AI.Editor.Overlay;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.U2D.AI.Editor
{
    partial class SpriteFrameAIModule : SpriteEditorFrameModuleModeBase, IEventSender
    {
        bool m_ModuleModeActivated;
        UIEventBus m_UIEventBus = new();

        Texture2D m_WorkspaceBackgroundTexture;
        SpriteEditorModuleBase m_Module;

        IScenePreview m_ScenePreview;
        ISpriteAIUndo m_Undo;
        IAIModuleController m_AIModuleController;

        AIModuleMainCanvas m_WorkCanvas;

        IImageManipulationCanvas m_ImageManipulationCanvas;
        IAIPanel m_AIPanel;

        const string k_DefaultSpriteName = "AI Sliced";

        public UIEventBus uiEventBus => m_UIEventBus;

        // Called when mode is added to a module, This is called when the module is being activated
        protected override void OnAddToModuleInternal(SpriteEditorModuleBase moduleBase)
        {
            if (!Utilities.TryGetUriFromGeneratedAsset(spriteEditor.GetDataProvider<ITextureDataProvider>().texture, out var assetUri))
                Utilities.TryGetUriFromAsset(spriteEditor.GetDataProvider<ITextureDataProvider>().texture, out assetUri);

            var dataProvider = spriteEditor.GetDataProvider<ISpriteEditorDataProvider>();
            if (SpriteCustomDataProvider.HasDataProvider(dataProvider))
            {
                SpriteCustomDataProvider customDataProvider = new(dataProvider);
                if (customDataProvider.GetData("com.unity.2d.enhancers.customdata", out string data))
                {
                    var customData = JsonUtility.FromJson<SpriteEditorCustomData>(data);
                    if (customData.selectedArtifact != null)
                        assetUri = new Uri(customData.selectedArtifact);
                }
            }

            m_Module = module;
            var textureDataProvider = spriteEditor.GetDataProvider<ITextureDataProvider>();
            textureDataProvider.GetTextureActualWidthAndHeight(out var width, out var height);

            if (textureDataProvider.texture != null)
            {
                InitAIToolKit(textureDataProvider.texture, out m_AIPanel, out var generationResultOverlay, out var brushToolSettings, out m_ImageManipulationCanvas);

                m_WorkCanvas = new AIModuleMainCanvas(m_ImageManipulationCanvas.visualElement, m_AIPanel.visualElement);
                var dataStorage = new OperatorDoodleStorage(new TextureData()
                {
                    data = Utilities.GetBytesFromTexture2D(textureDataProvider.texture),
                    width = textureDataProvider.texture.width,
                    height = textureDataProvider.texture.height
                });

                m_Undo = new SpriteAIUndo(m_UIEventBus);
                dataStorage.SetTexture(null, Utilities.GetBytesFromTexture2D(textureDataProvider.texture), false);

                m_AIModuleController = new AIModuleController(m_AIPanel, generationResultOverlay, brushToolSettings,
                    m_ImageManipulationCanvas, m_UIEventBus, m_Undo, dataStorage);
                m_AIModuleController.SetOriginalTexture(textureDataProvider.texture, assetUri);

                m_AIModuleController.OnEnableEnhancers += OnEnableEnhancers;
                m_AIModuleController.OnSelectedImage += OnSelectedImage;

                m_ImageManipulationCanvas.OnMove += deltaPos => { spriteEditor.scrollPosition -= deltaPos; };
                m_ImageManipulationCanvas.OnWheel += deltaZoom => { spriteEditor.zoomLevel -= deltaZoom; };

                textureDataProvider.RegisterSourceTextureOverride(OverrideSourceTextureCallback);
            }

            m_WorkspaceBackgroundTexture = new Texture2D(1, 1, TextureFormat.RGBAHalf, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "com.unity.2d.enhancer m_WorkspaceBackgroundTexture"
            };
            m_WorkspaceBackgroundTexture.SetPixel(1, 1, new Color(0, 0, 0, 0));
            m_WorkspaceBackgroundTexture.Apply();

            m_ScenePreview = new ScenePreview(module.spriteEditor);
            m_AIModuleController.UpdateImageReferenceDoodles();
        }

        void OnEnableEnhancers(bool enabled)
        {
            if (enabled)
            {
                if (!m_ModuleModeActivated)
                {
                    m_ModuleModeActivated = true;
                    RequestModeToActivate(this);
                }
            }
            else
            {
                m_ModuleModeActivated = false;
                RequestModeToActivate(null);
            }

            m_AIModuleController.ShowOverlays = m_ModuleModeActivated;
        }

        // Called when mode is removed from a module, This is called when module is deactivated
        protected override void OnRemoveFromModuleInternal(SpriteEditorModuleBase moduleBase)
        {
            try
            {
                m_AIModuleController.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError("2D Enhancers failed to deinitialize.");
                Debug.LogException(e);
            }
            finally
            {
                spriteEditor.GetDataProvider<ITextureDataProvider>().UnregisterSourceTextureOverride(OverrideSourceTextureCallback);
                m_Undo?.Dispose();
                m_Undo = null;

                m_WorkspaceBackgroundTexture.SafeDestroy();
                m_WorkspaceBackgroundTexture = null;
                m_AIModuleController.ShowOverlays = false;
            }
        }

        // public UIEventBus uiEventBus => m_UIEventBus;

        // This is called when the mode is activated from the module,
        public override bool ActivateMode()
        {
            SetupSpriteEditor();
            if (!m_Module.spriteEditor.GetMainVisualContainer().Contains(m_WorkCanvas))
                m_Module.spriteEditor.GetMainVisualContainer().Add(m_WorkCanvas);

            m_AIModuleController.SelectOriginal();

            return true;
        }

        // This is called when the mode is deactivated within the module. Usually when another mode is taking over.
        public override void DeactivateMode()
        {
            m_ModuleModeActivated = false;
            RestoreSpriteEditor();
            if (m_Module.spriteEditor.GetMainVisualContainer().Contains(m_WorkCanvas))
                m_Module.spriteEditor.GetMainVisualContainer().Remove(m_WorkCanvas);
        }

        public bool aiModeEnabled { get; private set; }

        public bool moduleModeActivated => m_ModuleModeActivated;

        public override void DoMainGUI()
        {
            m_ImageManipulationCanvas.UpdateTransform(spriteEditor.scrollPosition, spriteEditor.zoomLevel);
        }

        public override bool ApplyModeData(bool apply, HashSet<Type> dataProviderTypes)
        {
            if (apply)
            {
                var overrideTexture = m_AIModuleController.OverrideTexture;
                if (overrideTexture != null)
                {
                    var dataProvider = spriteEditor.GetDataProvider<ISpriteEditorDataProvider>();
                    if (SpriteCustomDataProvider.HasDataProvider(dataProvider))
                    {
                        SpriteCustomDataProvider customDataProvider = new(dataProvider);
                        var data = new SpriteEditorCustomData()
                        {
                            selectedArtifact = overrideTexture.LocalPath
                        };
                        customDataProvider.SetData("com.unity.2d.enhancers.customdata", JsonUtility.ToJson(data));
                        dataProviderTypes.Add(typeof(SpriteCustomDataProvider));
                    }
                }
            }

            return apply;
        }

        public override bool CanBeActivated()
        {
            var dataProvider = spriteEditor.GetDataProvider<ISpriteEditorDataProvider>();
            return dataProvider != null && dataProvider.spriteImportMode != SpriteImportMode.None;
        }

        public override void DoToolbarGUI(Rect drawArea) { }

        public override void DoPostGUI() { }

        void InitAIToolKit(Texture2D texture, out IAIPanel iaiPanel, out IGenerationResultOverlay generationResultOverlay, out IBrushToolSettingsOverlay brushToolSettings, out IImageManipulationCanvas imageManipulationCanvas)
        {
            iaiPanel = spriteEditor.GetOverlay<AIPanel>(AIPanel.overlayId);
            generationResultOverlay = spriteEditor.GetOverlay<GenerationResultOverlay>(GenerationResultOverlay.overlayId);
            brushToolSettings = spriteEditor.GetOverlay<BrushToolSettingsOverlay>(BrushToolSettingsOverlay.overlayId);
            var targetObject = spriteEditor.GetDataProvider<ISpriteEditorDataProvider>().targetObject;

            var generationsEditorBridge = new AIEditorBridge(generationResultOverlay.visualElement, targetObject);
            generationResultOverlay.SetAIEditorBridge(generationsEditorBridge);

            var aiPanelEditorBridge = new AIEditorBridge(m_AIPanel.visualElement, targetObject);
            // This is to prevent the Generate window from opening after promoting the asset.
            aiPanelEditorBridge.SetPromoteNewAssetPostAction(_ => { });

            var aiControlTypeBridge = new AIControlTypeBridge();
            m_AIPanel.SetAIBridge(aiControlTypeBridge, aiPanelEditorBridge);

            imageManipulationCanvas = new ImageManipulationCanvas(m_UIEventBus, texture, new DoodlePadsContainer());

            aiModeEnabled = true;

            m_UIEventBus.SendEvent(new AIModeEnableEvent(this) { enable = aiModeEnabled });
        }

        void SetupSpriteEditor()
        {
            var size = m_ImageManipulationCanvas.size;
            SetupSpriteEditorWindowPreviewSpace(size.x, size.y);

            //we only support generating to the full texture
            spriteEditor.enableMouseMoveEvent = true;
            spriteEditor.spriteRects = new List<SpriteRect>();

            var texture = spriteEditor.GetDataProvider<ITextureDataProvider>().texture;
            if (!TryGetSpriteRect(spriteEditor, texture, out var spriteRect))
                spriteRect = GetDefaultRect(texture);
            m_ScenePreview.SetSpriteRect(texture, spriteRect);
            m_ScenePreview.Activate();
        }

        void SetupSpriteEditorWindowPreviewSpace(int width, int height)
        {
            if (m_ModuleModeActivated)
            {
                spriteEditor.SetPreviewTexture(m_WorkspaceBackgroundTexture, width, height);
                spriteEditor.ResetZoomAndScroll();
            }
        }

        void RestoreSpriteEditor()
        {
            if (spriteEditor.GetDataProvider<ISpriteEditorDataProvider>().targetObject != null)
            {
                var textureProvider = spriteEditor.GetDataProvider<ITextureDataProvider>();
                if (textureProvider != null)
                {
                    textureProvider.GetTextureActualWidthAndHeight(out var width, out var height);

                    var texture = textureProvider.previewTexture;
                    spriteEditor.SetPreviewTexture(texture, width, height);
                }
            }

            m_ScenePreview.Deactivate();
        }

        void OverrideSourceTextureCallback(string assetPath)
        {
            var overrideTexture = m_AIModuleController.OverrideTexture;
            if (overrideTexture != null)
            {
                var bytes = File.ReadAllBytes(overrideTexture.LocalPath);
                if (bytes.Length > 0)
                {
                    File.WriteAllBytes(assetPath, bytes);
                    TagAssetPostProcessor.AddAssetPathToTag(assetPath);
                }
            }
        }

        void OnSelectedImage(Texture2D texture)
        {
            m_ImageManipulationCanvas.SetOriginalTexture(texture);
            if (!TryGetSpriteRect(spriteEditor, texture, out var spriteRect))
                spriteRect = GetDefaultRect(texture);

            spriteEditor.GetDataProvider<ISpriteEditorDataProvider>().SetSpriteRects(new[] { spriteRect });

            m_ScenePreview.SetSpriteRect(texture, spriteRect);

            try
            {
                InternalEditorBridge.GenerateOutline(texture, spriteRect.rect, 0, 0, false, out var outlines);
                List<Vector2[]> paths = new List<Vector2[]>(outlines.Length);
                for (int j = 0; j < outlines.Length; ++j)
                {
                    paths.Add(outlines[j]);
                }

                spriteEditor.GetDataProvider<ISpriteOutlineDataProvider>().SetOutlines(spriteRect.spriteID, paths);

                InternalEditorBridge.GenerateOutline(texture, spriteRect.rect, 0.25f, 200, false, out outlines);
                paths = new List<Vector2[]>(outlines.Length);
                for (int j = 0; j < outlines.Length; ++j)
                {
                    paths.Add(outlines[j]);
                }

                spriteEditor.GetDataProvider<ISpritePhysicsOutlineDataProvider>().SetOutlines(spriteRect.spriteID, paths);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            spriteEditor.GetDataProvider<ITextureDataProvider>().OverrideTextures(texture, texture, texture.width, texture.height);

            SetupSpriteEditorWindowPreviewSpace(texture.width, texture.height);
            spriteEditor.SetDataModified();
            spriteEditor.RequestRepaint();
        }

        static bool TryGetSpriteRect(ISpriteEditor spriteEditor, Texture2D texture, out SpriteRect spriteRect)
        {
            spriteRect = null;
            var readable = Utilities.GetReadableTexture(texture);

            var frames = new List<Rect>(InternalSpriteUtility.GenerateAutomaticSpriteRectangles(readable, 4, 0));
            if (texture != readable)
                readable.SafeDestroy();

            if (frames.Count == 0)
                return false;

            var spriteRects = spriteEditor.GetDataProvider<ISpriteEditorDataProvider>().GetSpriteRects();
            if (frames.Count > 0)
            {
                var f = frames[0];
                for (int i = 1; spriteRects.Length > 0 && i < frames.Count; ++i)
                {
                    f.xMin = f.xMin > frames[i].xMin ? frames[i].xMin : f.xMin;
                    f.yMin = f.yMin > frames[i].yMin ? frames[i].yMin : f.yMin;
                    f.xMax = f.xMax < frames[i].xMax ? frames[i].xMax : f.xMax;
                    f.yMax = f.yMax < frames[i].yMax ? frames[i].yMax : f.yMax;
                }
            }

            spriteRect = (spriteRects.Length == 0 ? null : spriteRects[0]) ?? new SpriteRect { name = k_DefaultSpriteName };
            spriteRect.rect = frames[0];

            return true;
        }

        static SpriteRect GetDefaultRect(Texture2D texture)
        {
            return new SpriteRect { name = k_DefaultSpriteName, rect = new Rect(Vector2.zero, new Vector2(texture.width, texture.height)), };
        }
    }
}
