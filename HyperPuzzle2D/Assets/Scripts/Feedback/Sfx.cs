using UnityEngine;

namespace HyperPuzzle2D.Feedback
{
    /// <summary>
    /// Tiny procedural SFX bank. No audio assets in the project yet, so every clip is
    /// synthesised once at boot as a short PCM buffer and played through a pooled source.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        public static Sfx Instance { get; private set; }

        readonly AudioSource[] _sources = new AudioSource[4];
        int _sourceIndex;
        AudioClip _fire;
        AudioClip _hit;
        AudioClip _break;
        AudioClip _explosion;
        AudioClip _chain;
        AudioClip _clear;
        AudioClip _fail;
        AudioClip _ui;

        void Awake()
        {
            Instance = this;
            for (var i = 0; i < _sources.Length; i++)
            {
                _sources[i] = gameObject.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
                _sources[i].spatialBlend = 0f;
            }

            _fire = Tone("sfx_fire", 0.13f, 130f, 520f, 0.7f, NoiseMix.Soft);
            _hit = Tone("sfx_hit", 0.08f, 210f, 80f, 0.72f, NoiseMix.Hard);
            _break = Tone("sfx_break", 0.14f, 920f, 170f, 0.65f, NoiseMix.Hard);
            _explosion = Tone("sfx_explosion", 0.26f, 110f, 42f, 0.95f, NoiseMix.Hard);
            _chain = Tone("sfx_chain", 0.12f, 560f, 980f, 0.5f, NoiseMix.Soft);
            _clear = Chord("sfx_clear", 0.22f, new[] { 523f, 659f, 784f }, 0.45f);
            _fail = Tone("sfx_fail", 0.28f, 320f, 110f, 0.5f, NoiseMix.Soft);
            _ui = Tone("sfx_ui", 0.05f, 660f, 880f, 0.35f, NoiseMix.None);
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Fire() => Play(_fire, 0.9f, 1f);
        public void Hit(float strength) => Play(_hit, Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(strength)), Mathf.Lerp(1.15f, 0.82f, Mathf.Clamp01(strength)));
        public void Break(int chain) => Play(_break, Mathf.Clamp01(0.7f + chain * 0.05f), Mathf.Min(1.45f, 0.92f + chain * 0.07f));
        public void Explosion(int chain)
        {
            Play(_explosion, 1f, Mathf.Min(1.15f, 0.88f + chain * 0.03f));
            Play(_chain, 0.75f, Mathf.Min(1.55f, 0.95f + chain * 0.08f));
        }
        public void Cleared() => Play(_clear, 0.9f);
        public void Failed() => Play(_fail, 0.8f);
        public void Ui() => Play(_ui, 0.55f);

        void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null || !Meta.Progress.SfxEnabled)
            {
                return;
            }

            var source = _sources[_sourceIndex++ % _sources.Length];
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }

        enum NoiseMix
        {
            None,
            Soft,
            Hard,
        }

        /// <summary>One-shot sine with an exponential decay and optional noise bite.</summary>
        static AudioClip Tone(string name, float seconds, float startHz, float endHz, float gain, NoiseMix noise)
        {
            var rate = 22050;
            var samples = Mathf.CeilToInt(seconds * rate);
            var data = new float[samples];
            var rng = new System.Random(name.GetHashCode());

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)(samples - 1);
                var hz = Mathf.Lerp(startHz, endHz, t);
                var env = Mathf.Exp(-4.2f * t);
                var wave = Mathf.Sin(2f * Mathf.PI * hz * (i / (float)rate));

                if (noise == NoiseMix.Soft)
                {
                    wave = wave * 0.7f + ((float)rng.NextDouble() * 2f - 1f) * 0.3f;
                }
                else if (noise == NoiseMix.Hard)
                {
                    wave = wave * 0.35f + ((float)rng.NextDouble() * 2f - 1f) * 0.65f;
                    env = Mathf.Exp(-9f * t);
                }

                data[i] = wave * env * gain;
            }

            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Three staggered tones for a clear fanfare that still fits under a quarter second.</summary>
        static AudioClip Chord(string name, float seconds, float[] freqs, float gain)
        {
            var rate = 22050;
            var samples = Mathf.CeilToInt(seconds * rate);
            var data = new float[samples];

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)rate;
                var env = Mathf.Exp(-3.5f * (i / (float)(samples - 1)));
                var sum = 0f;
                for (var n = 0; n < freqs.Length; n++)
                {
                    var gate = t >= n * 0.04f ? 1f : 0f;
                    sum += Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * gate;
                }

                data[i] = (sum / freqs.Length) * env * gain;
            }

            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
