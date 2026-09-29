// Plays the synthesised effects through a small voice pool (with per-sound rate limits) and
// crossfades the per-mall music loop, which is rendered on a worker thread.
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.Audio
{
    public sealed class AudioHub : MonoBehaviour
    {
        readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, float> lastPlay = new Dictionary<string, float>();
        AudioSource[] voices;
        int nextVoice;
        AudioSource musicA, musicB;
        bool aIsCurrent = true;
        float musicVol = 0.55f, sfxVol = 0.8f;
        float fade = 1;
        Task<float[]> musicTask;
        string musicKey = "", pendingKey = "";
        const int MusicRate = 32000;

        public void Init(float music, float sfx)
        {
            musicVol = music;
            sfxVol = sfx;
            voices = new AudioSource[14];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0;
            }
            musicA = gameObject.AddComponent<AudioSource>();
            musicB = gameObject.AddComponent<AudioSource>();
            foreach (var m in new[] { musicA, musicB }) { m.loop = true; m.playOnAwake = false; m.volume = 0; m.spatialBlend = 0; }

            Add("dig", Synth.Dig(1), Synth.Dig(2), Synth.Dig(3), Synth.Dig(4));
            Add("coin", Synth.Coin(1), Synth.Coin(2), Synth.Coin(3), Synth.Coin(4));
            Add("register", Synth.Register());
            Add("buy", Synth.Blip(7));
            Add("buy_big", Synth.Arp(new[] { 72, 76, 79, 84 }, 0.05f, 0.25f, 1.2f, 0.7f));
            Add("wish_spawn", Synth.WishSpawn());
            Add("wish_catch", Synth.WishCatch());
            Add("wish_escape", Synth.Whoosh(true));
            Add("compress", Synth.Whoosh(false));
            Add("relic", Synth.Fanfare(false));
            Add("cleared", Synth.Fanfare(true));
            Add("golden", Synth.Golden());
            Add("golden_spawn", Synth.Ting());
            Add("achievement", Synth.Achievement());
            Add("stratum", Synth.Rumble());
            Add("ui", Synth.UIClick());
            Add("whack", Synth.Clang());
            Add("squeak", Synth.Squeak());
            Add("denied", Synth.Denied());
            Add("event", Synth.Siren());
            Add("plop", Synth.Splash(1, false), Synth.Splash(2, false), Synth.Splash(3, false));
            Add("splash", Synth.Splash(4, true), Synth.Splash(5, true));
            Add("throw", Synth.Whoosh(false));
            Add("step", Synth.Footstep(1), Synth.Footstep(2), Synth.Footstep(3), Synth.Footstep(4), Synth.Footstep(5), Synth.Footstep(6));
            Add("whistle", Synth.Whistle());
            // the mall-only machines
            Add("pop", Synth.Pop());
            Add("reels", Synth.Reels());
            Add("jackpot", Synth.Jackpot());
            Add("drone", Synth.DroneWhirr());
            Add("well", Synth.WellChime());
        }

        void Add(string name, params float[][] buffers)
        {
            var arr = new AudioClip[buffers.Length];
            for (int i = 0; i < buffers.Length; i++)
            {
                var c = AudioClip.Create(name + i, buffers[i].Length, 1, Synth.Rate, false);
                c.SetData(buffers[i], 0);
                arr[i] = c;
            }
            clips[name] = arr;
        }

        public void SetVolumes(float music, float sfx)
        {
            musicVol = music;
            sfxVol = sfx;
        }

        /// <summary>Play an effect. minInterval stops rapid repeats from stacking into noise.</summary>
        public void Play(string name, float vol = 1f, float pitchVar = 0.06f, float minInterval = 0.035f, float pitch = 1f)
        {
            if (voices == null || sfxVol <= 0.001f || !clips.TryGetValue(name, out var arr)) return;
            float now = Time.unscaledTime;
            if (lastPlay.TryGetValue(name, out var last) && now - last < minInterval) return;
            lastPlay[name] = now;
            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            v.clip = arr[Random.Range(0, arr.Length)];
            v.volume = vol * sfxVol;
            v.panStereo = 0;
            v.pitch = pitch * (1 + Random.Range(-pitchVar, pitchVar));
            v.Play();
        }

        /// <summary>Play an effect from a place in the world: quieter with distance, panned left/right.</summary>
        public void PlayAt(string name, Vector3 pos, float vol = 1f, float pitchVar = 0.08f, float minInterval = 0.02f, float pitch = 1f)
        {
            var cam = Camera.main;
            if (cam == null) { Play(name, vol, pitchVar, minInterval, pitch); return; }
            Vector3 d = pos - cam.transform.position;
            float dist = d.magnitude;
            float att = 1f / (1f + dist * 0.12f + dist * dist * 0.004f);
            if (att * vol < 0.015f) return;
            float pan = dist > 0.01f ? Vector3.Dot(cam.transform.right, d / dist) * 0.75f : 0;
            if (voices == null || sfxVol <= 0.001f || !clips.TryGetValue(name, out var arr)) return;
            float now = Time.unscaledTime;
            if (lastPlay.TryGetValue(name, out var last) && now - last < minInterval) return;
            lastPlay[name] = now;
            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            v.clip = arr[Random.Range(0, arr.Length)];
            v.volume = vol * att * sfxVol;
            v.panStereo = pan;
            v.pitch = pitch * (1 + Random.Range(-pitchVar, pitchVar));
            v.Play();
        }

        public void PlayMusicFor(MallDef mall, int remodel)
        {
            var th = mall.Theme;
            string key = mall.Id + remodel;
            if (key == musicKey || key == pendingKey) return;
            pendingKey = key;
            int root = th.MusicRoot + (remodel % 3) - 1, mode = th.MusicMode, seed = mall.Id.GetHashCode() ^ remodel;
            float tempo = th.MusicTempo;
            musicTask = Task.Run(() => Synth.Music(root, tempo, mode, seed, MusicRate));
        }

        void Update()
        {
            if (musicTask != null && musicTask.IsCompleted)
            {
                if (musicTask.Status == TaskStatus.RanToCompletion)
                {
                    var buf = musicTask.Result;
                    var clip = AudioClip.Create("music " + pendingKey, buf.Length, 1, MusicRate, false);
                    clip.SetData(buf, 0);
                    var incoming = aIsCurrent ? musicB : musicA;
                    incoming.clip = clip;
                    incoming.volume = 0;
                    incoming.Play();
                    aIsCurrent = !aIsCurrent;
                    fade = 0;
                    musicKey = pendingKey;
                }
                musicTask = null;
            }
            fade = Mathf.Min(1, fade + Time.unscaledDeltaTime / 2.5f);
            var cur = aIsCurrent ? musicA : musicB;
            var old = aIsCurrent ? musicB : musicA;
            float target = musicVol * 0.45f;
            cur.volume = target * fade;
            old.volume = target * (1 - fade);
            if (fade >= 1 && old.isPlaying) old.Stop();
        }
    }
}
