using System.Collections.Generic;
using UnityEngine;

namespace LesLeave
{
    /// <summary>Small helpers for building visuals from imported models or plain shapes.</summary>
    public static class ModelUtil
    {
        public static Bounds GetBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>
        /// Instantiates a model under parent, scales it so its height (or largest side) equals
        /// targetSize metres, and sits it on the parent's origin. Imported models come in all sizes,
        /// so this keeps everything the right size without hand-tuning.
        /// </summary>
        public static GameObject InstantiateFitted(GameObject prefab, Transform parent, float targetSize, bool useHeight)
        {
            if (prefab == null) return null;
            GameObject inst = GameObject.Instantiate(prefab, parent);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;

            Bounds b = GetBounds(inst);
            float size = useHeight ? b.size.y : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (size > 0.0001f) inst.transform.localScale = inst.transform.localScale * (targetSize / size);

            b = GetBounds(inst);
            Vector3 anchorPoint = new Vector3(b.center.x, b.min.y, b.center.z);
            inst.transform.position += parent.position - anchorPoint;
            return inst;
        }

        public static Material FlatMaterial(Color color)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            Material m = new Material(sh);
            m.color = color;
            return m;
        }

        public static GameObject Primitive(PrimitiveType type, Transform parent, Vector3 localPos,
                                           Vector3 localScale, Color color, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = FlatMaterial(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!keepCollider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) UnityEngine.Object.Destroy(c);
            }
            return go;
        }

        /// <summary>A quad showing a texture. flat = lying on the floor, otherwise standing upright.</summary>
        public static GameObject TextureQuad(Transform parent, Texture tex, Vector3 localPos, Vector2 size, bool flat)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.transform.SetParent(parent, false);
            q.transform.localPosition = localPos;
            q.transform.localRotation = flat ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            Renderer r = q.GetComponent<Renderer>();
            Material m = FlatMaterial(Color.white);
            m.mainTexture = tex;
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Collider c = q.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.Destroy(c);
            return q;
        }

        public static Light AddPointLight(Transform parent, Vector3 localPos, Color color, float range)
        {
            GameObject go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            return l;
        }
    }

    /// <summary>Re-uses effect objects (fire, smoke) instead of creating and destroying them, which avoids frame-rate stutter.</summary>
    public class PrefabPool
    {
        private readonly Transform root;
        private readonly Dictionary<GameObject, Stack<GameObject>> free = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly Dictionary<GameObject, GameObject> owner = new Dictionary<GameObject, GameObject>();

        public PrefabPool(Transform root) { this.root = root; }

        public GameObject Get(GameObject prefab, Transform parent)
        {
            GameObject obj = null;
            Stack<GameObject> stack;
            if (free.TryGetValue(prefab, out stack))
            {
                while (stack.Count > 0 && obj == null) obj = stack.Pop();
            }
            if (obj == null)
            {
                obj = GameObject.Instantiate(prefab);
                owner[obj] = prefab;
            }
            obj.transform.SetParent(parent, false);
            obj.SetActive(false);
            return obj;
        }

        /// <summary>Destroy every pooled object. Needed before the source assets are released.</summary>
        public void Clear()
        {
            foreach (KeyValuePair<GameObject, Stack<GameObject>> kv in free)
            {
                foreach (GameObject o in kv.Value) if (o != null) UnityEngine.Object.Destroy(o);
            }
            free.Clear();
            owner.Clear();
        }

        public void Release(GameObject obj)
        {
            if (obj == null) return;
            obj.SetActive(false);
            obj.transform.SetParent(root, false);
            GameObject prefab;
            if (owner.TryGetValue(obj, out prefab))
            {
                Stack<GameObject> stack;
                if (!free.TryGetValue(prefab, out stack))
                {
                    stack = new Stack<GameObject>();
                    free[prefab] = stack;
                }
                stack.Push(obj);
            }
            else
            {
                UnityEngine.Object.Destroy(obj);
            }
        }
    }
}
