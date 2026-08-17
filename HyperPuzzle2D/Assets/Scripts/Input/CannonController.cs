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

        // Tuned to the gap between cannon and shelf: the weakest pull still reaches the near edge
        // of the stack, the strongest clears it without flying off screen.
        [SerializeField] float minPower = 8f;
        [SerializeField] float maxPower = 18f;
        [SerializeField] float maxPull = 2.5f;
        [SerializeField] int previewDots = 22;
        [SerializeField] float previewStep = 0.05f;

        /// <summary>How far above the horizon the flattest legal shot sits.</summary>
        const float MinElevationDegrees = 5f;

        public bool CanFire { get; set; } = true;

        public event System.Action<Vector2> Fired;

        Camera _camera;
        Transform _previewRoot;
        SpriteRenderer[] _previewDots;
        SpriteRenderer _chargeRing;
        bool _dragging;
        bool _waitingForPointerRelease;
        Vector3 _pull;
        Vector3 _dragStartWorld;
        Vector3 _restPosition;
        float _recoil;

        public Vector3 MuzzlePosition => transform.position + transform.up * MuzzleOffset;

        public void ArmAfterPointerRelease()
        {
            CanFire = true;
            _dragging = false;
            _waitingForPointerRelease = true;
            if (_previewRoot != null)
            {
                _previewRoot.gameObject.SetActive(false);
            }
        }

        void Awake()
        {
            _camera = Camera.main;
            _restPosition = transform.position;
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

            var charge = new GameObject("ChargeRing");
            charge.transform.SetParent(_previewRoot, false);
            _chargeRing = charge.AddComponent<SpriteRenderer>();
            _chargeRing.sprite = Shapes.Circle;
            _chargeRing.sortingOrder = SortingOrders.AimGuide;

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

            // Menu/result buttons enable the cannon from a pointer-up callback. Ignore that
            // transition frame so the UI gesture cannot leak through as an immediate shot.
            if (_waitingForPointerRelease)
            {
                if (!UnityEngine.Input.GetMouseButton(0) && UnityEngine.Input.touchCount == 0)
                {
                    _waitingForPointerRelease = false;
                }

                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                // The HUD overlays the playfield, so a tap on its buttons must not start an aim.
                if (PointerOverUi())
                {
                    return;
                }

                _dragging = true;
                _dragStartWorld = PointerWorld();
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

        static bool PointerOverUi()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null)
            {
                return false;
            }

            if (UnityEngine.Input.touchCount > 0)
            {
                return events.IsPointerOverGameObject(UnityEngine.Input.GetTouch(0).fingerId);
            }

            return events.IsPointerOverGameObject();
        }

        void UpdateAim()
        {
            var pointer = PointerWorld();
            // Gesture-relative slingshot: start anywhere, drag down-left, release. The old
            // cannon-relative vector required dragging beyond the phone's left edge because the
            // cannon itself sits there, making reliable horizontal shots physically impossible.
            var delta = _dragStartWorld - pointer;
            if (delta.magnitude > maxPull)
            {
                delta = delta.normalized * maxPull;
            }

            _pull = ClampToUpperHalf(delta);

            // A pull too small to aim with leaves the barrel where the player last put it, rather
            // than snapping it somewhere arbitrary the moment a finger touches down.
            if (_pull.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_pull.y, _pull.x) * Mathf.Rad2Deg - 90f);
            }

            DrawPreview(ComputeVelocity());
        }

        /// <summary>
        /// Aiming is limited to the upper half turn. The cannon stands on a plinth, so every angle
        /// below the horizon fires into it, and the barrel visibly pointing at the floor reads as
        /// a broken control rather than a bad shot.
        /// Clamping by angle instead of flattening the vector matters: flattening also destroyed
        /// the pull length, which dropped the shot under the minimum charge and turned an
        /// over-dragged aim into a dead vertical preview and no shot at all.
        /// </summary>
        static Vector2 ClampToUpperHalf(Vector2 pull)
        {
            var magnitude = pull.magnitude;
            if (magnitude < 1e-4f)
            {
                return Vector2.zero;
            }

            var minSin = Mathf.Sin(MinElevationDegrees * Mathf.Deg2Rad);
            if (pull.y >= magnitude * minSin)
            {
                return pull;
            }

            // Ride the horizon on the side the player pulled toward, at full strength. A pull with
            // no horizontal intent at all defaults downrange instead of firing off the back.
            var side = pull.x < 0f ? -1f : 1f;
            var cos = Mathf.Cos(MinElevationDegrees * Mathf.Deg2Rad);
            return new Vector2(side * magnitude * cos, magnitude * minSin);
        }

        Vector3 PointerWorld()
        {
            var pointer = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            pointer.z = 0f;
            return pointer;
        }

        void DrawPreview(Vector2 velocity)
        {
            _previewRoot.gameObject.SetActive(true);

            var origin = MuzzlePosition;
            var gravity = Physics2D.gravity * Projectile.GravityScale;
            var charge = Mathf.Clamp01(_pull.magnitude / maxPull);
            var tint = Color.Lerp(Palette.AccentCool, Palette.Accent, charge);
            _chargeRing.transform.position = origin;
            _chargeRing.transform.localScale = Vector3.one * Mathf.Lerp(0.24f, 0.58f, charge);
            _chargeRing.color = new Color(tint.r, tint.g, tint.b, 0.34f);

            // Below the firing threshold there is no shot to preview. Integrating a zero velocity
            // would draw the ball's free fall as a line dropping off the bottom of the screen,
            // which looks exactly like the cannon is aimed at the floor.
            if (velocity.sqrMagnitude <= 0.01f)
            {
                foreach (var dot in _previewDots)
                {
                    dot.gameObject.SetActive(false);
                }

                return;
            }

            var previous = (Vector2)origin;
            var blocked = false;
            for (var i = 0; i < _previewDots.Length; i++)
            {
                var t = (i + 1) * previewStep;
                var point = origin + (Vector3)(velocity * t) + (Vector3)(0.5f * t * t * gravity);

                var fade = 1f - i / (float)_previewDots.Length;
                var dot = _previewDots[i];
                if (blocked)
                {
                    dot.gameObject.SetActive(false);
                    continue;
                }

                dot.gameObject.SetActive(true);
                var segment = (Vector2)point - previous;
                var hit = FirstTargetHit(previous, segment);
                if (hit.collider != null)
                {
                    point = hit.point;
                    blocked = true;
                    fade = 1f;
                }

                dot.transform.position = point;
                dot.transform.localScale = Vector3.one * (blocked ? 0.28f : Mathf.Lerp(0.06f, 0.17f, fade));
                dot.color = blocked ? Palette.Projectile : new Color(tint.r, tint.g, tint.b, fade * 0.85f);
                previous = point;
            }
        }

        static RaycastHit2D FirstTargetHit(Vector2 origin, Vector2 segment)
        {
            if (segment.sqrMagnitude <= 0.0001f)
            {
                return default;
            }

            var hits = Physics2D.CircleCastAll(origin, 0.22f, segment.normalized, segment.magnitude);
            var best = default(RaycastHit2D);
            var bestDistance = float.PositiveInfinity;
            foreach (var hit in hits)
            {
                var block = hit.collider != null ? hit.collider.GetComponent<DestructibleBlock>() : null;
                if (block == null || block.IsCleared || hit.distance >= bestDistance)
                {
                    continue;
                }

                best = hit;
                bestDistance = hit.distance;
            }

            return best;
        }

        public void Kick()
        {
            _recoil = 0.22f;
        }

        void LateUpdate()
        {
            _recoil = Mathf.MoveTowards(_recoil, 0f, Time.unscaledDeltaTime * 1.8f);
            transform.position = _restPosition - transform.up * _recoil;
        }

        Vector2 ComputeVelocity()
        {
            var charge = Mathf.Clamp01(_pull.magnitude / maxPull);
            if (charge < 0.06f)
            {
                return Vector2.zero;
            }

            var shapedCharge = charge * charge * (3f - 2f * charge);
            return _pull.normalized * Mathf.Lerp(minPower, maxPower, shapedCharge);
        }
    }
}
