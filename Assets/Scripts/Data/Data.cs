using System;
using System.Collections.Generic;
using UnityEngine;

namespace LesLeave
{
    public enum Difficulty { Easy = 0, Standard = 1, Hard = 2 }

    public enum DrillOutcome { None, Completed, Failed, Aborted }

    // ------------------------------------------------------------------ scenario (read-only, StreamingAssets)

    [Serializable]
    public class EntityDef
    {
        public string type;       // Door, FireAlarm, ExitPoint, Elevator, AssemblyPoint, FireVolume, Extinguisher
        public string id;
        public Vector3 pos;       // metres, relative to the placed scenario origin
        public float yaw;         // degrees
        public bool hot;          // doors only
        public float activateAt;  // seconds after the drill starts (fires)
        public float blockAt;     // seconds after the drill starts (exits); 0 = never blocked
    }

    [Serializable]
    public class HazardDef
    {
        public string type;       // Smoke
        public float start;       // seconds
        public float over;        // seconds the layer takes to descend
        public float from;        // layer bottom height at start (m)
        public float to;          // layer bottom height at the end (m)
    }

    [Serializable]
    public class ScenarioData
    {
        public string id;
        public int version;
        public string title;
        public string description;
        public float[] timeLimits;           // Easy, Standard, Hard (seconds)
        public List<EntityDef> entities = new List<EntityDef>();
        public List<HazardDef> hazards = new List<HazardDef>();
    }

    // ------------------------------------------------------------------ session (written to persistentDataPath)

    [Serializable]
    public class DecisionEvent
    {
        public float t;
        public string type;
        public string entityId;
        public bool correct;
        public Vector3 pos;
    }

    [Serializable]
    public class PathSample
    {
        public float t;
        public Vector3 p;
    }

    [Serializable]
    public class Session
    {
        public string id;
        public string scenarioId;
        public int scenarioVersion;
        public string difficulty;
        public string startTime;
        public float duration;
        public float timeLimit;
        public string outcome;
        public string reason;
        public float score;
        public float peakExposure;
        public bool alarmActivated;
        public bool inspectedDoor;
        public bool enteredHotDoor;
        public bool usedElevator;
        public bool usedStairs;
        public int speedWarnings;
        public List<DecisionEvent> events = new List<DecisionEvent>();
        public List<PathSample> path = new List<PathSample>();

        public void AddEvent(float t, string type, string entityId, bool correct, Vector3 pos)
        {
            DecisionEvent e = new DecisionEvent();
            e.t = t;
            e.type = type;
            e.entityId = entityId;
            e.correct = correct;
            e.pos = pos;
            events.Add(e);
        }
    }

    /// <summary>Runtime state of the person doing the drill.</summary>
    public class Player
    {
        public Vector3 position;
        public float deviceHeight;   // camera height above the floor plane
        public float exposure;       // 0..1
        public float speed;          // m/s, smoothed
        public bool inSmoke;
        public bool nearFire;

        public void Reset()
        {
            exposure = 0f;
            speed = 0f;
            inSmoke = false;
            nearFire = false;
        }
    }

    /// <summary>Built-in copy of scenario S1, used if the JSON file cannot be read.</summary>
    public static class DefaultScenario
    {
        private static EntityDef E(string type, string id, float x, float z, float yaw = 0f,
                                   bool hot = false, float activateAt = 0f, float blockAt = 0f)
        {
            EntityDef d = new EntityDef();
            d.type = type;
            d.id = id;
            d.pos = new Vector3(x, 0f, z);
            d.yaw = yaw;
            d.hot = hot;
            d.activateAt = activateAt;
            d.blockAt = blockAt;
            return d;
        }

        public static ScenarioData S1()
        {
            ScenarioData s = new ScenarioData();
            s.id = "S1-corridor";
            s.version = 1;
            s.title = "Classroom to corridor fire";
            s.description = "You are standing in a classroom doorway. Smoke has been detected and a fire is spreading in the corridor. "
                          + "Raise the alarm, test the doors, stay below the smoke, avoid the elevator and reach the assembly point outside.";
            s.timeLimits = new float[] { 300f, 300f, 300f };

            s.entities.Add(E("FireAlarm", "alarm", -1.0f, 0.8f));
            s.entities.Add(E("Extinguisher", "extinguisher", -1.6f, 0.4f));
            s.entities.Add(E("Door", "door1", -1.5f, 3.5f, 0f, false));
            s.entities.Add(E("Door", "door2", 1.5f, 3.5f, 0f, true));
            s.entities.Add(E("FireVolume", "fire_door2", 1.5f, 4.4f, 0f, false, 0f));
            s.entities.Add(E("FireVolume", "fire_corridor", 3.2f, 8.5f, 0f, false, 10f));
            s.entities.Add(E("ExitPoint", "exit_main", 0f, 7.5f, 0f, false, 0f, 45f));
            s.entities.Add(E("FireVolume", "fire_main", 0f, 7.0f, 0f, false, 45f));
            s.entities.Add(E("Elevator", "elevator", 3.5f, 6.0f, 90f));
            s.entities.Add(E("Stairwell", "stairs", -3.8f, 8.8f, -90f));
            s.entities.Add(E("ExitPoint", "exit_side", -3.8f, 6.5f, -90f));
            s.entities.Add(E("AssemblyPoint", "assembly", -5.0f, 11.0f));

            HazardDef smoke = new HazardDef();
            smoke.type = "Smoke";
            smoke.start = 30f;
            smoke.over = 30f;
            smoke.from = 2.4f;
            smoke.to = 1.3f;
            s.hazards.Add(smoke);
            return s;
        }
    }
}
