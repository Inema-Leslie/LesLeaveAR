using System.Collections.Generic;
using UnityEngine;

namespace LesLeave
{
    /// <summary>A fire. Appears at its activation time, grows, crackles, and hurts if you stand too close.</summary>
    public class FireVolume : SimEntity
    {
        private const float HeatRadius = 1.3f;

        private float activateAt;
        private bool active;
        private float grow;
        private GameObject fire;
        private bool firePooled;
        private Vector3 baseScale = Vector3.one;
        private AudioSource crackle;
        private Light glow;

        protected override void Build()
        {
            activateAt = Def.activateAt;

            if (Ctx.Lib != null && Ctx.Lib.fireBasePrefab != null && Ctx.Pool != null)
            {
                fire = Ctx.Pool.Get(Ctx.Lib.fireBasePrefab, transform);
                firePooled = true;
                fire.transform.localPosition = Vector3.zero;
                fire.transform.localRotation = Quaternion.identity;
                baseScale = fire.transform.localScale;
            }
            else
            {
                fire = ModelUtil.Primitive(PrimitiveType.Sphere, transform, new Vector3(0f, 0.4f, 0f),
                                           new Vector3(0.8f, 0.8f, 0.8f), new Color(1f, 0.45f, 0.05f, 0.85f), false);
                firePooled = false;
                baseScale = fire.transform.localScale;
                fire.SetActive(false);
            }

            crackle = gameObject.AddComponent<AudioSource>();
            crackle.clip = Ctx.Lib != null ? Ctx.Lib.crackleClip : null;
            crackle.loop = true;
            crackle.playOnAwake = false;
            crackle.spatialBlend = 1f;
            crackle.minDistance = 1f;
            crackle.maxDistance = 9f;
            crackle.volume = 0.8f;

            glow = ModelUtil.AddPointLight(transform, new Vector3(0f, 1f, 0f), new Color(1f, 0.5f, 0.15f), 5f);

            SetVisible(false);
            if (activateAt <= 0f) Show();   // fires that burn from the start are visible while briefing
        }

        public override void Tick(float dt, float time, Player player)
        {
            if (!active && time >= activateAt) Show();
            if (!active) return;

            grow = Mathf.Clamp01(grow + dt / 3f);
            fire.transform.localScale = baseScale * Mathf.Lerp(0.35f, 1f, grow);
            glow.intensity = (1.5f + Mathf.Sin(time * 11f) * 0.4f) * grow;

            if (crackle.clip != null && !crackle.isPlaying) crackle.Play();
            if (grow > 0.5f && FlatDistance(player) < HeatRadius) player.nearFire = true;
        }

        private void Show()
        {
            active = true;
            grow = activateAt <= 0f ? 1f : 0f;
            SetVisible(true);
        }

        private void SetVisible(bool on)
        {
            if (fire != null) fire.SetActive(on);
            if (glow != null) glow.enabled = on;
            if (!on && crackle != null) crackle.Stop();
        }

        public override void ResetState()
        {
            active = false;
            grow = 0f;
            if (fire != null) fire.transform.localScale = baseScale;
            SetVisible(false);
            if (activateAt <= 0f) Show();
        }

        public override void OnReleased()
        {
            if (crackle != null) crackle.Stop();
            if (fire != null && firePooled && Ctx.Pool != null)
            {
                fire.transform.localScale = baseScale;
                Ctx.Pool.Release(fire);
                fire = null;
            }
        }
    }

    /// <summary>
    /// The smoke layer fills the space from the ceiling down to Bottom. While it is active, a device held
    /// higher than Bottom is "in the smoke". Crouch below it to stay safe.
    /// </summary>
    public class SmokeLayer : SimEntity
    {
        public HazardDef Hazard;

        public bool Active { get; private set; }
        public float Bottom { get; private set; }

        private GameObject plane;
        private readonly List<GameObject> puffs = new List<GameObject>();
        private float lastPublished = -999f;

        protected override void Build()
        {
            if (Hazard == null)
            {
                Hazard = new HazardDef();
                Hazard.start = 30f; Hazard.over = 30f; Hazard.from = 2.4f; Hazard.to = 1.3f;
            }
            Bottom = Hazard.from;

            float s = Ctx.Scale;
            plane = ModelUtil.Primitive(PrimitiveType.Quad, transform, new Vector3(-0.5f * s, Bottom, 6f * s),
                                        new Vector3(13f * s, 17f * s, 1f), new Color(0.35f, 0.35f, 0.38f, 0.28f), false);
            plane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            plane.SetActive(false);

            if (Ctx.Lib != null && Ctx.Lib.smokePrefab != null && Ctx.Pool != null)
            {
                float[] xs = { -3f, 3f };
                float[] zs = { 2f, 6f, 10f };
                for (int xi = 0; xi < xs.Length; xi++)
                {
                    for (int zi = 0; zi < zs.Length; zi++)
                    {
                        GameObject p = Ctx.Pool.Get(Ctx.Lib.smokePrefab, transform);
                        p.transform.localPosition = new Vector3(xs[xi] * s, Bottom + 0.6f, zs[zi] * s);
                        puffs.Add(p);
                    }
                }
            }
        }

        public override void Tick(float dt, float time, Player player)
        {
            if (!Active)
            {
                if (time < Hazard.start) return;
                Active = true;
                plane.SetActive(true);
                for (int i = 0; i < puffs.Count; i++) puffs[i].SetActive(true);
                Ctx.Bus.Publish(new WarningEvent("Smoke is filling the space. Crouch below the smoke layer!", "beep"));
            }

            float k = Hazard.over <= 0f ? 1f : Mathf.Clamp01((time - Hazard.start) / Hazard.over);
            Bottom = Mathf.Lerp(Hazard.from, Hazard.to, k);

            Vector3 pp = plane.transform.localPosition;
            pp.y = Bottom;
            plane.transform.localPosition = pp;
            for (int i = 0; i < puffs.Count; i++)
            {
                Vector3 q = puffs[i].transform.localPosition;
                q.y = Bottom + 0.6f;
                puffs[i].transform.localPosition = q;
            }

            if (Mathf.Abs(Bottom - lastPublished) > 0.02f)
            {
                lastPublished = Bottom;
                Ctx.Bus.Publish(new SmokeChangedEvent(true, Bottom));
            }
        }

        public override void ResetState()
        {
            Active = false;
            Bottom = Hazard.from;
            lastPublished = -999f;
            if (plane != null) plane.SetActive(false);
            for (int i = 0; i < puffs.Count; i++) puffs[i].SetActive(false);
            Ctx.Bus.Publish(new SmokeChangedEvent(false, 99f));
        }

        public override void OnReleased()
        {
            for (int i = 0; i < puffs.Count; i++)
            {
                if (Ctx.Pool != null) Ctx.Pool.Release(puffs[i]);
            }
            puffs.Clear();
        }
    }
}
