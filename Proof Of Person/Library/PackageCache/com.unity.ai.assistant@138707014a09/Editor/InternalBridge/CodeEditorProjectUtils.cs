using UnityEditor;

namespace Unity.AI.Assistant.Bridge.Editor
{
    class CodeEditorProjectUtils
    {
        public static void Sync()
        {
            CodeEditorProjectSync.SyncEditorProject();
        }
    }
}
