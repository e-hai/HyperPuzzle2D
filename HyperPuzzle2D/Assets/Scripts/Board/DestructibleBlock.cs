using UnityEngine;

namespace HyperPuzzle2D.Board
{
    /// <summary>
    /// One physical target with a material profile, damage state and separate gameplay/visual
    /// lifetimes. A piece counts as knocked off as soon as it leaves the shelf, but remains visible
    /// until it falls below the camera; this keeps reward timing immediate without visible pops.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DestructibleBlock : MonoBehaviour
    {
        const float SettledSpeedSqr = 0.4f;

        [SerializeField] float clearY = -8f;
        [SerializeField] float knockOffY = -1f;
        [SerializeField] float playLeft = -1.2f;
        [SerializeField] float playRight = 3.2f;
        [SerializeField] int hitScore = 5;
        [SerializeField] int breakScore = 20;
        [SerializeField] int knockOffScore = 15;
        [SerializeField] float resistance = 60f;
        [SerializeField] DestructionMaterial material = DestructionMaterial.Normal;

        public int HitScore => hitScore;
        public int BreakScore => breakScore;
        public int KnockOffScore => knockOffScore;
        public DestructionMaterial Material => material;
        public bool IsCleared { get; private set; }
        public bool IsKnockedOff { get; private set; }
        public bool IsBroken { get; private set; }
        public float Health01 { get; private set; } = 1f;

        public event System.Action<DestructibleBlock, ImpactEvent> Damaged;
        public event System.Action<DestructibleBlock, ImpactEvent> Broken;
        public event System.Action<DestructibleBlock> KnockedOff;

        /// <summary>Configures both reward timing and the later visual cleanup boundary.</summary>
        public void Configure(DestructionMaterial destructionMaterial, float shelfY, float left, float right, float despawnY)
        {
            material = destructionMaterial;
            knockOffY = shelfY - 0.9f;
            playLeft = left - 0.5f;
            playRight = right + 0.5f;
            clearY = despawnY;

            switch (material)
            {
                case DestructionMaterial.Brittle:
                    // Glass, not masonry. A lobbed shot arrives near the top of its arc with
                    // little energy left, and a brittle block that survives that reads as a bug.
                    resistance = 9f;
                    hitScore = 5;
                    breakScore = 25;
                    knockOffScore = 10;
                    break;
                case DestructionMaterial.Explosive:
                    // Deliberately fragile: the core is the payoff, so reaching it should be the
                    // hard part, never surviving the hit that lands on it.
                    resistance = 14f;
                    hitScore = 5;
                    breakScore = 35;
                    knockOffScore = 10;
                    break;
                case DestructionMaterial.Support:
                    resistance = 24f;
                    hitScore = 5;
                    breakScore = 25;
                    knockOffScore = 15;
                    break;
                case DestructionMaterial.Heavy:
                    resistance = 170f;
                    hitScore = 10;
                    breakScore = 40;
                    knockOffScore = 25;
                    break;
                case DestructionMaterial.Beam:
                    // A beam is the span holding a deck up, so snapping one is the payoff shot.
                    // Anything tougher just eats a good hit and drops nothing.
                    resistance = 28f;
                    hitScore = 8;
                    breakScore = 30;
                    knockOffScore = 20;
                    break;
                case DestructionMaterial.Ball:
                    // Balls ride on top, so they are what a lobbed shot meets first. At the old
                    // value that opening hit scored a few points and moved nothing.
                    resistance = 45f;
                    hitScore = 8;
                    breakScore = 25;
                    knockOffScore = 20;
                    break;
                default:
                    resistance = 40f;
                    hitScore = 6;
                    breakScore = 20;
                    knockOffScore = 15;
                    break;
            }
        }

        /// <summary>True while the target is still moving, so a run is not judged mid-cascade.</summary>
        public bool IsSettling => !IsCleared && _body != null && _body.linearVelocity.sqrMagnitude > SettledSpeedSqr;

        Rigidbody2D _body;

        bool _parked;

        /// <summary>
        /// Holds the authored structure exactly as designed until the player fires. Kinematic
        /// bodies ignore gravity and every impulse, so this cannot drift no matter what the
        /// solver would otherwise do at spawn.
        /// </summary>
        public void Park()
        {
            if (_body == null)
            {
                return;
            }

            _parked = true;
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }

        /// <summary>
        /// Hands the piece back to the simulation, but asleep: it stays put until something
        /// actually touches it. Waking the whole board instead would let untouched pieces
        /// settle and tilt, which is what rolls perched balls off their supports.
        /// Ignored once released, so later shots never re-freeze pieces that are mid-flight.
        /// </summary>
        public void Release()
        {
            if (_body == null || !_parked)
            {
                return;
            }

            _parked = false;
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.Sleep();
        }

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        void Update()
        {
            if (IsCleared)
            {
                return;
            }

            if (!IsKnockedOff && !_parked &&
                (transform.position.y < knockOffY || transform.position.x < playLeft || transform.position.x > playRight))
            {
                IsKnockedOff = true;
                KnockedOff?.Invoke(this);
            }

            if (transform.position.y < clearY)
            {
                Despawn();
            }
        }

        /// <summary>Applies measured projectile/explosion energy and returns true when it breaks.</summary>
        public bool ApplyImpact(ImpactEvent impact)
        {
            if (IsCleared || IsBroken || IsKnockedOff)
            {
                return false;
            }

            var damage = impact.Energy / Mathf.Max(1f, resistance);
            Health01 = Mathf.Clamp01(Health01 - damage);
            Damaged?.Invoke(this, impact);
            if (Health01 > 0f)
            {
                return false;
            }

            IsBroken = true;
            IsKnockedOff = true;
            Broken?.Invoke(this, impact);
            Despawn();
            return true;
        }

        void Despawn()
        {
            if (IsCleared)
            {
                return;
            }

            IsCleared = true;
            gameObject.SetActive(false);
        }
    }
}
