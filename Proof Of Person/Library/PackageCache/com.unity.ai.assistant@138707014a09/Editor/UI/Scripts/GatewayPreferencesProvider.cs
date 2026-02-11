using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using UnityEditor;

namespace Unity.AI.Assistant.Editor
{
    static class GatewayPreferencesProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateGatewayPreferencesProvider()
        {
            var page = new GatewayProjectSettingsPage();
            page.Initialize(null);

            var provider = new SettingsProvider("Preferences/AI/Gateway", SettingsScope.User)
            {
                label = "Gateway",
                activateHandler = (searchContext, rootElement) =>
                {
                    rootElement.Add(page);
                }
            };

            return provider;
        }
    }
}
