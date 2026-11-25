using System.Collections.Generic;
using UnityEngine;

namespace LuckySeven.Slot
{
    public enum SoundCue
    {
        Button,
        Spin,
        ReelStop,
        Win,
        Jackpot,
        FreeSpin
    }

    /// <summary>
    /// Lightweight procedural sound bank. No external audio files were supplied,
    /// so short tones are synthesised at runtime and played through one shared
    /// AudioSource. This keeps the Sounds folder optional and the build small.
    /// </summary>
    public sealed class AudioManager
    {
        private const int SampleRate = 44100;
        private readonly Dictionary<SoundCue, AudioClip> clips = new Dictionary<SoundCue, AudioClip>();
        private AudioSource source;

        [Range(0f, 1f)] public float Volume = 0.35f;
        public bool Muted { get; set; }

        public void Initialize(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            clips[SoundCue.Button] = CreateTone("click", 520f, 0.06f, 0.25f, 520f);
            clips[SoundCue.Spin] = CreateTone("spin", 180f, 0.35f, 0.22f, 420f);
            clips[SoundCue.ReelStop] = CreateTone("stop", 260f, 0.09f, 0.30f, 120f);
            clips[SoundCue.Win] = CreateArpeggio("win", new[] { 523f, 659f, 784f }, 0.11f);
            clips[SoundCue.Jackpot] = CreateArpeggio("jackpot", new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.13f);
            clips[SoundCue.FreeSpin] = CreateArpeggio("bonus", new[] { 880f, 1175f }, 0.12f);
        }

        public void Play(SoundCue cue)
        {
            if (Muted || source == null || !clips.ContainsKey(cue)) return;
            source.PlayOneShot(clips[cue], Volume);
        }

        private static AudioClip CreateTone(string name, float startFrequency, float duration,
                                            float amplitude, float endFrequency)
        {
            int count = Mathf.CeilToInt(SampleRate * duration);
            float[] data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += frequency / SampleRate;
                float envelope = Mathf.Sin(Mathf.PI * t); // soft attack / release
                data[i] = Mathf.Sin(phase * Mathf.PI * 2f) * envelope * amplitude;
            }
            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateArpeggio(string name, float[] notes, float noteDuration)
        {
            int perNote = Mathf.CeilToInt(SampleRate * noteDuration);
            int total = perNote * notes.Length;
            float[] data = new float[total];
            for (int n = 0; n < notes.Length; n++)
            {
                float phase = 0f;
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)perNote;
                    phase += notes[n] / SampleRate;
                    float envelope = Mathf.Sin(Mathf.PI * t);
                    data[n * perNote + i] = Mathf.Sin(phase * Mathf.PI * 2f) * envelope * 0.24f;
                }
            }
            AudioClip clip = AudioClip.Create(name, total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
