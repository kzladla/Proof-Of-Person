using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Services.Core
{
    static class Extensions
    {
        public static Action Use<T>(this Signal<T> signal, Action<T> callback)
        {
            void OnSignalOnOnChange() => callback(signal.Value);
            signal.OnChange += OnSignalOnOnChange;
            callback(signal.Value);

            return () => signal.OnChange -= OnSignalOnOnChange;
        }

        internal static void OnShow(VisualElement element)
        {
            try
            {
                foreach (var item in DropdownExtension.onShow.OrderBy(item => item.order))
                    item.callback.Invoke(element);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal static void OnExtend(VisualElement element)
        {
            try
            {
                foreach (var item in DropdownExtension.onExtend.OrderBy(item => item.order))
                    item.callback.Invoke(element);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal static void OnExtendGeneral(VisualElement element)
        {
            try
            {
                foreach (var item in DropdownExtension.onExtendMain.OrderBy(item => item.order))
                    item.callback.Invoke(element);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
