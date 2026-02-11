using System;
using System.Collections.Generic;
using Unity.AI.Image.Interfaces;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.U2D.AI.Editor.AIBridge
{
    interface IAIControlTypeBridge : IDisposable
    {
        EAIMode GetMode(EControlType controlType);
        void SetImageReferenceAsSelected(EControlType controlType, VisualElement imageControlCtx, bool isSelected);
        VisualElement GetImageReferenceElement(EControlType controlType, VisualElement aiPanel);
        VisualElement GetDoodlePadElement(EControlType controlType, VisualElement ctx);
        Button GetDoodlePadEditButton(EControlType controlType, VisualElement ctx);
        ObjectField GetObjectFieldElement(EControlType controlType, VisualElement ctx);
        void AddUnsavedAssetBytesChangedHandler(EControlType controlType, VisualElement ctx, Action<byte[]> handler);
        void AddDoodleDataChangedHandler(EControlType controlType, VisualElement ctx, Action<byte[]> handler);
        void AddIsActiveChangedHandler(EControlType controlType, VisualElement ctx, Action<bool> handler);
        byte[] GetDoodlePadData(EControlType controlType, VisualElement visualElement);
        byte[] GetUnsavedAssetBytes(EControlType controlType, VisualElement visualElement);
        bool IsActive(EControlType controlType, VisualElement visualElement);
        void SetDoodlePadData(EControlType controlType, VisualElement visualElement, byte[] data);
        void SetUnsavedAssetBytes(EControlType controlType, VisualElement visualElement, byte[] data);
    }

    internal class AIControlTypeBridge : IAIControlTypeBridge
    {
        List<Func<bool>> m_UnsubscribeEventHandlers = new();

        public void Dispose()
        {
            if (m_UnsubscribeEventHandlers == null)
                return;

            foreach (var eventHandler in m_UnsubscribeEventHandlers)
                eventHandler?.Invoke();

            m_UnsubscribeEventHandlers.Clear();
        }

        public EAIMode GetMode(EControlType controlType)
        {
            var internalControlType = controlType.ToControlType();
            var mode = internalControlType.GetMode();
            return mode.ToEAIMode();
        }

        public void SetImageReferenceAsSelected(EControlType controlType, VisualElement imageControlCtx, bool isSelected)
        {
            var internalControlType = controlType.ToControlType();
            internalControlType.SetImageReferenceAsSelected(imageControlCtx, isSelected);
        }

        public VisualElement GetImageReferenceElement(EControlType controlType, VisualElement aiPanel)
        {
            var internalControlType = controlType.ToControlType();
            return internalControlType.GetImageReferenceElement(aiPanel);
        }

        public VisualElement GetDoodlePadElement(EControlType controlType, VisualElement ctx)
        {
            var internalControlType = controlType.ToControlType();
            return internalControlType.GetDoodlePadElement(ctx);
        }

        public Button GetDoodlePadEditButton(EControlType controlType, VisualElement ctx)
        {
            var internalControlType = controlType.ToControlType();
            return internalControlType.GetDoodlePadEditButton(ctx);
        }

        public ObjectField GetObjectFieldElement(EControlType controlType, VisualElement ctx)
        {
            var internalControlType = controlType.ToControlType();
            return internalControlType.GetObjectFieldElement(ctx);
        }

        public void AddUnsavedAssetBytesChangedHandler(EControlType controlType, VisualElement ctx, Action<byte[]> handler)
        {
            var internalControlType = controlType.ToControlType();
            m_UnsubscribeEventHandlers.Add(internalControlType.AddUnsavedAssetBytesChangedHandler(ctx, handler));
        }

        public void AddDoodleDataChangedHandler(EControlType controlType, VisualElement ctx, Action<byte[]> handler)
        {
            var internalControlType = controlType.ToControlType();
            m_UnsubscribeEventHandlers.Add(internalControlType.AddDoodleDataChangedHandler(ctx, handler));
        }

        public void AddIsActiveChangedHandler(EControlType controlType, VisualElement ctx, Action<bool> handler)
        {
            var internalControlType = controlType.ToControlType();
            m_UnsubscribeEventHandlers.Add(internalControlType.AddIsActiveChangedHandler(ctx, handler));
        }

        public byte[] GetDoodlePadData(EControlType controlType, VisualElement visualElement)
        {
            var internalControlType = controlType.ToControlType();
            return internalControlType.GetDoodlePadData(visualElement);
        }

        public byte[] GetUnsavedAssetBytes(EControlType controlType, VisualElement visualElement)
        {
            var internalControlType = controlType.ToControlType();
            return internalControlType.GetUnsavedAssetBytes(visualElement);
        }

        public bool IsActive(EControlType controlType, VisualElement visualElement)
        {
            var internalControlType = controlType.ToControlType();
            return internalControlType.IsActive(visualElement);
        }

        public void SetDoodlePadData(EControlType controlType, VisualElement visualElement, byte[] data)
        {
            var internalControlType = controlType.ToControlType();
            internalControlType.SetDoodlePadData(visualElement, data);
        }

        public void SetUnsavedAssetBytes(EControlType controlType, VisualElement visualElement, byte[] data)
        {
            var internalControlType = controlType.ToControlType();
            internalControlType.SetUnsavedAssetBytes(visualElement, data);
        }
    }
}
