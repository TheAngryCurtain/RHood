using UnityEngine;

public class GameEvents
{
    public class BuildingChangeEvent : VSGameEvent
    {
        public bool Entered;

        public BuildingChangeEvent(bool entered)
        {
            Entered = entered;
        }
    }
}
