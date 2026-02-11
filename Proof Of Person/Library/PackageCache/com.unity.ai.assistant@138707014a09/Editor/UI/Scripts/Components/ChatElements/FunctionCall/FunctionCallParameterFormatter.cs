using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    static class FunctionCallParameterFormatter
    {
        public static string FormatInstanceID(JToken value)
        {
            var obj = EditorUtility.EntityIdToObject(value.Value<int>());
            var displayName = obj ? obj.name : null;
            if (displayName != null)
                return $"{value.ConvertToString()} '{displayName}' [{obj.GetType().Name}]";
            return value.ConvertToString();
        }
    }
}
