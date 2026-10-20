using UnityEngine;

namespace Doom
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        AudioSource[] pool;
        int next;
        AudioSource ui;
        AudioSource music;

        void Awake()
        {
            I = this;
            pool = new AudioSource[32];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 3f;
                s.maxDistance = 45f;
                s.dopplerLevel = 0f;
                pool[i] = s;
            }

            ui = gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = false;
            ui.spatialBlend = 0f;
            ui.ignoreListenerPause = true;

            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.ignoreListenerPause = true;
        }

        void Update()
        {
            music.volume = Settings.MusicVolume;
        }

        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (!clip) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.transform.position = position;
            s.clip = clip;
            s.volume = volume * Settings.SfxVolume;
            s.pitch = pitch * Random.Range(0.96f, 1.04f);
            s.Play();
        }

        public void PlayAt(string name, Vector3 position, float volume = 1f, float pitch = 1f) =>
            PlayAt(GameAssets.Sound(name), position, volume, pitch);

        /// <summary>Non-positional sound (player weapon, pickups, menus). Plays through pause.</summary>
        public void Play2D(string name, float volume = 1f, float pitch = 1f)
        {
            var clip = GameAssets.Sound(name);
            if (!clip) return;
            ui.pitch = pitch;
            ui.PlayOneShot(clip, volume * Settings.SfxVolume);
        }

        public void PlayMusic(string name)
        {
            var clip = GameAssets.Music(name);
            if (music.clip == clip && (clip == null || music.isPlaying)) return;
            music.clip = clip;
            if (clip) music.Play();
            else music.Stop();
        }
    }
}
