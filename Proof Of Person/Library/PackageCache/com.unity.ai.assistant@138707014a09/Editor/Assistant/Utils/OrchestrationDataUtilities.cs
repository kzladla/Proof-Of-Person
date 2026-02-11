using System.Collections.Generic;
using Unity.AI.Assistant.ApplicationModels;
using Unity.AI.Assistant.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Editor.Context;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Editor.Utils
{
    static class OrchestrationDataUtilities
    {
        internal static List<ChatRequestV1.AttachedContextModel> FromEditorContextReport(
            EditorContextReport editorContextReport)
        {
            var contextList = new List<ChatRequestV1.AttachedContextModel>();

            if (editorContextReport?.AttachedContext == null)
                return contextList;

            // Go through each context item
            foreach (var contextItem in editorContextReport.AttachedContext)
            {
                var contextModel = new ChatRequestV1.AttachedContextModel();
                var metaDataModel = new ChatRequestV1.AttachedContextModel.MetadataModel();
                ChatRequestV1.AttachedContextModel.BodyModel bodyModel = null;

                var selection = contextItem.Context as IContextSelection;
                if (selection == null)
                {
                    InternalLog.LogWarning("Context is not an IContextSelection.");
                    continue;
                }

                // There is technically two more of these, ContextSelection and StaticDatabase
                // They don't show up in these scenarios
                switch (selection)
                {
                    case UnityObjectContextSelection objectContext:
                    {
                        var contextEntry = objectContext.Target.GetContextEntry();

                        metaDataModel.DisplayValue = contextEntry.DisplayValue;
                        metaDataModel.Value = contextEntry.Value;
                        metaDataModel.ValueType = contextEntry.ValueType;
                        metaDataModel.ValueIndex = contextEntry.ValueIndex;
                        metaDataModel.EntryType = (int)contextEntry.EntryType;

                        break;
                    }

                    case VirtualContextSelection virtualContext:
                    {
                        if (virtualContext.Metadata is ImageContextMetaData imageContextMetaData)
                        {
                            bodyModel = new ChatRequestV1.AttachedContextModel.ImageBodyModel
                            {
                                Category = imageContextMetaData.Category.ToString(),
                                Format = imageContextMetaData.Format,
                                Width = imageContextMetaData.Width,
                                Height = imageContextMetaData.Height,
                                ImageContent = contextItem.Payload,
                                Payload = "",
                            };
                        }

                        metaDataModel.DisplayValue = virtualContext.DisplayValue;
                        metaDataModel.Value = "";
                        metaDataModel.ValueType = virtualContext.PayloadType;
                        metaDataModel.ValueIndex = -1;
                        metaDataModel.EntryType = (int)AssistantContextType.Virtual;

                        break;
                    }

                    case ConsoleContextSelection consoleContext:
                    {
                        var contextEntry = consoleContext.Target.GetValueOrDefault().GetContextEntry();

                        metaDataModel.DisplayValue = contextEntry.DisplayValue;
                        metaDataModel.Value = contextEntry.Value;
                        metaDataModel.ValueType = contextEntry.ValueType;
                        metaDataModel.ValueIndex = contextEntry.ValueIndex;
                        metaDataModel.EntryType = (int)contextEntry.EntryType;

                        break;
                    }

                    default:
                    {
                        InternalLog.LogWarning("Context is not attached object or console - skipping.");
                        continue;
                    }
                }

                if (bodyModel == null)
                {
                    // No specific body model has been made, use the default one
                    bodyModel = new ChatRequestV1.AttachedContextModel.TextBodyModel
                    {
                        Payload = contextItem.Payload,
                        Truncated = contextItem.Truncated
                    };
                }

                contextModel.Body = bodyModel;
                contextModel.Metadata = metaDataModel;
                contextList.Add(contextModel);
            }

            return contextList;
        }
    }
}
