using System;
using System.IO;
using Unity.AI.Assistant.Bridge;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Editor.Utils
{
    static class ChatContextEntryExtensions
    {
        public static void Activate(this AssistantContextEntry entry)
        {
            switch (entry.EntryType)
            {
                case AssistantContextType.Component:
                case AssistantContextType.HierarchyObject:
                case AssistantContextType.SceneObject:
                case AssistantContextType.SubAsset:
                {
                    var targetObject = entry.GetTargetObject();
                    if (targetObject != null)
                    {
                        Selection.activeObject = targetObject;
                        EditorGUIUtility.PingObject(targetObject);
                    }

                    break;
                }
                case AssistantContextType.ConsoleMessage:
                    ConsoleUtils.SelectConsoleLog(entry.GetLogData());
                    break;
            }
        }

        public static AssistantContextEntry GetContextEntry(this LogData logData)
        {
            var result = new AssistantContextEntry
            {
                DisplayValue = $"Console {logData.Type.ToString()}",
                Value = logData.Message,
                ValueType = logData.Type.ToString(),
                EntryType = AssistantContextType.ConsoleMessage
            };

            return result;
        }

        public static AssistantContextEntry GetContextEntry(this Object source)
        {
            if (AssetDatabase.Contains(source))
            {
                var entryType = AssistantContextType.HierarchyObject;
                var guid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(source)).ToString();
                var entryValue = guid;

                if (AssetDatabase.IsSubAsset(source) &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out guid, out var subAssetId))
                {
                    entryType = AssistantContextType.SubAsset;
                    entryValue = $"{guid}_{subAssetId}";
                }

                var result = new AssistantContextEntry
                {
                    DisplayValue = source.name,
                    Value = entryValue,
                    ValueType = source.GetType().FullName,
                    EntryType = entryType
                };

                return result;
            }

            if (source is Component component)
            {
                var result = new AssistantContextEntry
                {
                    DisplayValue = source.name,
                    Value = component.gameObject.GetObjectHierarchy(),
                    ValueType = component.GetType().FullName,
                    ValueIndex = component.GetComponentIndex(),
                    EntryType = AssistantContextType.Component
                };

                return result;
            }

            if (source is GameObject gameObject)
            {
                var result = new AssistantContextEntry
                {
                    DisplayValue = source.name,
                    Value = gameObject.GetObjectHierarchy(),
                    ValueType = source.GetType().FullName,
                    EntryType = AssistantContextType.SceneObject
                };

                return result;
            }

            throw new InvalidDataException("Source is not a valid Object for " + typeof(AssistantContextEntry));
        }

        private static GameObject GetGameObject(string objectHierarchy)
        {
            var parts = objectHierarchy.Split('\n');

            // Find the object by instance ID, if the hierarchy contained one after a unique linebreak separator
            if (parts.Length == 2)
            {
                if (!int.TryParse(parts[1], out var instanceId))
                {
                    throw new FormatException("Invalid instance ID format.");
                }

                return EditorUtility.EntityIdToObject(instanceId) as GameObject;
            }

            // Default to old format of `ContextEntry.Value` with hierarchy of scene object
            return GameObject.Find(objectHierarchy);
        }

        public static Component GetComponent(this AssistantContextEntry entry)
        {
            switch (entry.EntryType)
            {
                case AssistantContextType.Component:
                {
                    var host = GetGameObject(entry.Value);
                    if (host == null)
                    {
                        return null;
                    }

                    Component candidate = null;
                    var components = host.GetComponents<Component>();
                    for (var i = 0; i < components.Length; i++)
                    {
                        if (components[i] == null || components[i].GetType().FullName != entry.ValueType)
                        {
                            continue;
                        }

                        if (candidate == null)
                        {
                            candidate = components[i];
                        }

                        if (i == entry.ValueIndex)
                        {
                            // We found the exact component we want
                            candidate = components[i];
                            break;
                        }
                    }

                    return candidate;
                }

                default:
                {
                    throw new InvalidOperationException("Invalid Type for GetComponent: " + entry.EntryType);
                }
            }
        }

        public static LogData GetLogData(this AssistantContextEntry entry)
        {
            switch (entry.EntryType)
            {
                case AssistantContextType.ConsoleMessage:
                {
                    var result = new LogData
                    {
                        Message = entry.Value,
                        Type = Enum.Parse<LogDataType>(entry.ValueType)
                    };

                    return result;
                }

                default:
                {
                    throw new InvalidOperationException("Invalid Type for GetLogData: " + entry.EntryType);
                }
            }
        }

        public static Object GetTargetObject(this AssistantContextEntry entry)
        {
            switch (entry.EntryType)
            {
                case AssistantContextType.Component:
                case AssistantContextType.SceneObject:
                {
                    return GetGameObject(entry.Value);
                }

                case AssistantContextType.HierarchyObject:
                case AssistantContextType.SubAsset:
                {
                    var guid = entry.Value;
                    long subAssetId = 0;

                    if (entry.EntryType == AssistantContextType.SubAsset)
                    {
                        var splitIds = entry.Value.Split("_");
                        if (splitIds.Length > 1)
                        {
                            guid = splitIds[0];
                            long.TryParse(splitIds[1], out subAssetId);
                        }
                    }

                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        return null;
                    }

                    if (entry.EntryType == AssistantContextType.SubAsset)
                    {
                        var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                        foreach (var asset in allAssets)
                        {
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid,
                                out var localId);
                            if (localId == subAssetId)
                            {
                                return asset;
                            }
                        }
                    }

                    var type = typeof(Object);

                    // Try to use the correct type for the asset, if that does not work, fall back to Object:
                    if (AssetDatabase.GetImporterType(assetPath) == typeof(ModelImporter))
                    {
                        type = typeof(Mesh);
                    }

                    var result = AssetDatabase.LoadAssetAtPath(assetPath, type);
                    if (result == null)
                    {
                        result = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                    }

                    return result;
                }

                default:
                {
                    throw new InvalidOperationException("Invalid Type for GetTargetObject: " + entry.EntryType);
                }
            }
        }
    }
}
