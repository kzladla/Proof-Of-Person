using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    static class FunctionCallRendererFactory
    {
        static readonly Lazy<Dictionary<string, Type>> k_RendererMap = new (BuildRendererMap);

        public static IFunctionCallRenderer CreateFunctionCallRenderer(string functionId)
        {
            var map = k_RendererMap.Value;

            if (!map.TryGetValue(functionId, out var elementType))
                elementType = typeof(DefaultFunctionCallRenderer);

            return (IFunctionCallRenderer)Activator.CreateInstance(elementType)!;
        }

        static Dictionary<string, Type> BuildRendererMap()
        {
            var map = new Dictionary<string, Type>();

            var types = TypeCache.GetTypesDerivedFrom<IFunctionCallRenderer>();

            foreach (var type in types)
            {
                if (type.IsAbstract)
                    continue;

                var attr = type.GetCustomAttribute<FunctionCallRendererAttribute>();
                if (attr == null)
                    continue;

                if (!map.TryAdd(attr.FunctionId, type))
                    throw new InvalidOperationException($"A renderer for {attr.FunctionId} is already registered");
            }

            return map;
        }
    }
}
