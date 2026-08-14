using HyperPuzzle2D.Art;
using UnityEngine;

namespace HyperPuzzle2D.Feedback
{
    /// <summary>
    /// Short-lived sprite burst spawned on impacts and clears. Self-destructs when spent.
    /// </summary>
    public sealed class HitBurst : MonoBehaviour
    {
        struct Particle
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Spin;
            public float Size;
        }

        const float Life = 0.5f;
        const float Gravity = 16f;

        Particle[] _particles;
        float _age;

        public static void Play(Transform parent, Vector3 position, Color color, int count, float speed)
        {
            PlayDirectional(parent, position, color, count, speed, Vector2.zero, 360f);
        }

        public static void PlayDirectional(Transform parent, Vector3 position, Color color, int count, float speed, Vector2 direction, float spreadDegrees)
        {
            var go = new GameObject("HitBurst");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<HitBurst>().Spawn(color, count, speed, direction, spreadDegrees);
        }

        public static void PlayRing(Transform parent, Vector3 position, Color color, float size)
        {
            var ring = new GameObject("ImpactRing");
            ring.transform.SetParent(parent, false);
            ring.transform.position = position;
            var renderer = ring.AddComponent<SpriteRenderer>();
            renderer.sprite = Shapes.Circle;
            renderer.color = new Color(color.r, color.g, color.b, 0.42f);
            renderer.sortingOrder = SortingOrders.Effects;
            ring.transform.localScale = Vector3.one * 0.08f;
            var pulse = ring.AddComponent<ImpactRing>();
            pulse.Configure(size);
        }

        void Spawn(Color color, int count, float speed, Vector2 direction, float spreadDegrees)
        {
            _particles = new Particle[count];
            var directional = direction.sqrMagnitude > 0.01f && spreadDegrees < 359f;
            var baseAngle = directional ? Mathf.Atan2(direction.y, direction.x) : 0f;
            var spread = spreadDegrees * Mathf.Deg2Rad;
            for (var i = 0; i < count; i++)
            {
                var shard = new GameObject("Shard");
                shard.transform.SetParent(transform, false);

                var renderer = shard.AddComponent<SpriteRenderer>();
                renderer.sprite = Random.value < 0.5f ? Shapes.Circle : Shapes.RoundedRect;
                renderer.color = color;
                renderer.sortingOrder = SortingOrders.Effects;

                var angle = directional
                    ? baseAngle + Random.Range(-spread * 0.5f, spread * 0.5f)
                    : (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                var size = Random.Range(0.1f, 0.22f);
                shard.transform.localScale = Vector3.one * size;

                _particles[i] = new Particle
                {
                    Transform = shard.transform,
                    Renderer = renderer,
                    Velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed * Random.Range(0.6f, 1.3f),
                    Spin = Random.Range(-540f, 540f),
                    Size = size,
                };
            }
        }

        void Update()
        {
            _age += Time.deltaTime;
            var t = _age / Life;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            var fade = 1f - t * t;
            for (var i = 0; i < _particles.Length; i++)
            {
                ref var particle = ref _particles[i];
                particle.Velocity.y -= Gravity * Time.deltaTime;
                particle.Transform.position += particle.Velocity * Time.deltaTime;
                particle.Transform.Rotate(0f, 0f, particle.Spin * Time.deltaTime);
                particle.Transform.localScale = Vector3.one * (particle.Size * fade);

                var color = particle.Renderer.color;
                color.a = fade;
                particle.Renderer.color = color;
            }
        }
    }

    /// <summary>Cheap expanding flash used under shards to make a heavy contact read in one frame.</summary>
    public sealed class ImpactRing : MonoBehaviour
    {
        const float Life = 0.22f;
        float _size;
        float _age;
        SpriteRenderer _renderer;

        public void Configure(float size)
        {
            _size = size;
            _renderer = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_age / Life);
            transform.localScale = Vector3.one * Mathf.Lerp(0.08f, _size, 1f - Mathf.Pow(1f - t, 3f));
            if (_renderer != null)
            {
                var color = _renderer.color;
                color.a = (1f - t) * 0.42f;
                _renderer.color = color;
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
