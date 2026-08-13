using HyperPuzzle2D.Art;
using HyperPuzzle2D.Board;
using UnityEngine;

namespace HyperPuzzle2D.Input
{
    /// <summary>
    /// One-finger aim: drag to set angle/power, release to fire.
    /// The preview integrates the same gravity the projectile uses, so dots read as a real arc.
    /// </summary>
    public sealed class CannonController : MonoBehaviour
    {
        /// <summary>Distance from the pivot to the barrel tip, where shots originate.</summary>
        public const float MuzzleOffset = 1.2f;

        [SerializeField] float minPower = 6f;
        [SerializeField] float maxPower = 16f;
        [SerializeField] float maxPull = 2.5f;
        [SerializeField] int previewDots = 22;
        [SerializeField] float previewStep = 0.05f;

        public bool CanFire { get; set; } = true;

        public event System.Action<Vector2> Fired;

        Camera _camera;
        Transform _previewRoot;
        SpriteRenderer[] _previewDots;
        bool _dragging;
        Vector3 _pull;

        public Vector3 MuzzlePosition => transform.position + transform.up * MuzzleOffset;

        void Awake()
        {
            _camera = Camera.main;
            BuildPreview();
        }

        void BuildPreview()
        {
            _previewRoot = new GameObject("AimPreview").transform;
            _previewDots = new SpriteRenderer[previewDots];

            for (var i = 0; i < previewDots; i++)
            {
                var dot = new GameObject("Dot");
                dot.transform.SetParent(_previewRoot, false);

                var renderer = dot.AddComponent<SpriteRenderer>();
                renderer.sprite = Shapes.Circle;
                renderer.sortingOrder = SortingOrders.AimGuide;
                _previewDots[i] = renderer;
            }

            _previewRoot.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!CanFire)
            {
                if (_dragging)
                {
                    _dragging = false;
                    _previewRoot.gameObject.SetActive(false);
                }

                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _dragging = true;
                UpdateAim();
            }
            else if (_dragging && UnityEngine.Input.GetMouseButton(0))
            {
                UpdateAim();
            }
            else if (_dragging && UnityEngine.Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                _previewRoot.gameObject.SetActive(false);

                var velocity = ComputeVelocity();
                if (velocity.sqrMagnitude > 0.01f)
                {
                    Fired?.Invoke(velocity);
                }
            }
        }

        void UpdateAim()
        {
            var pointer = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            pointer.z = 0f;

            var delta = transform.position - pointer;
            if (delta.magnitude > maxPull)
            {
                delta = delta.normalized * maxPull;
            }

            // Bias upward so a drag straight across still produces a lobbing shot.
            if (delta.y < 0.2f)
            {
                delta.y = 0.2f;
            }

            _pull = delta;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_pull.y, _pull.x) * Mathf.Rad2Deg - 90f);

            DrawPreview(ComputeVelocity());
        }

        void DrawPreview(Vector2 velocity)
        {
            _previewRoot.gameObject.SetActive(true);

            var origin = MuzzlePosition;
            var gravity = Physics2D.gravity * Projectile.GravityScale;
            var charge = Mathf.Clamp01(_pull.magnitude / maxPull);
            var tint = Color.Lerp(Palette.AccentCool, Palette.Accent, charge);

            for (var i = 0; i < _previewDots.Length; i++)
            {
                var t = (i + 1) * previewStep;
                var point = origin + (Vector3)(velocity * t) + (Vector3)(0.5f * t * t * gravity);

                var fade = 1f - i / (float)_previewDots.Length;
                var dot = _previewDots[i];
                dot.transform.position = point;
                dot.transform.localScale = Vector3.one * Mathf.Lerp(0.06f, 0.17f, fade);
                dot.color = new Color(tint.r, tint.g, tint.b, fade * 0.85f);
            }
        }

        Vector2 ComputeVelocity()
        {
            var charge = Mathf.Clamp01(_pull.magnitude / maxPull);
            return _pull.normalized * Mathf.Lerp(minPower, maxPower, charge);
        }
    }
}
