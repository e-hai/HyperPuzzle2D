using UnityEngine;

namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// Keeps the authored play field fully visible and the backdrop full-bleed when the
    /// Game view aspect changes (device rotation, editor resize, aspect dropdown).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ViewportFitter : MonoBehaviour
    {
        Camera _camera;
        Transform _backdrop;
        Vector2 _halfExtents;
        float _lastAspect = -1f;

        public void Configure(Vector2 halfExtents, Transform backdrop)
        {
            _camera = GetComponent<Camera>();
            _halfExtents = halfExtents;
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
            var aspect = _camera.aspect > 0.01f ? _camera.aspect : 16f / 9f;
            _lastAspect = _camera.aspect;
            _camera.orthographicSize = Mathf.Max(_halfExtents.y, _halfExtents.x / aspect);

            if (_backdrop != null)
            {
                var height = _camera.orthographicSize * 2f;
                _backdrop.localScale = new Vector3(height * aspect, height, 1f);
            }
        }
    }
}
