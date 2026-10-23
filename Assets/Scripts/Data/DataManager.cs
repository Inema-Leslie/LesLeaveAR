using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace LesLeave
{
    /// <summary>Repository: the rest of the game never knows where or how sessions are stored.</summary>
    public interface SessionRepository
    {
        void Save(Session s);
        List<Session> FindAll();
    }

    /// <summary>Stores one JSON file per session in the app-private folder.</summary>
    public class JsonSessionRepository : SessionRepository
    {
        private readonly string dir;

        public JsonSessionRepository()
        {
            dir = Path.Combine(Application.persistentDataPath, "sessions");
            try { Directory.CreateDirectory(dir); }
            catch (Exception e) { Debug.LogWarning("Could not create sessions folder: " + e.Message); }
        }

        public void Save(Session s)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) return;
            try
            {
                string target = Path.Combine(dir, s.id + ".json");
                string tmp = target + ".tmp";
                // write to a temporary file first, then replace, so a crash can never corrupt the previous save
                File.WriteAllText(tmp, JsonUtility.ToJson(s, true));
                File.Copy(tmp, target, true);
                File.Delete(tmp);
            }
            catch (Exception e)
            {
                Debug.LogError("Could not save session: " + e.Message);
            }
        }

        public List<Session> FindAll()
        {
            List<Session> result = new List<Session>();
            try
            {
                if (!Directory.Exists(dir)) return result;
                string[] files = Directory.GetFiles(dir, "*.json");
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        Session s = JsonUtility.FromJson<Session>(File.ReadAllText(files[i]));
                        if (s != null && !string.IsNullOrEmpty(s.id)) result.Add(s);
                    }
                    catch (Exception) { /* skip unreadable file */ }
                }
                result.Sort((a, b) => string.Compare(b.startTime, a.startTime, StringComparison.Ordinal));
            }
            catch (Exception e)
            {
                Debug.LogError("Could not list sessions: " + e.Message);
            }
            return result;
        }
    }

    /// <summary>Loads scenarios, saves sessions. The only class that touches storage.</summary>
    public class DataManager : MonoBehaviour
    {
        // Add a file name here (without .json) to make a new scenario appear in the menu.
        private static readonly string[] ScenarioFiles = { "S1-corridor" };

        public readonly List<ScenarioData> Scenarios = new List<ScenarioData>();
        public bool Loaded { get; private set; }

        private SessionRepository repo;

        public void Init(GameManager gm)
        {
            repo = new JsonSessionRepository();
        }

        public IEnumerator LoadScenarios()
        {
            Scenarios.Clear();
            for (int i = 0; i < ScenarioFiles.Length; i++)
            {
                string path = Path.Combine(Application.streamingAssetsPath, "scenarios");
                path = path + "/" + ScenarioFiles[i] + ".json";
                string text = null;

                if (path.Contains("://"))
                {
                    // Android: StreamingAssets lives inside the apk, so it has to be read with a web request
                    using (UnityWebRequest req = UnityWebRequest.Get(path))
                    {
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success) text = req.downloadHandler.text;
                    }
                }
                else if (File.Exists(path))
                {
                    text = File.ReadAllText(path);
                }

                ScenarioData data = Parse(text);
                if (data != null) Scenarios.Add(data);
                else Debug.LogWarning("Scenario file '" + ScenarioFiles[i] + "' missing or invalid; using the built-in copy.");
            }

            if (Scenarios.Count == 0) Scenarios.Add(DefaultScenario.S1());
            Loaded = true;
        }

        private static ScenarioData Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                ScenarioData d = JsonUtility.FromJson<ScenarioData>(text);
                if (d == null || string.IsNullOrEmpty(d.id)) return null;
                if (d.entities == null || d.entities.Count == 0) return null;
                if (d.timeLimits == null || d.timeLimits.Length < 3) d.timeLimits = new float[] { 300f, 300f, 300f };
                if (d.hazards == null) d.hazards = new List<HazardDef>();
                return d;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void SaveSession(Session s) { repo.Save(s); }

        public void AutoSave(Session s) { repo.Save(s); }

        public List<Session> ListSessions() { return repo.FindAll(); }
    }
}
