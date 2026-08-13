using UnityEngine;

namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// Slow sine drift for background decoration, so the backdrop is never fully static.
    /// </summary>
    public sealed class FloatingMote : MonoBehaviour
    {
        Vector3 _origin;
        float _amplitude;
        float _speed;
        float _phase;

        public void Configure(float amplitude, float speed)
        {
            _origin = transform.position;
            _amplitude = amplitude;
            _speed = speed;
            _phase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            var offset = Mathf.Sin(Time.time * _speed + _phase) * _amplitude;
            transform.position = _origin + new Vector3(offset * 0.35f, offset, 0f);
        }
    }
}
