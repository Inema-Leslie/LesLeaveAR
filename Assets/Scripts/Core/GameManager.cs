using UnityEngine;

namespace LesLeave
{
    /// <summary>Composition root: creates the shared event bus, wires every manager together and drives the update order.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public EventBus Bus { get; private set; }
        public AssetLibrary Assets { get; private set; }
        public DataManager Data { get; private set; }
        public ARManager AR { get; private set; }
        public EntityManager Entities { get; private set; }
        public TaskManager Tasks { get; private set; }
        public AudioManager Audio { get; private set; }
        public SimulationManager Sim { get; private set; }
        public UIManager UI { get; private set; }
        public AssetService AssetSvc { get; private set; }

        private bool ready;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Bus = new EventBus();
            Assets = FindFirstObjectByType<AssetLibrary>();
            Data = Ensure<DataManager>();
            AR = Ensure<ARManager>();
            Entities = Ensure<EntityManager>();
            Tasks = Ensure<TaskManager>();
            Audio = Ensure<AudioManager>();
            Sim = Ensure<SimulationManager>();
            UI = Ensure<UIManager>();
            AssetSvc = Ensure<AssetService>();
        }

        private T Ensure<T>() where T : Component
        {
            T c = GetComponent<T>();
            if (c == null) c = gameObject.AddComponent<T>();
            return c;
        }

        private System.Collections.IEnumerator Start()
        {
            AssetSvc.Init(this);
            Data.Init(this);
            AR.Init(this);
            Entities.Init(this);
            Tasks.Init(this);
            Audio.Init(this);
            Sim.Init(this);
            UI.Init(this);

            yield return Data.LoadScenarios();
            Sim.Begin();
            ready = true;
        }

        private void Update()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            AR.Tick(dt);
            Sim.Tick(dt);
        }
    }
}
