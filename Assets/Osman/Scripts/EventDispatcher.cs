using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EventDispatcher : MonoBehaviour
{
    private Dictionary<string, UnityEvent> eventRegistry;

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
            eventRegistry = new Dictionary<string, UnityEvent>();
        }
    }

    public static void RegisterListener(string eventName, UnityAction callback)
    {
        UnityEvent thisEvent = null;
        if (Instance.eventRegistry.TryGetValue(eventName, out thisEvent))
        {
            thisEvent.AddListener(callback);
        }
        else
        {
            thisEvent = new UnityEvent();
            thisEvent.AddListener(callback);
            Instance.eventRegistry.Add(eventName, thisEvent);
        }
    }

    public static void UnregisterListener(string eventName, UnityAction callback)
    {
        if (dispatcherInstance == null) return;

        UnityEvent thisEvent = null;
        if (Instance.eventRegistry.TryGetValue(eventName, out thisEvent))
        {
            thisEvent.RemoveListener(callback);
        }
    }

    public static void InvokeEvent(string eventName)
    {
        UnityEvent thisEvent = null;
        if (Instance.eventRegistry.TryGetValue(eventName, out thisEvent))
        {
            thisEvent.Invoke();
        }
    }
}