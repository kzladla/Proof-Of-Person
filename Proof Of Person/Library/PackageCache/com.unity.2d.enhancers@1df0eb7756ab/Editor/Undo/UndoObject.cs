using System;
using UnityEngine;

namespace Unity.U2D.AI.Editor.Undo
{
    [Serializable]
    abstract class UndoObject : ScriptableObject
    {
        static bool s_Undoing = false;
        static event Action s_OnUndoRedoPerformedCallback;
        public static void BeginUndo()
        {
            s_Undoing = true;
        }

        public static void EndUndo()
        {
            s_Undoing = false;
        }

        protected static bool undoing => s_Undoing;

        public static T Create<T, T1>(T1 data) where T : UndoObject<T1> where T1:struct, IEquatable<T1>
        {
            var undoObject = CreateInstance<T>();
            undoObject.hideFlags = HideFlags.HideAndDontSave;
            undoObject.Init(data);
            return undoObject;
        }

        public static void Dispose(UndoObject undoObject)
        {
            undoObject?.Dispose();
            ObjectHelper.SafeDestroy(undoObject);
        }

        public static void RegisterUndoEvent(Action undoCallback)
        {
            s_OnUndoRedoPerformedCallback += undoCallback;
            if (s_OnUndoRedoPerformedCallback?.GetInvocationList().Length == 1)
            {
                // TODO: figure out how to abstract this to interface
                UnityEditor.Undo.undoRedoPerformed += OnUndoRedoPerformed;
            }
        }

        public static void UnregisterUndoEvent(Action undoCallback)
        {
            s_OnUndoRedoPerformedCallback -= undoCallback;
            if (s_OnUndoRedoPerformedCallback?.GetInvocationList().Length== 0)
            {
                // TODO: figure out how to abstract this to interface
                UnityEditor.Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            }
        }

        static void OnUndoRedoPerformed()
        {
            BeginUndo();
            s_OnUndoRedoPerformedCallback?.Invoke();
            EndUndo();
        }

        public abstract void Dispose();

        public static void ClearUndo(UnityEngine.Object doodleUndoObject)
        {
            UnityEditor.Undo.ClearUndo(doodleUndoObject);
        }
    }

    [Serializable]
    class UndoObject<T> : UndoObject, ISerializationCallbackReceiver where T:struct, IEquatable<T>
    {

        [SerializeField]
        int m_Version = 0;
        int m_CurrentVersion = 0;

        [SerializeField]
        T m_Data;

        public void Init(T data)
        {
            m_Data = data;
        }

        public void SetData(T data, string actionName)
        {
            if (!undoing && !data.Equals(m_Data))
            {
                UnityEditor.Undo.RegisterCompleteObjectUndo(this, actionName);
                m_Data = data;
                m_CurrentVersion++;
                m_Version = m_CurrentVersion;
            }
        }

        public T data => m_Data;

        public void SetData(T data)
        {
            m_Data = data;
        }

        public bool VersionChanged(bool resetVersion)
        {
            bool returnValue = m_CurrentVersion != m_Version;
            if (resetVersion)
                m_CurrentVersion = m_Version;
            return returnValue;
        }

        public override void Dispose()
        {
            UnityEditor.Undo.ClearUndo(this);
        }

        public void OnBeforeSerialize()
        {

        }

        public void OnAfterDeserialize()
        {

        }
    }
}
