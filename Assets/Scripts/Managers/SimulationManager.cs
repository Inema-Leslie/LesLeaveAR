using System;
using UnityEngine;

namespace LesLeave
{
    /// <summary>Runs the drill: owns the state machine, the clock and the session record.</summary>
    public class SimulationManager : MonoBehaviour
    {
        public GameManager GM { get; private set; }
        public SimState Current { get; private set; }
        public string StateName { get { return Current != null ? Current.Name : "None"; } }

        public float Clock;
        public float CountdownRemaining = -1f;
        public Session Session { get; private set; }
        public DrillOutcome Outcome { get; private set; }
        public string EndReason { get; private set; }

        public int ScenarioIndex;
        public Difficulty Difficulty = Difficulty.Standard;
        public bool SmallRoom;

        public ScenarioData Scenario
        {
            get
            {
                if (GM.Data.Scenarios.Count == 0) return null;
                return GM.Data.Scenarios[Mathf.Clamp(ScenarioIndex, 0, GM.Data.Scenarios.Count - 1)];
            }
        }

        public float Scale { get { return SmallRoom ? 0.5f : 1f; } }

        public float TimeLimit
        {
            get
            {
                ScenarioData s = Scenario;
                if (s == null || s.timeLimits == null || s.timeLimits.Length < 3) return 120f;
                return s.timeLimits[(int)Difficulty];
            }
        }

        public void Init(GameManager g)
        {
            GM = g;
            GM.Bus.Subscribe<ExposureChangedEvent>(OnExposure);
            GM.Bus.Subscribe<ElevatorEnteredEvent>(OnElevator);
            GM.Bus.Subscribe<AssemblyReachedEvent>(OnAssembly);
        }

        public void Begin() { ChangeState(new MenuState(this)); }

        public void ChangeState(SimState next)
        {
            if (Current != null) Current.Exit();
            Current = next;
            Current.Enter();
            GM.Bus.Publish(new StateChangedEvent(Current.Name));
        }

        public void Tick(float dt)
        {
            if (Current != null) Current.Tick(dt);
        }

        // ------------------------------------------------------------------ flow controls (called by the UI)

        public void StartFlow() { ChangeState(new ScanningState(this)); }

        /// <summary>Pick the scenario up and put it somewhere else, without scanning again.</summary>
        public void Reposition()
        {
            GM.Entities.Clear();
            ChangeState(new PlacingState(this));
        }

        public void ToMenu() { ChangeState(new MenuState(this)); }

        public void ConfirmPlacement()
        {
            Pose pose;
            if (!GM.AR.TryGetHoverPose(out pose)) return;
            ScenarioData data = Scenario;
            if (data == null) return;

            Transform anchor = GM.AR.PlaceAnchor(pose);
            GM.Entities.Build(data, anchor, Scale);
            GM.Bus.Publish(new ScenarioPlacedEvent());
            ChangeState(new BriefingState(this));
        }

        public void StartCountdown() { CountdownRemaining = 3f; }

        public void BeginDrill()
        {
            ScenarioData data = Scenario;
            Clock = 0f;
            Outcome = DrillOutcome.None;
            EndReason = "";

            Session = new Session();
            Session.id = Guid.NewGuid().ToString("N");
            Session.scenarioId = data.id;
            Session.scenarioVersion = data.version;
            Session.difficulty = Difficulty.ToString();
            Session.startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Session.timeLimit = TimeLimit;
            Session.outcome = "InProgress";

            GM.Entities.ResetAll();
            GM.Tasks.BeginDrill();
            GM.Data.AutoSave(Session);
            GM.Bus.Publish(new DrillStartedEvent());
            ChangeState(new RunningState(this));
        }

        public void Pause(bool auto)
        {
            if (Current is RunningState) ChangeState(new PausedState(this, auto));
        }

        public void Resume()
        {
            if (Current is PausedState) ChangeState(new RunningState(this));
        }

        public void Abort()
        {
            if (Session != null && (Current is PausedState || Current is RunningState))
            {
                Finish(DrillOutcome.Aborted, "Drill aborted by the user.");
            }
            ToMenu();
        }

        public void Retry()
        {
            ChangeState(new BriefingState(this));
        }

        public void EndDrill(DrillOutcome outcome, string reason)
        {
            if (!(Current is RunningState)) return;
            Finish(outcome, reason);
            if (outcome == DrillOutcome.Completed) ChangeState(new CompletedState(this));
            else ChangeState(new FailedState(this));
        }

        private void Finish(DrillOutcome outcome, string reason)
        {
            Outcome = outcome;
            EndReason = reason;
            Session.outcome = outcome.ToString();
            Session.reason = reason;
            Session.duration = Clock;
            Session.score = GM.Tasks.ComputeScore(Session);
            GM.Data.SaveSession(Session);
            GM.Bus.Publish(new DrillEndedEvent(outcome, reason));
        }

        // ------------------------------------------------------------------ rules that end the drill

        private void OnExposure(ExposureChangedEvent e)
        {
            if (e.Value >= 0.999f)
                EndDrill(DrillOutcome.Failed, "Smoke exposure reached 100 %. Stay below the smoke layer.");
        }

        private void OnElevator(ElevatorEnteredEvent e)
        {
            EndDrill(DrillOutcome.Failed, "You used the elevator. Never use an elevator in a fire.");
        }

        private void OnAssembly(AssemblyReachedEvent e)
        {
            EndDrill(DrillOutcome.Completed, "You reached the assembly point.");
        }
    }
}
