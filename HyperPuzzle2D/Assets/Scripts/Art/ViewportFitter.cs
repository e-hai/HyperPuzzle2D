using UnityEngine;

namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// Frames the play field for portrait phones and keeps the backdrop full-bleed when the
    /// aspect changes (device model, editor resize, aspect dropdown).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ViewportFitter : MonoBehaviour
    {
        Camera _camera;
        Transform _backdrop;
        float _halfWidth;
        float _minOrthoSize;
        float _lastAspect = -1f;

        /// <summary>
        /// Fits the field <em>width</em> rather than fitting both axes. Portrait phones run from
        /// roughly 16:9 to 21:9, and fitting both axes would zoom out on the squarer ones until
        /// empty gutters appeared beside the field. Locking to width fills every phone edge to
        /// edge and keeps the cannon-to-target distance identical across devices; the extra
        /// height on taller phones is absorbed by supports authored to run off the frame.
        /// <paramref name="minOrthoSize"/> only guards non-phone aspects (a wide editor view),
        /// where it keeps the playable band on screen.
        /// </summary>
        public static float OrthoSizeFor(float aspect, float halfWidth, float minOrthoSize)
        {
            var safeAspect = aspect > 0.01f ? aspect : 0.45f;
            return Mathf.Max(minOrthoSize, halfWidth / safeAspect);
        }

        public void Configure(float halfWidth, float minOrthoSize, Transform backdrop)
        {
            _camera = GetComponent<Camera>();
            _halfWidth = halfWidth;
            _minOrthoSize = minOrthoSize;
            _backdrop = backdrop;
            Fit();
        }

        void LateUpdate()
        {
            if (_camera != null && !Mathf.Approximately(_camera.aspect, _lastAspect))
            {
                Fit();
            }
        }

        void Fit()
        {
            var aspect = _camera.aspect > 0.01f ? _camera.aspect : 0.45f;
            _lastAspect = _camera.aspect;
            _camera.orthographicSize = OrthoSizeFor(aspect, _halfWidth, _minOrthoSize);

            var viewHeight = _camera.orthographicSize * 2f;

            if (_backdrop != null)
            {
                _backdrop.localScale = new Vector3(viewHeight * aspect, viewHeight, 1f);
            }
        }
    }
}
