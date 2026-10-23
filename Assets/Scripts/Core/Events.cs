using System;
using System.Collections.Generic;

namespace LesLeave
{
    /// <summary>Base class for every message sent through the EventBus.</summary>
    public abstract class GameEvent { }

    public sealed class StateChangedEvent : GameEvent
    {
        public readonly string Name;
        public StateChangedEvent(string name) { Name = name; }
    }

    public sealed class ScenarioPlacedEvent : GameEvent { }

    public sealed class DrillStartedEvent : GameEvent { }

    public sealed class DrillEndedEvent : GameEvent
    {
        public readonly DrillOutcome Outcome;
        public readonly string Reason;
        public DrillEndedEvent(DrillOutcome outcome, string reason) { Outcome = outcome; Reason = reason; }
    }

    public sealed class AlarmActivatedEvent : GameEvent
    {
        public readonly float Time;
        public AlarmActivatedEvent(float time) { Time = time; }
    }

    public sealed class DoorInspectedEvent : GameEvent
    {
        public readonly string DoorId;
        public readonly bool Hot;
        public readonly float Time;
        public DoorInspectedEvent(string id, bool hot, float time) { DoorId = id; Hot = hot; Time = time; }
    }

    public sealed class DoorEnteredEvent : GameEvent
    {
        public readonly string DoorId;
        public readonly bool Hot;
        public readonly float Time;
        public DoorEnteredEvent(string id, bool hot, float time) { DoorId = id; Hot = hot; Time = time; }
    }

    public sealed class ExitBlockedEvent : GameEvent
    {
        public readonly string ExitId;
        public readonly float Time;
        public ExitBlockedEvent(string id, float time) { ExitId = id; Time = time; }
    }

    public sealed class ElevatorEnteredEvent : GameEvent
    {
        public readonly float Time;
        public ElevatorEnteredEvent(float time) { Time = time; }
    }

    public sealed class StairsEnteredEvent : GameEvent
    {
        public readonly float Time;
        public StairsEnteredEvent(float time) { Time = time; }
    }

    public sealed class AssemblyReachedEvent : GameEvent
    {
        public readonly float Time;
        public AssemblyReachedEvent(float time) { Time = time; }
    }

    public sealed class SmokeChangedEvent : GameEvent
    {
        public readonly bool Active;
        public readonly float Bottom;
        public SmokeChangedEvent(bool active, float bottom) { Active = active; Bottom = bottom; }
    }

    public sealed class ExposureChangedEvent : GameEvent
    {
        public readonly float Value;
        public ExposureChangedEvent(float value) { Value = value; }
    }

    public sealed class TrackingChangedEvent : GameEvent
    {
        public readonly bool Tracking;
        public TrackingChangedEvent(bool tracking) { Tracking = tracking; }
    }

    /// <summary>Text shown on screen. Voice is an optional audio cue key ("beep").</summary>
    public sealed class WarningEvent : GameEvent
    {
        public readonly string Message;
        public readonly string Voice;
        public WarningEvent(string message, string voice = null) { Message = message; Voice = voice; }
    }

    /// <summary>Publish / subscribe hub. Managers talk to each other only through this class.</summary>
    public class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> handlers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<T>(Action<T> handler) where T : GameEvent
        {
            Type key = typeof(T);
            List<Delegate> list;
            if (!handlers.TryGetValue(key, out list))
            {
                list = new List<Delegate>();
                handlers[key] = list;
            }
            if (!list.Contains(handler)) list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : GameEvent
        {
            List<Delegate> list;
            if (handlers.TryGetValue(typeof(T), out list)) list.Remove(handler);
        }

        public void Publish<T>(T evt) where T : GameEvent
        {
            List<Delegate> list;
            if (!handlers.TryGetValue(typeof(T), out list)) return;
            Delegate[] copy = list.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                try
                {
                    ((Action<T>)copy[i]).Invoke(evt);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }
    }
}
