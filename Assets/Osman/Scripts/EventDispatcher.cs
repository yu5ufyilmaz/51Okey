using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EventDispatcher : MonoBehaviour
{
    private Dictionary<string, UnityEventBase> eventRegistry;

    private static EventDispatcher dispatcherInstance;

    public static EventDispatcher Instance
    {
        get
        {
            if (!dispatcherInstance)
            {
                dispatcherInstance = FindObjectOfType(typeof(EventDispatcher)) as EventDispatcher;

                if (!dispatcherInstance)
                {
                    Debug.LogError("Active event dispatcher not found in the scene!");
                }
                else
                {
                    dispatcherInstance.Setup();
                }
            }

            return dispatcherInstance;
        }
    }

    private void Setup()
    {
        if (eventRegistry == null)
        {
            eventRegistry = new Dictionary<string, UnityEventBase>();
        }
    }

    // Parametresiz olay dinleyici
    public static void RegisterFunction(string eventName, UnityAction callback)
    {
        UnityEvent thisEvent = null;
        if (Instance.eventRegistry.TryGetValue(eventName, out UnityEventBase baseEvent))
        {
            thisEvent = baseEvent as UnityEvent;
            thisEvent.AddListener(callback);
        }
        else
        {
            thisEvent = new UnityEvent();
            thisEvent.AddListener(callback);
            Instance.eventRegistry.Add(eventName, thisEvent);
        }
    }

    // Parametreli olay dinleyici (Örneğin string parametre alan)
    public static void RegisterFunction<T>(string eventName, UnityAction<T> callback)
    {
        UnityEvent<T> thisEvent = null;
        if (Instance.eventRegistry.TryGetValue(eventName, out UnityEventBase baseEvent))
        {
            thisEvent = baseEvent as UnityEvent<T>;
            thisEvent.AddListener(callback);
        }
        else
        {
            thisEvent = new UnityEvent<T>();
            thisEvent.AddListener(callback);
            Instance.eventRegistry.Add(eventName, thisEvent);
        }
    }

    // Parametresiz olay çağırma
    public static void SummonEvent(string eventName)
    {
        if (Instance.eventRegistry.TryGetValue(eventName, out UnityEventBase baseEvent))
        {
            (baseEvent as UnityEvent)?.Invoke();
        }
    }

    // Parametreli olay çağırma (string gibi bir parametre ile)
    public static void SummonEvent<T>(string eventName, T param)
    {
        if (Instance.eventRegistry.TryGetValue(eventName, out UnityEventBase baseEvent))
        {
            (baseEvent as UnityEvent<T>)?.Invoke(param);
        }
    }

    // Parametresiz olay dinleyiciyi kaldırma
    public static void UnregisterListener(string eventName, UnityAction callback)
    {
        if (dispatcherInstance == null) return;

        if (Instance.eventRegistry.TryGetValue(eventName, out UnityEventBase baseEvent))
        {
            (baseEvent as UnityEvent)?.RemoveListener(callback);
        }
    }

    // Parametreli olay dinleyiciyi kaldırma
    public static void UnregisterListener<T>(string eventName, UnityAction<T> callback)
    {
        if (dispatcherInstance == null) return;

        if (Instance.eventRegistry.TryGetValue(eventName, out UnityEventBase baseEvent))
        {
            (baseEvent as UnityEvent<T>)?.RemoveListener(callback);
        }
    }
}
