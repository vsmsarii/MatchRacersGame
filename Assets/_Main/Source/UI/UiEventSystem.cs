using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CasualKit.UI
{
    public static class UiEventSystem
    {
        public static void Ensure()
        {
            EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                Object.DontDestroyOnLoad(existing.gameObject);
                return;
            }

            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
