using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Unity.AI.Image.Interfaces;
using UnityEngine;

namespace Unity.U2D.AI.Editor.AIBridge
{
    interface IAIEditorBridge : IDisposable
    {
        VisualElement visualElement { get; }
        void EnableReplaceBlankAsset(bool enable);
        void EnableReplaceRefinementAsset(bool enable);
        void SetAIMode(EAIMode mode);
        void SetGenerationSelection(Uri uri);
        void SetPromoteNewAssetPostAction(Action<string> onPostPromote);
        void AddAIModeHandler(Action<EAIMode> onAIModeChange);
        void AddGenerationTriggeredHandler(Action onGenerate);
        void AddGenerationSelectionHandler(Action<Uri> onResultSelectUri);
    }

    internal class AIEditorBridge : IAIEditorBridge
    {
        List<Func<bool>> m_UnsubscribeEventHandlers;

        readonly VisualElement k_VisualElement;

        public AIEditorBridge(VisualElement visualElement, Object targetObject)
        {
            m_UnsubscribeEventHandlers = new List<Func<bool>>();

            k_VisualElement = visualElement;

            SetContext(k_VisualElement, targetObject);
        }

        public VisualElement visualElement => k_VisualElement;

        public void Dispose()
        {
            if (m_UnsubscribeEventHandlers == null)
                return;

            foreach (var eventHandler in m_UnsubscribeEventHandlers)
                eventHandler?.Invoke();

            m_UnsubscribeEventHandlers.Clear();
        }

        public void EnableReplaceBlankAsset(bool enable)
        {
            SpriteEditor.EnableReplaceBlankAsset(visualElement, enable);
        }

        public void EnableReplaceRefinementAsset(bool enable)
        {
            SpriteEditor.EnableReplaceRefinementAsset(visualElement, enable);
        }

        public void SetAIMode(EAIMode mode)
        {
            var newMode = (EquaptableAIMode)mode;
            visualElement.SetAIMode(newMode);
        }

        public void SetGenerationSelection(Uri uri)
        {
            if (uri != null && uri.IsFile && File.Exists(uri.LocalPath))
                visualElement.SetGenerationSelection(uri);
        }

        public void SetPromoteNewAssetPostAction(Action<string> onPostPromote)
        {
            visualElement.SetPromoteNewAssetPostAction(onPostPromote);
        }

        public void AddAIModeHandler(Action<EAIMode> onAIModeChange)
        {
            void Handler(AIMode aiMode)
            {
                var newAIMode = aiMode.ToEAIMode();
                onAIModeChange?.Invoke(newAIMode);
            }

            m_UnsubscribeEventHandlers.Add(visualElement.AddAIModeHandler(Handler));
        }

        public void AddGenerationTriggeredHandler(Action onGenerate)
        {
            m_UnsubscribeEventHandlers.Add(visualElement.AddGenerationTriggeredHandler(onGenerate));
        }

        public void AddGenerationSelectionHandler(Action<Uri> onResultSelectUri)
        {
            m_UnsubscribeEventHandlers.Add(visualElement.AddGenerationSelectionHandler(onResultSelectUri));
        }

        public static void SetContext(VisualElement ve, Object targetObject)
        {
            ve.SetObjectContext(targetObject);
        }
    }
}
