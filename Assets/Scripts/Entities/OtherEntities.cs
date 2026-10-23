using UnityEngine;

namespace LesLeave
{
    /// <summary>The elevator. Using it in a fire is a critical mistake and ends the drill.</summary>
    public class Elevator : SimEntity
    {
        private bool entered;
        private bool warned;

        protected override void Build()
        {
            Color metal = new Color(0.55f, 0.58f, 0.62f);
            ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(-0.3f, 1.05f, 0f), new Vector3(0.58f, 2.1f, 0.08f), metal, false);
            ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(0.3f, 1.05f, 0f), new Vector3(0.58f, 2.1f, 0.08f), metal, false);
            ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(0f, 2.2f, 0f), new Vector3(1.4f, 0.12f, 0.12f), new Color(0.25f, 0.25f, 0.28f), false);

            Texture danger = Ctx.Lib != null ? Ctx.Lib.dangerSign : null;
            if (danger != null)
                ModelUtil.TextureQuad(transform, danger, new Vector3(0f, 2.6f, 0f), new Vector2(0.45f, 0.45f), false);
            else
                ModelUtil.Primitive(PrimitiveType.Quad, transform, new Vector3(0f, 2.6f, 0f), new Vector3(0.45f, 0.45f, 1f), new Color(0.9f, 0.1f, 0.1f), false);

            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.1f, 0f);
            col.size = new Vector3(1.4f, 2.3f, 0.4f);
        }

        public override void OnInteract(Player player)
        {
            Ctx.Bus.Publish(new WarningEvent("Never use an elevator during a fire."));
        }

        public override void Tick(float dt, float time, Player player)
        {
            float d = FlatDistance(player);
            if (!warned && d < 2f)
            {
                warned = true;
                Ctx.Bus.Publish(new WarningEvent("Do not use the elevator!", "beep"));
            }
            if (!entered && d < 0.9f)
            {
                entered = true;
                Ctx.Bus.Publish(new ElevatorEnteredEvent(time));
            }
        }

        public override void ResetState()
        {
            entered = false;
            warned = false;
        }
    }

    /// <summary>The stairwell: the correct way down. Walking into it counts as using the stairs.</summary>
    public class Stairwell : SimEntity
    {
        private bool entered;
        private bool warned;

        protected override void Build()
        {
            Color frame = new Color(0.8f, 0.8f, 0.8f);
            ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(-0.55f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.12f), frame, false);
            ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(0.55f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.12f), frame, false);
            ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(0f, 2.2f, 0f), new Vector3(1.2f, 0.1f, 0.12f), frame, false);

            // three steps leading down behind the doorway
            for (int i = 0; i < 3; i++)
            {
                ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(0f, 0.04f, 0.3f + i * 0.3f),
                                    new Vector3(1.0f, 0.08f, 0.28f), new Color(0.3f + i * 0.08f, 0.32f + i * 0.08f, 0.34f + i * 0.08f), false);
            }

            Texture safe = Ctx.Lib != null ? Ctx.Lib.safeSign : null;
            if (safe != null)
                ModelUtil.TextureQuad(transform, safe, new Vector3(0f, 2.55f, 0f), new Vector2(0.5f, 0.5f), false);
            else
                ModelUtil.Primitive(PrimitiveType.Quad, transform, new Vector3(0f, 2.55f, 0f), new Vector3(0.5f, 0.5f, 1f), new Color(0.1f, 0.7f, 0.25f), false);

            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.1f, 0f);
            col.size = new Vector3(1.3f, 2.3f, 0.4f);
        }

        public override void OnInteract(Player player)
        {
            Ctx.Bus.Publish(new WarningEvent("The stairwell is the safe way down. Walk in."));
        }

        public override void Tick(float dt, float time, Player player)
        {
            float d = FlatDistance(player);
            if (!warned && d < 2.5f)
            {
                warned = true;
                Ctx.Bus.Publish(new WarningEvent("Stairwell ahead. Use the stairs."));
            }
            if (!entered && d < 0.9f)
            {
                entered = true;
                Ctx.Bus.Publish(new StairsEnteredEvent(time));
            }
        }

        public override void ResetState()
        {
            entered = false;
            warned = false;
        }
    }

    /// <summary>The safe place outside. Reaching it completes the drill.</summary>
    public class AssemblyPoint : SimEntity
    {
        private const float ReachRadius = 1.0f;

        private bool reached;
        private Transform ring;

        public bool IsReached(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude <= ReachRadius;
        }

        protected override void Build()
        {
            ModelUtil.Primitive(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.01f, 0f),
                                new Vector3(2.0f, 0.01f, 2.0f), new Color(0.1f, 0.75f, 0.3f, 0.75f), false);
            GameObject r = ModelUtil.Primitive(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.02f, 0f),
                                               new Vector3(2.3f, 0.005f, 2.3f), new Color(1f, 1f, 1f, 0.35f), false);
            ring = r.transform;

            // pole and sign so it can be seen from far away
            ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.05f, 1.8f, 0.05f), new Color(0.8f, 0.8f, 0.8f), false);
            Texture safe = Ctx.Lib != null ? Ctx.Lib.safeSign : null;
            if (safe != null)
                ModelUtil.TextureQuad(transform, safe, new Vector3(0f, 2.1f, 0f), new Vector2(0.8f, 0.8f), false);
            else
                ModelUtil.Primitive(PrimitiveType.Quad, transform, new Vector3(0f, 2.1f, 0f), new Vector3(0.8f, 0.8f, 1f), new Color(0.1f, 0.7f, 0.25f), false);

            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.0f, 0f);
            col.size = new Vector3(1.0f, 2.0f, 1.0f);
        }

        public override void OnInteract(Player player)
        {
            Ctx.Bus.Publish(new WarningEvent("This is the assembly point. Walk to it to finish the drill."));
        }

        public override void Tick(float dt, float time, Player player)
        {
            if (ring != null)
            {
                float k = 2.3f + Mathf.Sin(time * 3f) * 0.25f;
                ring.localScale = new Vector3(k, 0.005f, k);
            }
            if (!reached && IsReached(player.position))
            {
                reached = true;
                Ctx.Bus.Publish(new AssemblyReachedEvent(time));
            }
        }

        public override void ResetState() { reached = false; }
    }

    /// <summary>Decoration. Shows that extinguishers are not the answer to a growing fire.</summary>
    public class Extinguisher : SimEntity
    {
        protected override void Build()
        {
            GameObject visual = null;
            if (Ctx.Lib != null && Ctx.Lib.extinguisherPrefab != null)
                visual = ModelUtil.InstantiateFitted(Ctx.Lib.extinguisherPrefab, transform, 0.55f, true);
            if (visual == null)
                ModelUtil.Primitive(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.27f, 0f),
                                    new Vector3(0.16f, 0.27f, 0.16f), new Color(0.85f, 0.1f, 0.1f), false);

            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.3f, 0f);
            col.size = new Vector3(0.4f, 0.6f, 0.4f);
        }

        public override void OnInteract(Player player)
        {
            Ctx.Bus.Publish(new WarningEvent("Extinguishers are only for small fires. Evacuate first!"));
        }
    }
}
