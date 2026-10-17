using System;
using System.Collections.Generic;
using UnityEngine;

namespace Match3.Game
{
    public enum Sfx
    {
        Swap,
        Invalid,
        Match,
        Special,
        Rocket,
        Bomb,
        ColorBomb,
        Ice,
        Box,
        Win,
        Lose,
        Click
    }

    /// <summary>
    /// Sound effects synthesized from simple waves when the game starts, so there are no audio files.
    /// A few AudioSources are used round-robin so overlapping sounds can have different pitches.
    /// </summary>
    public sealed class SoundFx : MonoBehaviour
    {
        private const int Rate = 44100;
        private const string MutedKey = "match3.muted";

        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private readonly System.Random _noise = new System.Random(7);
        private AudioSource[] _sources;
        private int _next;
        private bool _muted;

        public bool Muted
        {
            get => _muted;
            set
            {
                _muted = value;
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            _muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            _sources = new AudioSource[8];
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i] = gameObject.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
            }
            BuildClips();
        }

        public void Play(Sfx sfx, float pitch = 1f, float volume = 1f)
        {
            if (_muted || !_clips.TryGetValue(sfx, out AudioClip clip)) return;
            AudioSource source = _sources[_next++ % _sources.Length];
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }

        // ---------- synthesis ----------

        private void BuildClips()
        {
            _clips[Sfx.Swap] = Make("swap", 0.08f, t => Sweep(500f, 820f, 0.08f, t) * Decay(t, 30f) * 0.35f);
            _clips[Sfx.Invalid] = Make("invalid", 0.2f, t => Square(t < 0.1f ? 220f : 165f, t) * Decay(t % 0.1f, 18f) * 0.16f);
            _clips[Sfx.Match] = Make("match", 0.16f, t => (Sine(880f, t) * 0.6f + Sine(1320f, t) * 0.25f) * Decay(t, 22f) * 0.5f);
            _clips[Sfx.Special] = Make("special", 0.3f, t => Sine(t < 0.1f ? 660f : t < 0.2f ? 880f : 1100f, t) * Decay(t % 0.1f, 14f) * 0.35f);
            _clips[Sfx.Rocket] = Make("rocket", 0.35f, t => (Sweep(300f, 1600f, 0.35f, t) * 0.35f + Noise() * 0.15f) * (1f - t / 0.35f));
            _clips[Sfx.Bomb] = Make("bomb", 0.5f, t => (Sine(70f, t) * 0.7f + Noise() * 0.5f * Decay(t, 10f)) * Decay(t, 6f) * 0.8f);
            _clips[Sfx.ColorBomb] = Make("colorbomb", 0.6f, t => (Sweep(400f, 1200f, 0.6f, t) + Sweep(600f, 1800f, 0.6f, t) * 0.5f) * 0.22f * (1f - t / 0.6f));
            _clips[Sfx.Ice] = Make("ice", 0.12f, t => Noise() * Decay(t, 40f) * 0.45f + Sine(2400f, t) * Decay(t, 50f) * 0.2f);
            _clips[Sfx.Box] = Make("box", 0.14f, t => (Sine(160f, t) * 0.7f + Noise() * 0.3f) * Decay(t, 35f) * 0.7f);
            _clips[Sfx.Win] = Make("win", 0.75f, t => Notes(t, 0.15f, 4f, 523f, 659f, 784f, 1046f));
            _clips[Sfx.Lose] = Make("lose", 0.65f, t => Notes(t, 0.2f, 5f, 392f, 330f, 262f));
            _clips[Sfx.Click] = Make("click", 0.04f, t => Sine(1200f, t) * Decay(t, 90f) * 0.4f);
        }

        private static AudioClip Make(string name, float seconds, Func<float, float> wave)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float fadeIn = Mathf.Clamp01(t / 0.004f);             // avoid clicks at the start
                float fadeOut = Mathf.Clamp01((seconds - t) / 0.01f);  // ... and at the end
                data[i] = Mathf.Clamp(wave(t) * fadeIn * fadeOut, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Sine(float frequency, float t) => Mathf.Sin(2f * Mathf.PI * frequency * t);
        private static float Square(float frequency, float t) => Mathf.Sign(Sine(frequency, t));
        private static float Decay(float t, float rate) => Mathf.Exp(-t * rate);

        // Frequency glides linearly from f0 to f1 over the given duration.
        private static float Sweep(float f0, float f1, float duration, float t) =>
            Mathf.Sin(2f * Mathf.PI * (f0 * t + (f1 - f0) * t * t / (2f * duration)));

        private float Noise() => (float)(_noise.NextDouble() * 2.0 - 1.0);

        // A little melody: each note lasts noteLength, the last one rings out.
        private static float Notes(float t, float noteLength, float decay, params float[] frequencies)
        {
            int index = Mathf.Min(frequencies.Length - 1, (int)(t / noteLength));
            float local = t - index * noteLength;
            float f = frequencies[index];
            return (Sine(f, t) * 0.7f + Sine(f * 2f, t) * 0.15f) * Decay(local, decay) * 0.35f;
        }
    }
}
