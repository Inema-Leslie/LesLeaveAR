using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace LesLeave
{
    /// <summary>
    /// The only class that knows about AR. It hides the difference between a real phone and the
    /// keyboard-and-mouse test mode, and tells everyone else through the EventBus.
    /// </summary>
    public class ARManager : MonoBehaviour
    {
        [Tooltip("Tick to use the keyboard/mouse test mode even on a phone.")]
        public bool forceSimulation;

        private GameManager gm;
        private IARProvider provider;
        private Transform anchor;
        private GameObject reticle;
        private bool showReticle;
        private bool lastTracking = true;

        public bool IsSimulated { get; private set; }
        public float FloorY { get; private set; }
        public Camera Camera { get { return provider != null ? provider.Camera : null; } }
        public Transform CameraTransform { get { return Camera != null ? Camera.transform : null; } }
        public bool IsTracking { get { return provider != null && provider.IsTracking; } }
        public bool PlaneDetected { get { return provider != null && provider.PlaneDetected; } }
        public Transform Anchor { get { return anchor; } }

        public void Init(GameManager g)
        {
            gm = g;

            Camera cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            IsSimulated = forceSimulation || Application.isEditor || !Application.isMobilePlatform;

            if (!IsSimulated)
            {
                ARPlaneManager planes = FindFirstObjectByType<ARPlaneManager>();
                ARRaycastManager raycaster = FindFirstObjectByType<ARRaycastManager>();
                bool anchors = FindFirstObjectByType<ARAnchorManager>() != null;
                if (planes == null || raycaster == null)
                {
                    Debug.LogError("AR objects missing from the scene. Run LesLeave > Build Drill Scene. Falling back to test mode.");
                    IsSimulated = true;
                }
                else
                {
                    provider = new DeviceARProvider(cam, planes, raycaster, anchors);
                }
            }

            if (IsSimulated)
            {
                SwitchOffRealAR(cam);
                provider = new SimulatedARProvider(cam);
            }

            BuildReticle();
        }

        /// <summary>In test mode the AR session and camera components are not needed; turn them off.</summary>
        private static void SwitchOffRealAR(Camera cam)
        {
            Transform oldRoot = cam.transform.root;
            bool wasChild = oldRoot != cam.transform;
            cam.transform.SetParent(null, true);
            if (wasChild) oldRoot.gameObject.SetActive(false);

            ARSession session = FindFirstObjectByType<ARSession>();
            if (session != null) session.gameObject.SetActive(false);

            Behaviour[] behaviours = cam.GetComponents<Behaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                string n = behaviours[i].GetType().Name;
                if (n == "ARCameraBackground" || n == "ARCameraManager" || n == "TrackedPoseDriver" || n == "ARPoseDriver")
                    behaviours[i].enabled = false;
            }
        }

        private void BuildReticle()
        {
            reticle = new GameObject("PlacementReticle");
            reticle.transform.SetParent(transform, false);
            ModelUtil.Primitive(PrimitiveType.Cylinder, reticle.transform, new Vector3(0f, 0f, 0f),
                                new Vector3(0.6f, 0.004f, 0.6f), new Color(0.2f, 0.9f, 1f, 0.6f), false);
            ModelUtil.Primitive(PrimitiveType.Cube, reticle.transform, new Vector3(0f, 0.01f, 0.45f),
                                new Vector3(0.06f, 0.01f, 0.6f), new Color(1f, 1f, 1f, 0.9f), false);
            reticle.SetActive(false);
        }

        public void SetScanning(bool on) { if (provider != null) provider.SetScanning(on); }

        public void ShowReticle(bool on)
        {
            showReticle = on;
            if (!on && reticle != null) reticle.SetActive(false);
        }

        /// <summary>Where the scenario would be placed right now (level, facing away from the user).</summary>
        public bool TryGetHoverPose(out Pose pose)
        {
            pose = default(Pose);
            if (provider == null) return false;
            Pose p;
            if (!provider.TryGetHoverPose(out p)) return false;

            Vector3 f = Vector3.ProjectOnPlane(CameraTransform.forward, Vector3.up);
            Quaternion rot = f.sqrMagnitude > 0.001f ? Quaternion.LookRotation(f.normalized, Vector3.up) : Quaternion.identity;
            pose = new Pose(p.position, rot);
            return true;
        }

        public Transform PlaceAnchor(Pose pose)
        {
            ClearAnchor();
            FloorY = pose.position.y;
            anchor = provider.CreateAnchor(pose);
            return anchor;
        }

        public void ClearAnchor()
        {
            if (anchor != null) Destroy(anchor.gameObject);
            anchor = null;
        }

        public void Tick(float dt)
        {
            if (provider == null) return;
            provider.Tick(dt);

            bool t = provider.IsTracking;
            if (t != lastTracking)
            {
                lastTracking = t;
                gm.Bus.Publish(new TrackingChangedEvent(t));
            }

            if (showReticle && reticle != null)
            {
                Pose p;
                if (TryGetHoverPose(out p))
                {
                    reticle.SetActive(true);
                    reticle.transform.SetPositionAndRotation(p.position + Vector3.up * 0.01f, p.rotation);
                }
                else reticle.SetActive(false);
            }
        }
    }
}
