using System.Collections;
using UnityEngine;

namespace LesLeave
{
    /// <summary>Plays the alarm, voice prompts and warning beeps in response to events.</summary>
    public class AudioManager : MonoBehaviour
    {
        private GameManager gm;
        private AudioSource alarm;
        private AudioSource voice;
        private AudioSource sfx;
        private AudioClip beep, chime, startTone, winTone, failTone;

        public void Init(GameManager g)
        {
            gm = g;

            alarm = gameObject.AddComponent<AudioSource>();
            alarm.loop = true;
            alarm.playOnAwake = false;
            alarm.spatialBlend = 0f;
            alarm.volume = 0.6f;

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;

            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            sfx.volume = 0.7f;

            beep = MakeBeep();
            chime = MakeTones(new float[] { 660f, 880f }, 0.12f);
            startTone = MakeTones(new float[] { 523f }, 0.25f);
            winTone = MakeTones(new float[] { 523f, 659f, 784f, 1047f }, 0.16f);
            failTone = MakeTones(new float[] { 262f, 196f, 147f }, 0.28f);

            gm.Bus.Subscribe<AlarmActivatedEvent>(OnAlarm);
            gm.Bus.Subscribe<DoorInspectedEvent>(OnDoorInspected);
            gm.Bus.Subscribe<ExitBlockedEvent>(OnExitBlocked);
            gm.Bus.Subscribe<WarningEvent>(OnWarning);
            gm.Bus.Subscribe<DrillEndedEvent>(OnEnded);
            gm.Bus.Subscribe<DrillStartedEvent>(e => sfx.PlayOneShot(startTone));
            gm.Bus.Subscribe<DoorEnteredEvent>(e => { if (!e.Hot) sfx.PlayOneShot(chime); });
            gm.Bus.Subscribe<StairsEnteredEvent>(e => sfx.PlayOneShot(chime));
        }

        public void StopAll()
        {
            StopAllCoroutines();
            if (alarm != null) alarm.Stop();
            if (voice != null) voice.Stop();
            if (sfx != null) sfx.Stop();
            // drop the clip references so the loaded assets can be released
            if (alarm != null) alarm.clip = null;
            if (voice != null) voice.clip = null;
        }

        private void OnAlarm(AlarmActivatedEvent e)
        {
            alarm.clip = gm.Assets != null ? gm.Assets.alarmClip : null;
            if (alarm.clip != null) alarm.Play();
            sfx.PlayOneShot(chime);
        }

        private void OnDoorInspected(DoorInspectedEvent e)
        {
            if (e.Hot && gm.Assets != null) PlayVoice(gm.Assets.doorHotClip);
        }

        private void OnExitBlocked(ExitBlockedEvent e)
        {
            StartCoroutine(ExitSequence());
        }

        private IEnumerator ExitSequence()
        {
            AudioClip blocked = gm.Assets != null ? gm.Assets.exitBlockedClip : null;
            AudioClip go = gm.Assets != null ? gm.Assets.goAssemblyClip : null;
            if (blocked != null)
            {
                PlayVoice(blocked);
                yield return new WaitForSeconds(blocked.length + 0.4f);
            }
            if (go != null) PlayVoice(go);
        }

        private void OnWarning(WarningEvent e)
        {
            if (e.Voice == "beep") sfx.PlayOneShot(beep);
        }

        private void OnEnded(DrillEndedEvent e)
        {
            StopAll();
            sfx.PlayOneShot(e.Outcome == DrillOutcome.Completed ? winTone : failTone);
        }

        private void PlayVoice(AudioClip clip)
        {
            if (clip == null) return;
            voice.Stop();
            voice.clip = clip;
            voice.Play();
        }

        private static AudioClip MakeTones(float[] freqs, float each)
        {
            const int rate = 44100;
            int per = (int)(rate * each);
            float[] data = new float[per * freqs.Length];
            for (int k = 0; k < freqs.Length; k++)
            {
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)rate;
                    float env = Mathf.Min(1f, i / 300f) * Mathf.Min(1f, (per - i) / 1500f);
                    data[k * per + i] = Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * 0.35f * env;
                }
            }
            AudioClip c = AudioClip.Create("tone", data.Length, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip MakeBeep()
        {
            const int rate = 44100;
            int n = rate / 5;
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (n - i) / 400f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.4f * env;
            }
            AudioClip c = AudioClip.Create("beep", n, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
