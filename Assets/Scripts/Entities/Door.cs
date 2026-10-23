using UnityEngine;

namespace LesLeave
{
    /// <summary>A door the trainee can test for heat. Hot doors must not be opened.</summary>
    public class Door : SimEntity
    {
        private static readonly Color HotColor = new Color(1f, 0.45f, 0.05f);
        private static readonly Color CoolColor = new Color(0.2f, 0.5f, 1f);
        private static readonly Color IdleColor = new Color(0.75f, 0.75f, 0.75f);

        private bool hot;
        private bool inspected;
        private bool entered;
        private Renderer lampRenderer;
        private Light lampLight;

        public bool IsHot { get { return hot; } }

        protected override void Build()
        {
            hot = Def.hot;

            GameObject visual = null;
            if (Ctx.Lib != null && Ctx.Lib.doorPrefab != null)
                visual = ModelUtil.InstantiateFitted(Ctx.Lib.doorPrefab, transform, 2.1f, true);
            if (visual == null)
                ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(0f, 1.05f, 0f),
                                    new Vector3(1.0f, 2.1f, 0.08f), new Color(0.55f, 0.4f, 0.25f), false);

            // generous tap target
            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.05f, 0f);
            col.size = new Vector3(1.1f, 2.2f, 0.6f);

            GameObject lamp = ModelUtil.Primitive(PrimitiveType.Sphere, transform, new Vector3(0f, 2.4f, 0f),
                                                  Vector3.one * 0.2f, IdleColor, false);
            lampRenderer = lamp.GetComponent<Renderer>();
            lampLight = ModelUtil.AddPointLight(transform, new Vector3(0f, 2.2f, -0.4f), IdleColor, 3.5f);
        }

        public override void OnInteract(Player player)
        {
            if (!InRange(player, 3f, "door")) return;
            inspected = true;
            SetLamp(hot ? HotColor : CoolColor);
            Ctx.Bus.Publish(new DoorInspectedEvent(Id, hot, Ctx.Now()));
            Ctx.Bus.Publish(new WarningEvent(hot ? "This door is HOT. Do not open it!" : "This door is cool. It is safer to use."));
        }

        public override void Tick(float dt, float time, Player player)
        {
            if (!entered && FlatDistance(player) < 0.9f)
            {
                entered = true;
                Ctx.Bus.Publish(new DoorEnteredEvent(Id, hot, time));
                if (hot) Ctx.Bus.Publish(new WarningEvent("You opened a HOT door! Fire is behind it.", "beep"));
            }
        }

        public override void ResetState()
        {
            inspected = false;
            entered = false;
            if (lampRenderer != null) lampRenderer.sharedMaterial.color = IdleColor;
            if (lampLight != null) lampLight.intensity = 0f;
        }

        private void SetLamp(Color c)
        {
            if (lampRenderer != null) lampRenderer.sharedMaterial.color = c;
            if (lampLight != null)
            {
                lampLight.color = c;
                lampLight.intensity = 3f;
            }
        }
    }
}
