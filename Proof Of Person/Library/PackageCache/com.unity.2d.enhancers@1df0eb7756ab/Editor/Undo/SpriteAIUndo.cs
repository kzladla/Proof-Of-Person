using System;
using Unity.U2D.AI.Editor.AIBridge;
using Unity.U2D.AI.Editor.EventBus;
using Unity.U2D.AI.Editor.Events.UI;
using UnityEngine;

namespace Unity.U2D.AI.Editor.Undo
{
    interface ISpriteAIUndo : IEventSender, IDisposable
    {
        event Action<ImageReferenceActiveEditUndoData> OnUndoRedo;
        void ClearDoodleUndo();
        void SetImageManipulationToolUndo(EImageManipulationMode evtMode);
        void SetDoodleUndo(DoodleUndoData doodleUndoData);
        bool HasDoodleUndo(DoodleDataKey doodleUndoData);
        void InitDoodleUndo(DoodleUndoData doodleUndoData);
        void SetAIModeUndo(EAIMode evtMode);
        void ClearImageManipulationToolUndo();
        void ClearImageReferenceActiveEditUndo();
        void SetImageReferenceActiveEditUndo(ImageReferenceActiveEditUndoData imageReferenceActiveEditUndoData);
    }

    class SpriteAIUndo : ISpriteAIUndo
    {
        public event Action<ImageReferenceActiveEditUndoData> OnUndoRedo;

        AIModeChangeUndoObject m_AIModeUndoObject;
        ImageManipulationToolChangeUndoObject m_ImageManipulationToolChangeUndoObject;
        DoodleUndoObject m_DoodleUndoObject;
        ImageReferenceActiveEditUndoObject m_ImageReferenceActiveEditUndoObject;
        UIEventBus m_EventBus;

        public SpriteAIUndo(UIEventBus eventBus)
        {
            m_EventBus = eventBus;
            UndoObject.RegisterUndoEvent(OnUndoRedoPerformed);
        }

        public void Dispose()
        {
            UndoObject.UnregisterUndoEvent(OnUndoRedoPerformed);
            UndoObject.Dispose(m_AIModeUndoObject);
            UndoObject.Dispose(m_ImageManipulationToolChangeUndoObject);
            UndoObject.Dispose(m_DoodleUndoObject);
            UndoObject.Dispose(m_ImageReferenceActiveEditUndoObject);
            m_AIModeUndoObject = null;
            m_ImageManipulationToolChangeUndoObject = null;
            m_DoodleUndoObject = null;
            m_ImageReferenceActiveEditUndoObject = null;
        }

        public void ClearDoodleUndo()
        {
            UndoObject.ClearUndo(m_DoodleUndoObject);
            UndoObject.Dispose(m_DoodleUndoObject);
            m_DoodleUndoObject = null;
        }

        public bool HasDoodleUndo(DoodleDataKey doodleDataKey)
        {
            if (m_DoodleUndoObject == null)
                return false;

            if (m_DoodleUndoObject.data.doodleDataKey.doodlePad != doodleDataKey.doodlePad)
                return false;

            return true;
        }

        public void InitDoodleUndo(DoodleUndoData data)
        {
            if(m_DoodleUndoObject == null)
                m_DoodleUndoObject = UndoObject.Create<DoodleUndoObject, DoodleUndoData>(data);
            else
                m_DoodleUndoObject.SetData(data);
        }

        public void SetDoodlePrev(DoodleUndoData prevData)
        {
            m_DoodleUndoObject.SetData(prevData);
        }

        public void SetDoodleUndo(DoodleUndoData data)
        {
            if (m_DoodleUndoObject == null)
            {
                m_DoodleUndoObject = UndoObject.Create<DoodleUndoObject, DoodleUndoData>(data);
            }
            else
            {
                m_DoodleUndoObject.SetData(data, "Doodle");
            }
        }

        public void SetAIModeUndo(EAIMode mode)
        {
            if (m_AIModeUndoObject == null)
            {
                m_AIModeUndoObject = UndoObject.Create<AIModeChangeUndoObject, EquaptableAIMode>(mode);
            }
            else
            {
                m_AIModeUndoObject.SetData(mode, "Change AI Mode");
            }
        }

        public void SetImageManipulationToolUndo(EImageManipulationMode mode)
        {
            if (m_ImageManipulationToolChangeUndoObject == null)
            {
                m_ImageManipulationToolChangeUndoObject = UndoObject.Create<ImageManipulationToolChangeUndoObject, EquaptableEImageManipulationMode>(mode);
            }
            else
            {
                m_ImageManipulationToolChangeUndoObject.SetData(mode, "Change AI Tool");
            }
        }

        public void ClearImageManipulationToolUndo()
        {
            UndoObject.ClearUndo(m_ImageManipulationToolChangeUndoObject);
            UndoObject.Dispose(m_ImageManipulationToolChangeUndoObject);
            m_ImageManipulationToolChangeUndoObject = null;
        }

        public void SetImageReferenceActiveEditUndo(ImageReferenceActiveEditUndoData data)
        {
            if (m_ImageReferenceActiveEditUndoObject == null)
            {
                m_ImageReferenceActiveEditUndoObject = UndoObject.Create<ImageReferenceActiveEditUndoObject, ImageReferenceActiveEditUndoData>(new ImageReferenceActiveEditUndoData()
                {
                    isEditting = false,
                    imageControl = null
                });
            }
            m_ImageReferenceActiveEditUndoObject.SetData(data, "Change Image Reference Active Edit");
        }

        public void ClearImageReferenceActiveEditUndo()
        {
            UndoObject.ClearUndo(m_ImageReferenceActiveEditUndoObject);
            UndoObject.Dispose(m_ImageReferenceActiveEditUndoObject);
            m_ImageReferenceActiveEditUndoObject = null;
        }

        void OnUndoRedoPerformed()
        {
            if (m_AIModeUndoObject != null && m_AIModeUndoObject.VersionChanged(true))
            {
                m_EventBus.SendEvent(new AIModeChangeEvent(this)
                {
                    mode = m_AIModeUndoObject.data
                });
            }

            if (m_ImageManipulationToolChangeUndoObject != null && m_ImageManipulationToolChangeUndoObject.VersionChanged(true))
            {
                m_EventBus.SendEvent(new ImageManipulationModeChangeEvent(this)
                {
                    mode = m_ImageManipulationToolChangeUndoObject.data
                });
            }

            if (m_DoodleUndoObject != null && m_DoodleUndoObject.VersionChanged(true))
            {
                m_EventBus.SendEvent(new DoodleUpdateEvent(this)
                {
                    textureData = m_DoodleUndoObject.data.doodleData,
                    doodleMode = m_DoodleUndoObject.data.doodleDataKey.doodlePad
                });
            }

            if (m_ImageReferenceActiveEditUndoObject != null && m_ImageReferenceActiveEditUndoObject.VersionChanged(true))
            {
                var data = m_ImageReferenceActiveEditUndoObject.data;

                OnUndoRedo?.Invoke(data);
            }
        }
    }
}
