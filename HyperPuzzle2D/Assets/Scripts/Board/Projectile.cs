using UnityEngine;

namespace HyperPuzzle2D.Board
{
    /// <summary>
    /// Simple projectile used by the cannon.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class Projectile : MonoBehaviour
    {
        /// <summary>Shared with the cannon's trajectory preview so the dots match the real arc.</summary>
        public const float GravityScale = 1.2f;

        /// <summary>Below the bottom of the tallest phone viewport, so shots leave view before despawning.</summary>
        const float OutOfPlayY = -12f;

        [SerializeField] float lifeSeconds = 1.8f;
        [SerializeField] float outOfPlayX = 5.2f;

        Rigidbody2D _body;
        float _spawnTime;
        float _lastImpactTime;
        float _resolveAt;
        bool _resolved;
        System.Action<Projectile> _onResolved;

        public void Launch(Vector2 velocity, System.Action<Projectile> onResolved)
        {
            _onResolved = onResolved;
            _resolved = false;
            _spawnTime = Time.time;
            _lastImpactTime = float.NegativeInfinity;
            _resolveAt = float.PositiveInfinity;
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = GravityScale;
            _body.linearVelocity = velocity;
        }

        void Update()
        {
            if (_resolved)
            {
                return;
            }

            var age = Time.time - _spawnTime;
            var quietAfterImpact = _lastImpactTime > 0f && Time.time - _lastImpactTime > 0.42f &&
                                   _body != null && _body.linearVelocity.sqrMagnitude < 0.35f;
            if (age >= lifeSeconds || Time.time >= _resolveAt || transform.position.y < OutOfPlayY ||
                Mathf.Abs(transform.position.x) > outOfPlayX || quietAfterImpact)
            {
                Resolve();
            }
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            _lastImpactTime = Time.time;
            if (collision.relativeVelocity.sqrMagnitude >= 36f)
            {
                _resolveAt = Mathf.Min(_resolveAt, Time.time + 0.55f);
            }
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
