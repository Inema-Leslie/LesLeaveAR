using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace LesLeave
{
    /// <summary>
    /// What the game needs from "the AR layer". Two implementations exist: the real phone (AR Foundation)
    /// and a keyboard/mouse simulation so the drill can be tested on a PC.
    /// </summary>
    public interface IARProvider
    {
        Camera Camera { get; }
        bool IsTracking { get; }
        bool PlaneDetected { get; }
        void SetScanning(bool on);
        bool TryGetHoverPose(out Pose pose);
        Transform CreateAnchor(Pose pose);
        void Tick(float dt);
    }

    // ------------------------------------------------------------------------------------------ phone

    public class DeviceARProvider : IARProvider
    {
        private readonly Camera cam;
        private readonly ARPlaneManager planes;
        private readonly ARRaycastManager raycaster;
        private readonly bool anchorsAvailable;
        private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

        public DeviceARProvider(Camera cam, ARPlaneManager planes, ARRaycastManager raycaster, bool anchorsAvailable)
        {
            this.cam = cam;
            this.planes = planes;
            this.raycaster = raycaster;
            this.anchorsAvailable = anchorsAvailable;
            if (planes != null) planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
        }

        public Camera Camera { get { return cam; } }

        public bool IsTracking { get { return ARSession.state == ARSessionState.SessionTracking; } }

        public bool PlaneDetected { get { return planes != null && planes.trackables.count > 0; } }

        public void SetScanning(bool on)
        {
            // shows or hides the plane visuals; planes keep being tracked so placement stays accurate
            // plane visuals stay hidden (see Tick); nothing to toggle
        }

        public bool TryGetHoverPose(out Pose pose)
        {
            pose = default(Pose);
            if (raycaster == null) return false;
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (raycaster.Raycast(center, Hits, TrackableType.PlaneWithinPolygon) && Hits.Count > 0)
            {
                pose = Hits[0].pose;
                return true;
            }
            return false;
        }

        public Transform CreateAnchor(Pose pose)
        {
            GameObject go = new GameObject("ScenarioAnchor");
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (anchorsAvailable) go.AddComponent<ARAnchor>();
            return go.transform;
        }

        private float hideTimer;

        public void Tick(float dt)
        {
            // Floor planes stay active (placement raycasts need them) but are never drawn;
            // only the placement ring is shown. Renderers are switched off a few times a second.
            if (planes == null) return;
            hideTimer -= dt;
            if (hideTimer > 0f) return;
            hideTimer = 0.1f;
            foreach (ARPlane plane in planes.trackables)
            {
                ARPlaneMeshVisualizer viz = plane.GetComponent<ARPlaneMeshVisualizer>();
                if (viz != null && viz.enabled) viz.enabled = false;
                Renderer[] rs = plane.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++) if (rs[i].enabled) rs[i].enabled = false;
            }
        }
    }

    // ------------------------------------------------------------------------------------------ PC test mode

    /// <summary>
    /// Keyboard and mouse stand-in for a phone.
    /// W A S D / arrows = walk, Shift = run, hold right mouse = look, hold C = crouch, T = toggle tracking lost.
    /// </summary>
    public class SimulatedARProvider : IARProvider
    {
        private readonly Camera cam;
        private float yaw;
        private float pitch = 35f;
        private float eye = 1.6f;
        private float px;
        private float pz = -1.5f;
        private bool tracking = true;
        private bool scanning;
        private float scanTime;

        public SimulatedARProvider(Camera cam)
        {
            this.cam = cam;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
            BuildFloor();
            Apply();
        }

        public Camera Camera { get { return cam; } }
        public bool IsTracking { get { return tracking; } }
        public bool PlaneDetected { get { return scanTime > 1.5f; } }

        public void SetScanning(bool on)
        {
            scanning = on;
            if (on) scanTime = 0f;
        }

        public bool TryGetHoverPose(out Pose pose)
        {
            Vector3 o = cam.transform.position;
            Vector3 f = cam.transform.forward;
            Vector3 hit;
            if (f.y < -0.1f)
            {
                float t = -o.y / f.y;
                hit = o + f * t;
                Vector3 flat = hit - o;
                flat.y = 0f;
                if (flat.magnitude > 4f) hit = o + flat.normalized * 4f;
            }
            else
            {
                Vector3 flat = new Vector3(f.x, 0f, f.z).normalized;
                hit = o + flat * 2f;
            }
            hit.y = 0f;
            pose = new Pose(hit, Quaternion.identity);
            return true;
        }

        public Transform CreateAnchor(Pose pose)
        {
            GameObject go = new GameObject("ScenarioAnchor");
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            return go.transform;
        }

        public void Tick(float dt)
        {
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;

            if (m != null && m.rightButton.isPressed)
            {
                Vector2 d = m.delta.ReadValue();
                yaw += d.x * 0.12f;
                pitch = Mathf.Clamp(pitch - d.y * 0.12f, -80f, 80f);
            }

            if (k != null)
            {
                float rad = yaw * Mathf.Deg2Rad;
                Vector3 fwd = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
                Vector3 mv = Vector3.zero;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) mv += fwd;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) mv -= fwd;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) mv += right;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) mv -= right;
                float speed = k.leftShiftKey.isPressed ? 3.0f : 1.2f;
                if (mv.sqrMagnitude > 0.001f) mv = mv.normalized * speed * dt;
                px += mv.x;
                pz += mv.z;

                float targetEye = k.cKey.isPressed ? 0.95f : 1.6f;
                eye = Mathf.MoveTowards(eye, targetEye, 4f * dt);

                if (k.tKey.wasPressedThisFrame) tracking = !tracking;
            }

            if (scanning) scanTime += dt;
            Apply();
        }

        private void Apply()
        {
            cam.transform.SetPositionAndRotation(new Vector3(px, eye, pz), Quaternion.Euler(pitch, yaw, 0f));
        }

        private static void BuildFloor()
        {
            const int cells = 24;
            const int px = 16;
            Texture2D tex = new Texture2D(cells * px, cells * px, TextureFormat.RGBA32, false);
            Color a = new Color(0.30f, 0.33f, 0.38f);
            Color b = new Color(0.26f, 0.29f, 0.34f);
            Color line = new Color(0.45f, 0.5f, 0.56f);
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    bool edge = (x % px == 0) || (y % px == 0);
                    bool alt = ((x / px) + (y / px)) % 2 == 0;
                    tex.SetPixel(x, y, edge ? line : (alt ? a : b));
                }
            }
            tex.Apply();

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "SimulatedFloor";
            floor.transform.position = new Vector3(0f, -0.01f, 6f);
            floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            floor.transform.localScale = new Vector3(cells, cells, 1f);
            Collider c = floor.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.Destroy(c);
            Material mat = ModelUtil.FlatMaterial(Color.white);
            mat.mainTexture = tex;
            floor.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
