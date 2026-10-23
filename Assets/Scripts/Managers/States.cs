using UnityEngine;

namespace LesLeave
{
    /// <summary>State pattern: one class per phase of the drill, so each phase only contains its own rules.</summary>
    public abstract class SimState
    {
        protected readonly SimulationManager Sim;
        protected GameManager GM { get { return Sim.GM; } }

        protected SimState(SimulationManager sim) { Sim = sim; }

        public abstract string Name { get; }
        public virtual void Enter() { }
        public virtual void Tick(float dt) { }
        public virtual void Exit() { }
    }

    public class MenuState : SimState
    {
        public MenuState(SimulationManager sim) : base(sim) { }
        public override string Name { get { return "Menu"; } }

        public override void Enter()
        {
            AudioListener.pause = false;
            GM.Audio.StopAll();
            GM.Entities.Clear();
            GM.Entities.DrainPool();
            GM.AssetSvc.ReleaseAll();
            GM.AR.ClearAnchor();
            GM.AR.SetScanning(false);
            GM.AR.ShowReticle(false);
        }
    }

    public class ScanningState : SimState
    {
        private float elapsed;
        public ScanningState(SimulationManager sim) : base(sim) { }
        public override string Name { get { return "Scanning"; } }

        public override void Enter()
        {
            elapsed = 0f;
            GM.AssetSvc.Begin();
            GM.AR.SetScanning(true);
        }

        public override void Tick(float dt)
        {
            elapsed += dt;
            if (elapsed > 1f && GM.AR.PlaneDetected && GM.AssetSvc.Ready) Sim.ChangeState(new PlacingState(Sim));
        }
    }

    public class PlacingState : SimState
    {
        public PlacingState(SimulationManager sim) : base(sim) { }
        public override string Name { get { return "Placing"; } }

        public override void Enter() { GM.AR.ShowReticle(true); }
        public override void Exit() { GM.AR.ShowReticle(false); }

        public override void Tick(float dt)
        {
            Vector2 tap;
            if (TapInput.TryGet(out tap) && !GM.UI.IsOverUI(tap)) Sim.ConfirmPlacement();
        }
    }

    public class BriefingState : SimState
    {
        public BriefingState(SimulationManager sim) : base(sim) { }
        public override string Name { get { return "Briefing"; } }

        public override void Enter()
        {
            AudioListener.pause = false;
            GM.Audio.StopAll();
            Sim.CountdownRemaining = -1f;
            GM.Entities.ResetAll();
        }

        public override void Tick(float dt)
        {
            if (Sim.CountdownRemaining < 0f) return;
            Sim.CountdownRemaining -= dt;
            if (Sim.CountdownRemaining <= 0f)
            {
                Sim.CountdownRemaining = -1f;
                Sim.BeginDrill();
            }
        }
    }

    public class RunningState : SimState
    {
        private float autosave;

        public RunningState(SimulationManager sim) : base(sim) { }
        public override string Name { get { return "Running"; } }

        public override void Enter() { AudioListener.pause = false; }

        public override void Tick(float dt)
        {
            // lost tracking: pause until the phone sees the room again
            if (!GM.AR.IsTracking)
            {
                Sim.Pause(true);
                return;
            }

            Sim.Clock += dt;
            GM.Tasks.UpdatePlayer(dt);
            GM.Entities.UpdateAll(dt, Sim.Clock, GM.Tasks.Player);
            if (Sim.Current != this) return;

            GM.Tasks.Tick(dt, Sim.Clock);
            if (Sim.Current != this) return;

            Vector2 tap;
            if (TapInput.TryGet(out tap) && !GM.UI.IsOverUI(tap)) HandleTap(tap);

            if (Sim.Clock >= Sim.TimeLimit)
            {
                Sim.EndDrill(DrillOutcome.Failed, "Time ran out before you reached the assembly point.");
                return;
            }

            Sim.Session.duration = Sim.Clock;
            autosave += dt;
            if (autosave >= 5f)
            {
                autosave = 0f;
                GM.Data.AutoSave(Sim.Session);
            }
        }

        private void HandleTap(Vector2 screenPos)
        {
            Camera cam = GM.AR.Camera;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(screenPos);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 40f))
            {
                SimEntity e = hit.collider.GetComponentInParent<SimEntity>();
                if (e != null) e.OnInteract(GM.Tasks.Player);
            }
        }
    }

    public class PausedState : SimState
    {
        public readonly bool Auto;

        public PausedState(SimulationManager sim, bool auto) : base(sim) { Auto = auto; }
        public override string Name { get { return "Paused"; } }

        public override void Enter() { AudioListener.pause = true; }
        public override void Exit() { AudioListener.pause = false; }

        public override void Tick(float dt)
        {
            if (Auto && GM.AR.IsTracking) Sim.Resume();
        }
    }

    public class CompletedState : SimState
    {
        public CompletedState(SimulationManager sim) : base(sim) { }
        public override string Name { get { return "Completed"; } }
    }

    public class FailedState : SimState
    {
        public FailedState(SimulationManager sim) : base(sim) { }
        public override string Name { get { return "Failed"; } }
    }
}
