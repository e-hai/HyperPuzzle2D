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

        [SerializeField] float lifeSeconds = 4f;

        Rigidbody2D _body;
        float _spawnTime;
        bool _resolved;
        System.Action<Projectile> _onResolved;

        public void Launch(Vector2 velocity, System.Action<Projectile> onResolved)
        {
            _onResolved = onResolved;
            _resolved = false;
            _spawnTime = Time.time;
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

            if (Time.time - _spawnTime >= lifeSeconds || transform.position.y < -8f)
            {
                Resolve();
            }
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            // Keep flying after first bounce; resolution is time / fall based.
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
