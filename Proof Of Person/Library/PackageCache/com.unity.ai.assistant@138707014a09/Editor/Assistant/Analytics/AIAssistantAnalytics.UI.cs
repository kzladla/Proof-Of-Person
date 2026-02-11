using System;
using UnityEditor;
using UnityEngine.Analytics;

namespace Unity.AI.Assistant.Editor.Analytics
{
    internal enum UITriggerBackendEventSubType
    {
        FavoriteConversation,
        DeleteConversation,
        RenameConversation,
        LoadConversation,
        EditCode,
        CancelRequest,
        CreateNewConversation,
        RefreshInspirationalPrompt
    }

    internal enum ContextSubType
    {
        ExpandContext,
        PingAttachedContextObjectFromFlyout,
        ClearAllAttachedContext,
        RemoveSingleAttachedContext,
        DragDropAttachedContext,
        ChooseContextFromFlyout
    }

    internal enum UITriggerLocalEventSubType
    {
        UseInspirationalPrompt,
        OpenShortcuts,
        ExecuteRunCommand,
        ChooseModeFromShortcut,
        OpenReferenceUrl,
        SaveCode,
        CopyCode,
        CopyResponse,
        ModifyRunCommandPreviewWithObjectPicker,
        ModifyRunCommandPreviewValue,
        ExpandCommandLogic,
        PermissionResponse
    }

    internal static partial class AIAssistantAnalytics
    {
        #region Remote UI Events

        internal const string k_UITriggerBackendEvent = "AIAssistantUITriggerBackendEvent";

        [Serializable]
        internal class UITriggerBackendEventData : IAnalytic.IData
        {
            public string SubType;
            public string ConversationId;
            public string MessageId;
            public string ResponseMessage;
            public string ConversationTitle;
            public string IsFavorite;
        }

        [AnalyticInfo(eventName: k_UITriggerBackendEvent, vendorKey: k_VendorKey)]
        class UITriggerBackendEvent : IAnalytic
        {
            private readonly UITriggerBackendEventData m_Data;

            public UITriggerBackendEvent(UITriggerBackendEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_Data;
                return true;
            }
        }

        internal static void ReportUITriggerBackendEvent(UITriggerBackendEventSubType subType, Action<UITriggerBackendEventData> enrich = null)
        {
            var data = new UITriggerBackendEventData
            {
                SubType = subType.ToString(),
            };
            enrich?.Invoke(data);

            EditorAnalytics.SendAnalytic(new UITriggerBackendEvent(data));
        }

        #endregion

        #region Context Events

        internal const string k_ContextEvent = "AIAssistantContextEvent";

        [Serializable]
        internal class ContextEventData : IAnalytic.IData
        {
            public string SubType;
            public string ContextContent;
            public string ContextType;
            public string IsSuccessful;
        }

        // Context Group
        [AnalyticInfo(eventName: k_ContextEvent, vendorKey: k_VendorKey)]
        class ContextEvent : IAnalytic
        {
            private readonly ContextEventData m_Data;

            public ContextEvent(ContextEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                data = m_Data;
                error = null;
                return true;
            }
        }

        internal static void ReportContextEvent(ContextSubType subType, Action<ContextEventData> enrich = null)
        {
            var data = new ContextEventData { SubType = subType.ToString() };
            enrich?.Invoke(data);
            EditorAnalytics.SendAnalytic(new ContextEvent(data));
        }

        #endregion

        #region Local UI Events

        internal const string k_UITriggerLocalEvent = "AIAssistantUITriggerLocalEvent";

        [Serializable]
        internal class UITriggerLocalEventData : IAnalytic.IData
        {
            public string SubType;
            public string UsedInspirationalPrompt;
            public string ChosenMode;
            public string ReferenceUrl;
            public string ConversationId;
            public string MessageId;
            public string ResponseMessage;
            public string PreviewParameter;
            public string FunctionId;
            public string UserAnswer;
            public string PermissionType;
        }

        [AnalyticInfo(eventName: k_UITriggerLocalEvent, vendorKey: k_VendorKey)]
        class UITriggerLocalEvent : IAnalytic
        {
            private readonly UITriggerLocalEventData m_Data;
            public UITriggerLocalEvent(UITriggerLocalEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                data = m_Data;
                error = null;
                return true;
            }
        }

        internal static void ReportUITriggerLocalEvent(UITriggerLocalEventSubType subType, Action<UITriggerLocalEventData> enrich = null)
        {
            var data = new UITriggerLocalEventData { SubType = subType.ToString() };
            enrich?.Invoke(data);
            EditorAnalytics.SendAnalytic(new UITriggerLocalEvent(data));
        }

        #endregion
    }
}
