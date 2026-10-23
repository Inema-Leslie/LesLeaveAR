using System.Collections.Generic;
using UnityEngine;

namespace LesLeave
{
    /// <summary>Factory: turns a scenario definition into a live entity.</summary>
    public class EntityFactory
    {
        public SimEntity Create(EntityDef def, Transform parent, EntityContext ctx)
        {
            GameObject go = new GameObject(def.type + "_" + def.id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(def.pos.x * ctx.Scale, def.pos.y, def.pos.z * ctx.Scale);
            go.transform.localRotation = Quaternion.Euler(0f, def.yaw, 0f);

            SimEntity e = null;
            switch (def.type)
            {
                case "Door": e = go.AddComponent<Door>(); break;
                case "FireAlarm": e = go.AddComponent<FireAlarm>(); break;
                case "ExitPoint": e = go.AddComponent<ExitPoint>(); break;
                case "Elevator": e = go.AddComponent<Elevator>(); break;
                case "Stairwell": e = go.AddComponent<Stairwell>(); break;
                case "AssemblyPoint": e = go.AddComponent<AssemblyPoint>(); break;
                case "FireVolume": e = go.AddComponent<FireVolume>(); break;
                case "Extinguisher": e = go.AddComponent<Extinguisher>(); break;
                default:
                    Debug.LogWarning("Unknown entity type in scenario: " + def.type);
                    Object.Destroy(go);
                    return null;
            }
            e.Setup(def, ctx);
            return e;
        }
    }

    /// <summary>Spawns, updates and removes every entity of the placed scenario.</summary>
    public class EntityManager : MonoBehaviour
    {
        private readonly List<SimEntity> entities = new List<SimEntity>();
        private readonly EntityFactory factory = new EntityFactory();
        private GameManager gm;
        private PrefabPool pool;
        private SmokeLayer smoke;

        public SmokeLayer Smoke { get { return smoke; } }
        public bool HasScenario { get { return entities.Count > 0; } }

        public void Init(GameManager g)
        {
            gm = g;
            GameObject poolRoot = new GameObject("PrefabPool");
            poolRoot.transform.SetParent(transform, false);
            poolRoot.SetActive(false);
            pool = new PrefabPool(poolRoot.transform);
        }

        public void Build(ScenarioData data, Transform anchor, float scale)
        {
            Clear();

            EntityContext ctx = new EntityContext();
            ctx.Bus = gm.Bus;
            ctx.Lib = gm.Assets;
            ctx.Pool = pool;
            ctx.Scale = scale;
            ctx.Now = () => gm.Sim.Clock;

            for (int i = 0; i < data.entities.Count; i++)
            {
                SimEntity e = factory.Create(data.entities[i], anchor, ctx);
                if (e != null) entities.Add(e);
            }

            for (int i = 0; i < data.hazards.Count; i++)
            {
                if (data.hazards[i].type != "Smoke") continue;
                GameObject go = new GameObject("SmokeLayer");
                go.transform.SetParent(anchor, false);
                smoke = go.AddComponent<SmokeLayer>();
                smoke.Hazard = data.hazards[i];
                EntityDef def = new EntityDef();
                def.type = "SmokeLayer";
                def.id = "smoke";
                smoke.Setup(def, ctx);
                entities.Add(smoke);
            }
        }

        public void UpdateAll(float dt, float time, Player player)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] != null) entities[i].Tick(dt, time, player);
            }
        }

        public void ResetAll()
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] != null) entities[i].ResetState();
            }
        }

        public void Clear()
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] == null) continue;
                entities[i].OnReleased();
                Destroy(entities[i].gameObject);
            }
            entities.Clear();
            smoke = null;
        }

        public void DrainPool() { if (pool != null) pool.Clear(); }

        public SimEntity Find(string id)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] != null && entities[i].Id == id) return entities[i];
            }
            return null;
        }
    }
}
