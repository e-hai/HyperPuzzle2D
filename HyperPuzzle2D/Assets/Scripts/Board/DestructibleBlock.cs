using UnityEngine;

namespace HyperPuzzle2D.Board
{
    /// <summary>
    /// Physics target that counts as cleared when it leaves the play field.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DestructibleBlock : MonoBehaviour
    {
        const float SettledSpeedSqr = 0.4f;

        [SerializeField] float clearY = -8f;
        [SerializeField] int scoreValue = 10;

        public int ScoreValue => scoreValue;
        public bool IsCleared { get; private set; }

        /// <summary>True while the target is still moving, so a run is not judged mid-cascade.</summary>
        public bool IsSettling => !IsCleared && _body != null && _body.linearVelocity.sqrMagnitude > SettledSpeedSqr;

        public event System.Action<DestructibleBlock> Cleared;

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

            if (transform.position.y < clearY)
            {
                MarkCleared();
            }
        }

        public void MarkCleared()
        {
            if (IsCleared)
            {
                return;
            }

            IsCleared = true;
            Cleared?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
