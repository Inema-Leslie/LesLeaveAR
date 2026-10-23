using System;
using UnityEngine;

namespace LesLeave
{
    /// <summary>Everything an entity needs from the outside world, handed over by the EntityManager.</summary>
    public class EntityContext
    {
        public EventBus Bus;
        public AssetLibrary Lib;
        public PrefabPool Pool;
        public Func<float> Now;     // drill clock in seconds
        public float Scale = 1f;
    }

    /// <summary>
    /// Base class of everything placed in the scenario (inheritance). Callers only use this type;
    /// each subclass decides what a tap or a tick means for it (polymorphism).
    /// </summary>
    public abstract class SimEntity : MonoBehaviour
    {
        public string Id { get; private set; }
        public EntityDef Def { get; private set; }
        protected EntityContext Ctx { get; private set; }

        public void Setup(EntityDef def, EntityContext ctx)
        {
            Id = def.id;
            Def = def;
            Ctx = ctx;
            Build();
        }

        /// <summary>Create the visuals and colliders.</summary>
        protected abstract void Build();

        /// <summary>The user tapped this entity.</summary>
        public virtual void OnInteract(Player player) { }

        /// <summary>Called every frame while the drill is running.</summary>
        public virtual void Tick(float dt, float time, Player player) { }

        /// <summary>Back to the state it had before the drill started.</summary>
        public virtual void ResetState() { }

        /// <summary>Give pooled objects back before this entity is destroyed.</summary>
        public virtual void OnReleased() { }

        protected float FlatDistance(Player p)
        {
            Vector3 d = p.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        protected bool InRange(Player p, float radius, string what)
        {
            if (FlatDistance(p) <= radius) return true;
            Ctx.Bus.Publish(new WarningEvent("Move closer to the " + what + " (within " + radius.ToString("0") + " m)."));
            return false;
        }
    }
}
