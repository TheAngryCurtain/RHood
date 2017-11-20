using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

// Taken from Ben Dangelo (gist @ github)
public delegate void EventDelegate<T>(T e) where T : VSGameEvent;

public class VSEventManager : Singleton<VSEventManager>
{
    [SerializeField] private bool mLimitQueueProcTime = false;
    [SerializeField] private float mQueueProcTime = 0.0f;

    private Queue mEventQueue = new Queue();

    private delegate void EventDelegate(VSGameEvent vSGE);

    private Dictionary<System.Type, EventDelegate> delegates = new Dictionary<System.Type, EventDelegate>();
    private Dictionary<System.Delegate, EventDelegate> delegateLookup = new Dictionary<System.Delegate, EventDelegate>();
    private Dictionary<System.Delegate, System.Delegate> onceLookups = new Dictionary<System.Delegate, System.Delegate>();

    private EventDelegate AddDelegate<T>(EventDelegate<T> vsDel) where T : VSGameEvent
    {
        if (delegateLookup.ContainsKey(vsDel))
        {
            return null;
        }

        // Generate an internal delegate using the event 
        EventDelegate internalDelegate = (VSGameEvent) => vsDel((T)VSGameEvent);
        delegateLookup[vsDel] = internalDelegate;

        EventDelegate tempDel;
        if (delegates.TryGetValue(typeof(T), out tempDel))
        {
            delegates[typeof(T)] = tempDel += internalDelegate;
        }
        else
        {
            delegates[typeof(T)] = internalDelegate;
        }

        return internalDelegate;
    }

    public void AddListener<T>(EventDelegate<T> vsDel) where T : VSGameEvent
    {
        AddDelegate<T>(vsDel);
    }

    public void AddListenerOnce<T>(EventDelegate<T> vsDel) where T : VSGameEvent
    {
        EventDelegate result = AddDelegate<T>(vsDel);

        if (result != null)
        {
            // remember this is only called once
            onceLookups[result] = vsDel;
        }
    }

    public void RemoveListener<T>(EventDelegate<T> vsDel) where T : VSGameEvent
    {
        EventDelegate internalDelegate;
        if (delegateLookup.TryGetValue(vsDel, out internalDelegate))
        {
            EventDelegate tempDel;
            if (delegates.TryGetValue(typeof(T), out tempDel))
            {
                tempDel -= internalDelegate;
                if (tempDel == null)
                {
                    delegates.Remove(typeof(T));
                }
                else
                {
                    delegates[typeof(T)] = tempDel;
                }
            }

            delegateLookup.Remove(vsDel);
        }
    }

    public void RemoveAll()
    {
        delegates.Clear();
        delegateLookup.Clear();
        onceLookups.Clear();
    }

    public bool HasListener<T>(EventDelegate<T> vsDel) where T : VSGameEvent
    {
        return delegateLookup.ContainsKey(vsDel);
    }

    public void TriggerEvent(VSGameEvent vSGameEvent)
    {
        EventDelegate del;
        if (delegates.TryGetValue(vSGameEvent.GetType(), out del))
        {
            del.Invoke(vSGameEvent);

            // remove listeners which should only be called once
            foreach (EventDelegate k in delegates[vSGameEvent.GetType()].GetInvocationList())
            {
                if (onceLookups.ContainsKey(k))
                {
                    delegates[vSGameEvent.GetType()] -= k;

                    if (delegates[vSGameEvent.GetType()] == null)
                    {
                        delegates.Remove(vSGameEvent.GetType());
                    }

                    delegateLookup.Remove(onceLookups[k]);
                    onceLookups.Remove(k);
                }
            }
        }
        else
        {
            Debug.LogWarning("Event: " + vSGameEvent.GetType() + " has no listeners");
        }
    }

    public bool QueueEvent(VSGameEvent evt)
    {
        if (!delegates.ContainsKey(evt.GetType()))
        {
            Debug.LogWarning("EventManager: QueueEvent failed due to no listeners for event: " + evt.GetType());
            return false;
        }

        mEventQueue.Enqueue(evt);
        return true;
    }

    void Update()
    {
        float timer = 0.0f;
        while (mEventQueue.Count > 0)
        {
            if (mLimitQueueProcTime)
            {
                if (timer > mQueueProcTime)
                    return;
            }

            VSGameEvent evt = mEventQueue.Dequeue() as VSGameEvent;
            TriggerEvent(evt);

            if (mLimitQueueProcTime)
                timer += Time.deltaTime;
        }
    }
}
