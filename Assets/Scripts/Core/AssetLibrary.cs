using UnityEngine;

namespace LesLeave
{
    /// <summary>
    /// One place that holds every prefab, texture and audio clip the drill uses.
    /// Filled in automatically by LesLeave > Build Drill Scene. Anything left empty
    /// is replaced by a simple placeholder shape, so the drill still runs.
    /// </summary>
    public class AssetLibrary : MonoBehaviour
    {
        [Header("Models and effects")]
        public GameObject doorPrefab;
        public GameObject fireAlarmModel;
        public GameObject fireBasePrefab;
        public GameObject smokePrefab;
        public GameObject rubblePrefab;
        public GameObject extinguisherPrefab;

        [Header("Signs")]
        public Texture safeSign;
        public Texture dangerSign;

        [Header("Fonts")]
        public Font headingFont;
        public Font bodyFont;

        [Header("Audio")]
        public AudioClip alarmClip;
        public AudioClip crackleClip;
        public AudioClip doorHotClip;
        public AudioClip exitBlockedClip;
        public AudioClip goAssemblyClip;
    }
}
