using UnityEngine;

namespace HyperPuzzle2D.Feedback
{
    /// <summary>
    /// Decaying positional shake. Runs in LateUpdate so it always wins over camera placement.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        const float Decay = 6f;
        const float Frequency = 28f;

        Vector3 _restPosition;
        float _amplitude;
        float _seed;

        void Awake()
        {
            Instance = this;
            _restPosition = transform.position;
            _seed = Random.value * 100f;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Re-reads the camera's resting position; call after moving the camera directly.</summary>
        public void SyncRestPosition()
        {
            _restPosition = transform.position;
        }

        public void Shake(float amplitude)
        {
            _amplitude = Mathf.Max(_amplitude, amplitude);
        }

        void LateUpdate()
        {
            if (_amplitude <= 0.0005f)
            {
                _amplitude = 0f;
                transform.position = _restPosition;
                return;
            }

            var time = Time.unscaledTime * Frequency;
            var offset = new Vector3(
                (Mathf.PerlinNoise(_seed, time) - 0.5f) * 2f,
                (Mathf.PerlinNoise(_seed + 13f, time) - 0.5f) * 2f,
                0f);

            transform.position = _restPosition + offset * _amplitude;
            _amplitude = Mathf.Lerp(_amplitude, 0f, Decay * Time.unscaledDeltaTime);
        }
    }
}
