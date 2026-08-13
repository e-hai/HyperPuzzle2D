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
            var go = new GameObject("HitBurst");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<HitBurst>().Spawn(color, count, speed);
        }

        void Spawn(Color color, int count, float speed)
        {
            _particles = new Particle[count];
            for (var i = 0; i < count; i++)
            {
                var shard = new GameObject("Shard");
                shard.transform.SetParent(transform, false);

                var renderer = shard.AddComponent<SpriteRenderer>();
                renderer.sprite = Random.value < 0.5f ? Shapes.Circle : Shapes.RoundedRect;
                renderer.color = color;
                renderer.sortingOrder = SortingOrders.Effects;

                var angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
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
}
