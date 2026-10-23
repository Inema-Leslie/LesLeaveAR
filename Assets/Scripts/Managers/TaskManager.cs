using System.Collections.Generic;
using UnityEngine;

namespace LesLeave
{
    public class TaskItem
    {
        public string Id;
        public string Label;
        public bool Done;
        public bool Failed;
    }

    /// <summary>Tracks the trainee's objectives, exposure and speed, and records every decision.</summary>
    public class TaskManager : MonoBehaviour
    {
        public const float SpeedLimit = 1.5f;   // m/s, NFR-07

        public readonly Player Player = new Player();
        public readonly List<TaskItem> Tasks = new List<TaskItem>();

        private GameManager gm;
        private Vector3 lastPos;
        private bool haveLast;
        private float speedTimer;
        private float speedCooldown;
        private float pathTimer;
        private float lastExposurePublished = -1f;
        private bool wasInSmoke;
        private float lowTimer;

        private Session Session { get { return gm.Sim.Session; } }

        public void Init(GameManager g)
        {
            gm = g;
            ResetTasks();
            gm.Bus.Subscribe<AlarmActivatedEvent>(OnAlarm);
            gm.Bus.Subscribe<DoorInspectedEvent>(OnDoorInspected);
            gm.Bus.Subscribe<DoorEnteredEvent>(OnDoorEntered);
            gm.Bus.Subscribe<ElevatorEnteredEvent>(OnElevator);
            gm.Bus.Subscribe<StairsEnteredEvent>(OnStairs);
            gm.Bus.Subscribe<AssemblyReachedEvent>(OnAssembly);
            gm.Bus.Subscribe<ExitBlockedEvent>(OnExitBlocked);
            gm.Bus.Subscribe<SmokeChangedEvent>(OnSmoke);
        }

        private void ResetTasks()
        {
            Tasks.Clear();
            Tasks.Add(new TaskItem { Id = "low", Label = "Stay low" });
            Tasks.Add(new TaskItem { Id = "signs", Label = "Follow signs" });
            Tasks.Add(new TaskItem { Id = "stairs", Label = "Use stairs" });
            Tasks.Add(new TaskItem { Id = "assembly", Label = "Assembly point" });
        }

        private TaskItem Get(string id)
        {
            for (int i = 0; i < Tasks.Count; i++) if (Tasks[i].Id == id) return Tasks[i];
            return null;
        }

        public void BeginDrill()
        {
            ResetTasks();
            Player.Reset();
            haveLast = false;
            speedTimer = 0f;
            speedCooldown = 0f;
            pathTimer = 0f;
            lastExposurePublished = -1f;
            wasInSmoke = false;
            lowTimer = 0f;
            gm.Bus.Publish(new ExposureChangedEvent(0f));
        }

        /// <summary>Read the camera: position, height above the floor and walking speed.</summary>
        public void UpdatePlayer(float dt)
        {
            Transform cam = gm.AR.CameraTransform;
            if (cam == null) return;

            Vector3 p = cam.position;
            Player.position = p;
            Player.deviceHeight = p.y - gm.AR.FloorY;

            if (haveLast && dt > 0.0001f)
            {
                Vector3 d = p - lastPos;
                d.y = 0f;
                float inst = d.magnitude / dt;
                Player.speed = Mathf.Lerp(Player.speed, inst, Mathf.Clamp01(dt * 4f));
            }
            lastPos = p;
            haveLast = true;
            Player.nearFire = false;   // entities set it again this frame
        }

        /// <summary>Exposure, speed warnings and path recording. Runs after the entities have updated.</summary>
        public void Tick(float dt, float time)
        {
            SmokeLayer smoke = gm.Entities.Smoke;
            Player.inSmoke = smoke != null && smoke.Active && Player.deviceHeight > smoke.Bottom;

            float rise = 0.14f;
            if (gm.Sim.Difficulty == Difficulty.Easy) rise = 0.08f;
            else if (gm.Sim.Difficulty == Difficulty.Hard) rise = 0.22f;

            bool exposed = Player.inSmoke || Player.nearFire;
            if (exposed) Player.exposure += rise * dt * (Player.nearFire ? 1.5f : 1f);
            else Player.exposure -= 0.10f * dt;
            Player.exposure = Mathf.Clamp01(Player.exposure);

            // "Stay low": crouched under an active smoke layer for a moment
            if (smoke != null && smoke.Active && !Player.inSmoke)
            {
                lowTimer += dt;
                if (lowTimer > 1.5f) Get("low").Done = true;
            }
            else if (smoke == null || !smoke.Active) lowTimer = 0f;

            if (Session != null) Session.peakExposure = Mathf.Max(Session.peakExposure, Player.exposure);

            if (Mathf.Abs(Player.exposure - lastExposurePublished) > 0.004f)
            {
                lastExposurePublished = Player.exposure;
                gm.Bus.Publish(new ExposureChangedEvent(Player.exposure));
            }

            if (Player.inSmoke && !wasInSmoke)
                gm.Bus.Publish(new WarningEvent("You are in the smoke! Crouch lower.", "beep"));
            wasInSmoke = Player.inSmoke;

            // speed limit warning
            speedCooldown -= dt;
            if (Player.speed > SpeedLimit)
            {
                speedTimer += dt;
                if (speedTimer > 0.4f && speedCooldown <= 0f)
                {
                    speedCooldown = 4f;
                    gm.Bus.Publish(new WarningEvent("Slow down. Walk, do not run.", "beep"));
                    if (Session != null)
                    {
                        Session.speedWarnings++;
                        Session.AddEvent(time, "SpeedWarning", "", false, Player.position);
                    }
                }
            }
            else speedTimer = 0f;

            // path sample, twice a second
            pathTimer += dt;
            if (pathTimer >= 0.5f && Session != null)
            {
                pathTimer = 0f;
                PathSample ps = new PathSample();
                ps.t = time;
                ps.p = Player.position;
                Session.path.Add(ps);
            }
        }

        public float ComputeScore(Session s)
        {
            ScoringStrategy strategy;
            if (gm.Sim.Difficulty == Difficulty.Easy) strategy = new EasyScoring();
            else if (gm.Sim.Difficulty == Difficulty.Hard) strategy = new StrictScoring();
            else strategy = new StandardScoring();
            return strategy.Compute(s);
        }

        // ------------------------------------------------------------------ event handlers

        private void Log(float t, string type, string id, bool correct)
        {
            if (Session != null) Session.AddEvent(t, type, id, correct, Player.position);
        }

        private void OnAlarm(AlarmActivatedEvent e)
        {
            if (Session != null) Session.alarmActivated = true;
            Log(e.Time, "AlarmActivated", "alarm", true);
        }

        private void OnDoorInspected(DoorInspectedEvent e)
        {
            if (Session != null) Session.inspectedDoor = true;
            Log(e.Time, "DoorInspected", e.DoorId, true);
        }

        private void OnDoorEntered(DoorEnteredEvent e)
        {
            TaskItem t = Get("signs");
            if (e.Hot)
            {
                t.Failed = true;
                if (Session != null) Session.enteredHotDoor = true;
                Player.exposure = Mathf.Clamp01(Player.exposure + 0.25f);
            }
            else if (!t.Failed)
            {
                t.Done = true;
            }
            Log(e.Time, "DoorEntered", e.DoorId, !e.Hot);
        }

        private void OnElevator(ElevatorEnteredEvent e)
        {
            Get("stairs").Failed = true;
            if (Session != null) Session.usedElevator = true;
            Log(e.Time, "ElevatorEntered", "elevator", false);
        }

        private void OnStairs(StairsEnteredEvent e)
        {
            TaskItem t = Get("stairs");
            if (!t.Failed) t.Done = true;
            if (Session != null) Session.usedStairs = true;
            Log(e.Time, "StairsEntered", "stairs", true);
        }

        /// <summary>The next place the trainee should head for, or false when there is nothing to point at.</summary>
        public bool TryGetTarget(out string label, out Vector3 position)
        {
            label = "";
            position = Vector3.zero;
            string id;
            if (Session == null) return false;

            if (!Session.alarmActivated) { id = "alarm"; label = "Fire alarm"; }
            else if (!Get("signs").Done && !Get("signs").Failed) return false;
            else if (!Get("stairs").Done && !Get("stairs").Failed) { id = "stairs"; label = "Stairwell"; }
            else { id = "assembly"; label = "Assembly point"; }

            SimEntity target = gm.Entities.Find(id);
            if (target == null) return false;
            position = target.transform.position;
            return true;
        }

        private void OnAssembly(AssemblyReachedEvent e)
        {
            Get("assembly").Done = true;
            Log(e.Time, "AssemblyReached", "assembly", true);
        }

        private void OnExitBlocked(ExitBlockedEvent e)
        {
            Log(e.Time, "ExitBlocked", e.ExitId, true);
        }

        private void OnSmoke(SmokeChangedEvent e)
        {
            if (e.Active && Session != null && !HasEvent("SmokeStarted"))
                Log(gm.Sim.Clock, "SmokeStarted", "smoke", true);
        }

        private bool HasEvent(string type)
        {
            if (Session == null) return false;
            for (int i = 0; i < Session.events.Count; i++) if (Session.events[i].type == type) return true;
            return false;
        }
    }
}
