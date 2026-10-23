using UnityEngine;

namespace LesLeave
{
    /// <summary>The manual fire alarm. Tap it from within 2 m to raise the alarm.</summary>
    public class FireAlarm : SimEntity
    {
        private const float TapRange = 2f;

        private bool activated;
        private Light glow;

        protected override void Build()
        {
            // pole so the alarm is reachable without a wall
            ModelUtil.Primitive(PrimitiveType.Cube, transform, new Vector3(0f, 0.7f, 0f),
                                new Vector3(0.08f, 1.4f, 0.08f), new Color(0.35f, 0.35f, 0.38f), false);

            GameObject boxRoot = new GameObject("AlarmBox");
            boxRoot.transform.SetParent(transform, false);
            boxRoot.transform.localPosition = new Vector3(0f, 1.2f, 0f);

            GameObject visual = null;
            if (Ctx.Lib != null && Ctx.Lib.fireAlarmModel != null)
                visual = ModelUtil.InstantiateFitted(Ctx.Lib.fireAlarmModel, boxRoot.transform, 0.3f, false);
            if (visual == null)
                ModelUtil.Primitive(PrimitiveType.Cube, boxRoot.transform, new Vector3(0f, 0.12f, 0f),
                                    new Vector3(0.22f, 0.28f, 0.08f), new Color(0.85f, 0.1f, 0.1f), false);

            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.3f, 0f);
            col.size = new Vector3(0.7f, 0.7f, 0.7f);

            glow = ModelUtil.AddPointLight(transform, new Vector3(0f, 1.5f, -0.3f), Color.red, 4f);
        }

        public override void OnInteract(Player player)
        {
            if (activated) return;
            if (!InRange(player, TapRange, "fire alarm")) return;
            activated = true;
            Ctx.Bus.Publish(new AlarmActivatedEvent(Ctx.Now()));
            Ctx.Bus.Publish(new WarningEvent("Fire alarm activated."));
        }

        public override void Tick(float dt, float time, Player player)
        {
            if (activated && glow != null) glow.intensity = 1.5f + Mathf.Sin(time * 8f) * 1.2f;
        }

        public override void ResetState()
        {
            activated = false;
            if (glow != null) glow.intensity = 0f;
        }
    }
}
