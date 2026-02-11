#if OBJECT_SELECTOR_TOOLBAR_DECORATOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit
{
    static class ObjectSelectorUtils
    {
        internal class ObjectSelectorTypeInfos
        {
            public Type            objectSelectorType { get; }
            public EventInfo       objectSelectorShownEvent { get; }
            public PropertyInfo    objectSelectorAllowedTypes { get; }
            public MethodInfo      objectSelectorSetSelection { get; }
            public MethodInfo      objectSelectorGetCurrentObject { get; }

            public ObjectSelectorTypeInfos()
            {
                objectSelectorType = typeof(UnityEditor.Editor).Assembly.GetType(k_ObjectSelectorClassName);
                if (objectSelectorType == null)
                    throw new InvalidOperationException($"Failed to find {k_ObjectSelectorClassName} class in assemblies.");
                objectSelectorShownEvent =
                    objectSelectorType.GetEvent(k_ObjectSelectorShownEventName);
                if (objectSelectorShownEvent == null)
                    throw new InvalidOperationException(
                        $"Failed to find {k_ObjectSelectorClassName}.{k_ObjectSelectorShownEventName} event field.");
                objectSelectorAllowedTypes =
                    objectSelectorType.GetProperty(k_ObjectSelectorAllowedTypesPropertyName);
                if (objectSelectorAllowedTypes == null)
                    throw new InvalidOperationException(
                        $"Failed to find {k_ObjectSelectorClassName}.{k_ObjectSelectorAllowedTypesPropertyName} property.");
                objectSelectorSetSelection =
                    objectSelectorType.GetMethod(k_ObjectSelectorSetSelectionMethodName);
                if (objectSelectorSetSelection == null)
                    throw new InvalidOperationException(
                        $"Failed to find {k_ObjectSelectorClassName}.{k_ObjectSelectorSetSelectionMethodName} method.");
                objectSelectorGetCurrentObject =
                    objectSelectorType.GetMethod("GetCurrentObject",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (objectSelectorGetCurrentObject == null)
                    throw new InvalidOperationException(
                        $"Failed to find {k_ObjectSelectorClassName}.GetCurrentObject method.");
            }
        }

        const string k_ObjectSelectorClassName = "UnityEditor.ObjectSelector";

        const string k_ObjectSelectorShownEventName = "shown";

        const string k_ObjectSelectorAllowedTypesPropertyName = "allowedTypes";

        const string k_ObjectSelectorSetSelectionMethodName = "SetSelection";

        const string k_SkipHiddenPackagesToggleName = "unity-object-selector__skip-hidden-packages-toggle";

        const string k_AdvancedObjectSelectorFirstRightElement = "ResultViewButtonContainer";

        internal const string classicObjectSelector = "Classic";

        internal const string advancedObjectSelector = "Advanced";

        static ObjectSelectorTypeInfos s_TypeInfo;

        internal static ObjectSelectorTypeInfos typeInfo
        {
            get
            {
                if (s_TypeInfo == null)
                {
                    try
                    {
                        s_TypeInfo = new ObjectSelectorTypeInfos();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                return s_TypeInfo;
            }
        }

        internal static void SetupShownEventHandler(Action<EditorWindow> shownHandler)
        {
            try
            {
                typeInfo.objectSelectorShownEvent.AddEventHandler(null, shownHandler);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to setup ObjectSelector shown event handler: {e.Message}");
            }
        }

        internal static void RemoveShownEventHandler(Action<EditorWindow> shownHandler)
        {
            try
            {
                typeInfo.objectSelectorShownEvent.RemoveEventHandler(null, shownHandler);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to remove ObjectSelector shown event handler: {e.Message}");
            }
        }

        internal static VisualElement GetTargetElement(EditorWindow window)
        {
            var element = window.rootVisualElement.Q<ToolbarToggle>(k_SkipHiddenPackagesToggleName);
            if (element == null)
            {
                // Classic object selector element not found.
                // Try to find the Advanced Object Selector element.
                return window.rootVisualElement.Q<VisualElement>(k_AdvancedObjectSelectorFirstRightElement);
            }
            return element;
        }

        internal static void SetSelection(int instanceID)
        {
#if UNITY_6000_4_OR_NEWER
            // Unity 6.4 and above use EntityId parameter
            typeInfo.objectSelectorSetSelection.Invoke(null, new object[] { (EntityId)instanceID });
#else
            // Unity 6000.3 and below use int parameter
            typeInfo.objectSelectorSetSelection.Invoke(null, new object[] { instanceID });
#endif
        }

        internal static Type[] GetAllowedTypes()
        {
            return (Type[]) typeInfo.objectSelectorAllowedTypes.GetValue(null);
        }
    }
}
#endif // OBJECT_SELECTOR_TOOLBAR_DECORATOR
