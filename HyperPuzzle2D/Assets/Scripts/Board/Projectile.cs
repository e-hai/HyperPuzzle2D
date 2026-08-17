using UnityEngine;

namespace HyperPuzzle2D.Board
{
    public enum ProjectileKind
    {
        /// <summary>Plain steel ball. No special: pure aim.</summary>
        Ball,

        /// <summary>Breaks into three fragments, on tap or on its first impact.</summary>
        Cluster,

        /// <summary>Sticks where it lands and detonates, on tap or after a short fuse.</summary>
        Charge,

        /// <summary>One piece of a split cluster. Smaller, and cannot split again.</summary>
        Fragment,
    }

    /// <summary>
    /// A shot in flight. Kinds differ only in what a tap does mid-air; the owner supplies that
    /// behaviour through <c>onSpecial</c>, since splitting and detonating both need the world.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class Projectile : MonoBehaviour
    {
        /// <summary>Shared with the cannon's trajectory preview so the dots match the real arc.</summary>
        public const float GravityScale = 1.2f;

        /// <summary>Below the bottom of the tallest phone viewport, so shots leave view before despawning.</summary>
        const float OutOfPlayY = -12f;

        /// <summary>
        /// How long a planted charge waits before going off by itself. Long enough that tapping
        /// early is a real choice, short enough that ignoring it never stalls the shot.
        /// </summary>
        const float ChargeFuseSeconds = 1.1f;

        [SerializeField] float lifeSeconds = 1.8f;
        [SerializeField] float outOfPlayX = 5.2f;

        Rigidbody2D _body;
        CircleCollider2D _collider;
        float _spawnTime;
        float _lastImpactTime;
        float _resolveAt;
        float _fuseAt;
        bool _resolved;
        bool _specialSpent;
        System.Action<Projectile> _onResolved;
        System.Action<Projectile> _onSpecial;

        public ProjectileKind Kind { get; private set; }

        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;

        /// <summary>True while a tap on this shot would still do something.</summary>
        public bool AwaitingTrigger => !_resolved && !_specialSpent && HasSpecial(Kind);

        public static bool HasSpecial(ProjectileKind kind)
        {
            return kind == ProjectileKind.Cluster || kind == ProjectileKind.Charge;
        }

        public void Launch(
            ProjectileKind kind,
            Vector2 velocity,
            System.Action<Projectile> onResolved,
            System.Action<Projectile> onSpecial)
        {
            Kind = kind;
            _onResolved = onResolved;
            _onSpecial = onSpecial;
            _resolved = false;
            _specialSpent = false;
            _spawnTime = Time.time;
            _lastImpactTime = float.NegativeInfinity;
            _resolveAt = float.PositiveInfinity;
            _fuseAt = float.PositiveInfinity;
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
            _body.gravityScale = GravityScale;
            _body.linearVelocity = velocity;
        }

        /// <summary>
        /// Spends the shot's special now. Returns false when there is nothing left to spend, so a
        /// tap can fall through to the next live shot instead of being swallowed.
        /// </summary>
        public bool TryTrigger()
        {
            if (!AwaitingTrigger)
            {
                return false;
            }

            _specialSpent = true;
            _onSpecial?.Invoke(this);
            return true;
        }

        void Update()
        {
            if (_resolved)
            {
                return;
            }

            if (Time.time >= _fuseAt)
            {
                TryTrigger();
                return;
            }

            // Leaving the play area always ends a shot, even mid-fuse.
            if (transform.position.y < OutOfPlayY || Mathf.Abs(transform.position.x) > outOfPlayX)
            {
                Resolve();
                return;
            }

            // A planted charge is deliberately sitting still, which is exactly what the idle
            // timeouts below look for. Its fuse owns the lifetime instead.
            if (_fuseAt < float.PositiveInfinity)
            {
                return;
            }

            var age = Time.time - _spawnTime;
            var quietAfterImpact = _lastImpactTime > 0f && Time.time - _lastImpactTime > 0.42f &&
                                   _body != null && _body.linearVelocity.sqrMagnitude < 0.35f;
            if (age >= lifeSeconds || Time.time >= _resolveAt || quietAfterImpact)
            {
                Resolve();
            }
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            _lastImpactTime = Time.time;

            // Untapped specials still pay off on contact: a cluster the player forgot to split
            // scatters where it lands, and a charge plants itself and burns down its fuse.
            if (AwaitingTrigger)
            {
                if (Kind == ProjectileKind.Cluster)
                {
                    TryTrigger();
                    return;
                }

                if (Kind == ProjectileKind.Charge)
                {
                    Plant();
                    return;
                }
            }

            if (collision.relativeVelocity.sqrMagnitude >= 36f)
            {
                _resolveAt = Mathf.Min(_resolveAt, Time.time + 0.55f);
            }
        }

        /// <summary>
        /// Parks a charge on the surface it hit. The collider goes away with it: a live kinematic
        /// body would keep shoving the structure around while the fuse burns, which reads as the
        /// charge pushing the board over rather than blowing it up.
        /// </summary>
        void Plant()
        {
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
                _body.bodyType = RigidbodyType2D.Kinematic;
            }

            if (_collider != null)
            {
                _collider.enabled = false;
            }

            _fuseAt = Time.time + ChargeFuseSeconds;
        }

        public void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _onResolved?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
