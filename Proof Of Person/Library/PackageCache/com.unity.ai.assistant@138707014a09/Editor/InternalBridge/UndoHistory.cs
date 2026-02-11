using UnityEditor;

namespace Unity.AI.Assistant.Bridge.Editor
{
    class UndoHistoryUtils
    {
        internal static void OpenHistory()
        {
            UndoHistoryWindow.OpenUndoHistory();
        }

        internal static void RevertGroupAndOpenHistory(int group)
        {
            Undo.RevertAllDownToGroup(group);
            UndoHistoryWindow.OpenUndoHistory();
        }
    }
}
