using UnityEngine;

namespace LesLeave
{
    /// <summary>An exit. Some exits get blocked by fire and debris partway through the drill.</summary>
    public class ExitPoint : SimEntity
    {
        private static readonly Color OpenColor = new Color(0.2f, 1f, 0.4f, 0.18f);
        private static readonly Color BlockedColor = new Color(1f, 0.2f, 0.1f, 0.28f);

        private bool blocked;
        private float blockAt;
        private float warnCooldown;
        private GameObject sign;
        private Renderer opening;
        private GameObject rubble;
        private bool rubblePooled;
        private Vector3 rubbleBaseScale = Vector3.one;

        public bool IsBlocked { get { return blocked; } }

        protected override void Build()
        {
            blockAt = Def.blockAt;
            Color frame = new Color(0.8f, 0.8f, 0.8f);

            ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(-0.8f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.1f), frame, false);
            ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(0.8f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.1f), frame, false);
            ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(0f, 2.2f, 0f), new Vector3(1.7f, 0.1f, 0.1f), frame, false);

            GameObject glass = ModelUtil.Primitive(PrimitiveType.Quad, transform, new Vector3(0f, 1.1f, 0f),
                                                   new Vector3(1.6f, 2.2f, 1f), OpenColor, false);
            opening = glass.GetComponent<Renderer>();

            Texture safe = Ctx.Lib != null ? Ctx.Lib.safeSign : null;
            if (safe != null)
            {
                sign = ModelUtil.TextureQuad(transform, safe, new Vector3(0f, 2.6f, 0f), new Vector2(0.5f, 0.5f), false);
            }
            else
            {
                sign = ModelUtil.Primitive(PrimitiveType.Quad, transform, new Vector3(0f, 2.6f, 0f),
                                           new Vector3(0.5f, 0.5f, 1f), new Color(0.1f, 0.7f, 0.25f), false);
            }

            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.1f, 0f);
            col.size = new Vector3(1.8f, 2.4f, 0.4f);
        }

        public override void OnInteract(Player player)
        {
            if (blocked) Ctx.Bus.Publish(new WarningEvent("This exit is blocked. Find another way out."));
            else Ctx.Bus.Publish(new WarningEvent("This exit is clear."));
        }

        public override void Tick(float dt, float time, Player player)
        {
            warnCooldown -= dt;
            if (!blocked && blockAt > 0f && time >= blockAt) SetBlocked(time);

            if (blocked && warnCooldown <= 0f && FlatDistance(player) < 1.2f)
            {
                warnCooldown = 4f;
                Ctx.Bus.Publish(new WarningEvent("Exit blocked! Turn back and find another exit.", "beep"));
            }
        }

        private void SetBlocked(float time)
        {
            blocked = true;
            if (opening != null) opening.sharedMaterial.color = BlockedColor;

            Texture danger = Ctx.Lib != null ? Ctx.Lib.dangerSign : null;
            if (sign != null)
            {
                Renderer r = sign.GetComponent<Renderer>();
                if (danger != null) r.sharedMaterial.mainTexture = danger;
                else r.sharedMaterial.color = new Color(0.9f, 0.1f, 0.1f);
            }

            if (Ctx.Lib != null && Ctx.Lib.rubblePrefab != null && Ctx.Pool != null)
            {
                rubble = Ctx.Pool.Get(Ctx.Lib.rubblePrefab, transform);
                rubblePooled = true;
                rubble.transform.localPosition = Vector3.zero;
                rubble.transform.localRotation = Quaternion.identity;
                rubble.SetActive(true);
                rubbleBaseScale = rubble.transform.localScale;
                FitRubble(rubble);
            }
            else
            {
                rubble = ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(0f, 0.5f, -0.2f),
                                             new Vector3(1.6f, 1.0f, 0.6f), new Color(0.35f, 0.3f, 0.28f), false);
                rubblePooled = false;
            }

            Ctx.Bus.Publish(new ExitBlockedEvent(Id, time));
            Ctx.Bus.Publish(new WarningEvent("The main exit is blocked by fire!"));
        }

        private void FitRubble(GameObject r)
        {
            Bounds b = ModelUtil.GetBounds(r);
            float size = Mathf.Max(b.size.x, b.size.z);
            if (size > 0.0001f) r.transform.localScale = r.transform.localScale * (1.8f / size);
            b = ModelUtil.GetBounds(r);
            r.transform.position += new Vector3(transform.position.x - b.center.x, transform.position.y - b.min.y, transform.position.z - b.center.z);
        }

        public override void ResetState()
        {
            blocked = false;
            warnCooldown = 0f;
            if (opening != null) opening.sharedMaterial.color = OpenColor;

            Texture safe = Ctx.Lib != null ? Ctx.Lib.safeSign : null;
            if (sign != null)
            {
                Renderer r = sign.GetComponent<Renderer>();
                if (safe != null) r.sharedMaterial.mainTexture = safe;
                else r.sharedMaterial.color = new Color(0.1f, 0.7f, 0.25f);
            }
            ReleaseRubble();
        }

        public override void OnReleased() { ReleaseRubble(); }

        private void ReleaseRubble()
        {
            if (rubble == null) return;
            if (rubblePooled && Ctx.Pool != null)
            {
                rubble.transform.localScale = rubbleBaseScale;
                Ctx.Pool.Release(rubble);
            }
            else Destroy(rubble);
            rubble = null;
        }
    }
}
