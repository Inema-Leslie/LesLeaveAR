using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LesLeave
{
    /// <summary>
    /// Loads the drill's prefabs, signs and voice clips through Addressables when a drill is being set up,
    /// and releases them again when the player returns to the menu, so memory is only used while it is needed.
    /// </summary>
    public class AssetService : MonoBehaviour
    {
        private GameManager gm;
        private readonly List<AsyncOperationHandle> handles = new List<AsyncOperationHandle>();
        private bool loading;

        public bool Ready { get; private set; }
        public int LoadedCount { get { return handles.Count; } }

        public void Init(GameManager g) { gm = g; }

        /// <summary>Start loading (does nothing if already loaded or loading).</summary>
        public void Begin()
        {
            if (Ready || loading) return;
            StartCoroutine(LoadAll());
        }

        private IEnumerator LoadAll()
        {
            loading = true;
            AssetLibrary l = gm.Assets;
            if (l == null)
            {
                loading = false;
                Ready = true;
                yield break;
            }

            yield return Load<GameObject>("door", a => l.doorPrefab = a);
            yield return Load<GameObject>("fireAlarm", a => l.fireAlarmModel = a);
            yield return Load<GameObject>("fireBase", a => l.fireBasePrefab = a);
            yield return Load<GameObject>("smoke", a => l.smokePrefab = a);
            yield return Load<GameObject>("rubble", a => l.rubblePrefab = a);
            yield return Load<Texture2D>("safeSign", a => l.safeSign = a);
            yield return Load<Texture2D>("dangerSign", a => l.dangerSign = a);
            yield return Load<AudioClip>("alarm", a => l.alarmClip = a);
            yield return Load<AudioClip>("crackle", a => l.crackleClip = a);
            yield return Load<AudioClip>("doorHot", a => l.doorHotClip = a);
            yield return Load<AudioClip>("exitBlocked", a => l.exitBlockedClip = a);
            yield return Load<AudioClip>("goAssembly", a => l.goAssemblyClip = a);

            loading = false;
            Ready = true;
            Debug.Log("Addressables: loaded " + handles.Count + " assets.");
        }

        private IEnumerator Load<T>(string key, Action<T> assign) where T : UnityEngine.Object
        {
            AsyncOperationHandle<T> h = Addressables.LoadAssetAsync<T>(key);
            handles.Add(h);
            yield return h;
            if (h.Status == AsyncOperationStatus.Succeeded)
            {
                assign(h.Result);
            }
            else
            {
                Debug.LogWarning("Addressable '" + key + "' could not be loaded; a placeholder will be used.");
                handles.Remove(h);
                Addressables.Release(h);
            }
        }

        /// <summary>Give every loaded asset back. Call only when nothing in the scene still uses them.</summary>
        public void ReleaseAll()
        {
            StopAllCoroutines();
            loading = false;

            AssetLibrary l = gm != null ? gm.Assets : null;
            if (l != null)
            {
                l.doorPrefab = null;
                l.fireAlarmModel = null;
                l.fireBasePrefab = null;
                l.smokePrefab = null;
                l.rubblePrefab = null;
                l.extinguisherPrefab = null;
                l.safeSign = null;
                l.dangerSign = null;
                l.alarmClip = null;
                l.crackleClip = null;
                l.doorHotClip = null;
                l.exitBlockedClip = null;
                l.goAssemblyClip = null;
            }

            int n = handles.Count;
            for (int i = 0; i < handles.Count; i++)
            {
                if (handles[i].IsValid()) Addressables.Release(handles[i]);
            }
            handles.Clear();
            Ready = false;
            if (n > 0) Debug.Log("Addressables: released " + n + " assets.");
        }
    }
}
