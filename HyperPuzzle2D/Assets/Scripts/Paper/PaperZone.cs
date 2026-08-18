using HyperPuzzle2D.Art;
using UnityEngine;

namespace HyperPuzzle2D.Paper
{
    /// <summary>
    /// One scored puzzle piece of the paper target. Holds its score/tier, highlights when the
    /// reticle is over it, and peels off (fly out + spin + fade) when hit before disabling itself.
    /// </summary>
    public sealed class PaperZone : MonoBehaviour
    {
        const float PeelLife = 0.45f;

        /// <summary>Region-map id this fragment owns; the field looks fragments up by it.</summary>
        public int Id { get; private set; }

        public int Score { get; private set; }
        public string LabelCn { get; private set; }
        public string LabelEn { get; private set; }
        public Color Tier { get; private set; }
        public bool IsBroken { get; private set; }

        SpriteRenderer _renderer;
        SpriteRenderer _glow;
        Color _baseColor;
        bool _highlighted;

        bool _peeling;
        float _peelAge;
        Vector3 _peelVelocity;
        float _peelSpin;

        public void Configure(int id, int score, string labelCn, string labelEn, Color tier, SpriteRenderer renderer)
        {
            Id = id;
            Score = score;
            LabelCn = labelCn;
            LabelEn = labelEn;
            Tier = tier;
            _renderer = renderer;
            _baseColor = renderer.color;

            // A copy of the piece sitting just behind it: scaled up while highlighted it draws a
            // tier-coloured rim in the exact shape of the piece, which no flat tint can do.
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(renderer.transform, false);
            glowGo.transform.localScale = Vector3.one * 1.14f;
            _glow = glowGo.AddComponent<SpriteRenderer>();
            _glow.sprite = renderer.sprite;
            _glow.sortingOrder = renderer.sortingOrder - 1;
            _glow.color = new Color(Palette.StarGold.r, Palette.StarGold.g, Palette.StarGold.b, 0f);
        }

        public void SetHighlighted(bool on)
        {
            if (_peeling || IsBroken || _highlighted == on)
            {
                return;
            }

            _highlighted = on;
            // Lift the piece toward white rather than tinting it: a tier tint turns muddy over
            // strongly coloured artwork. The colour cue lives in the rim behind it instead.
            _renderer.color = on
                ? Color.Lerp(_baseColor, Color.white, 0.22f) * 1.06f
                : _baseColor;
            transform.localScale = Vector3.one * (on ? 1.06f : 1f);

            // Lift the pair above the neighbouring pieces while highlighted, otherwise they cover
            // the rim, which only ever grows into its neighbours' space.
            _renderer.sortingOrder = on ? SortingOrders.Effects - 2 : SortingOrders.Block + 1;
            if (_glow != null)
            {
                _glow.sortingOrder = _renderer.sortingOrder - 1;
                _glow.color = new Color(Palette.StarGold.r, Palette.StarGold.g, Palette.StarGold.b, on ? 0.95f : 0f);
            }
        }

        public void Break(Vector3 direction)
        {
            if (IsBroken)
            {
                return;
            }

            IsBroken = true;
            _highlighted = false;
            if (_glow != null)
            {
                _glow.color = new Color(Palette.StarGold.r, Palette.StarGold.g, Palette.StarGold.b, 0f);
            }

            _peeling = true;
            _peelAge = 0f;
            _renderer.sortingOrder = SortingOrders.Effects - 1;

            var dir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.up;
            _peelVelocity = dir * Random.Range(3.5f, 5.5f) + Vector3.up * 1.5f;
            _peelSpin = Random.Range(-320f, 320f);

            // A quick white flash sells the impact before the piece peels, and a scatter of torn
            // paper bits, tinted with the part's tier, reads as the sheet tearing rather than a
            // sprite politely sliding off. Higher-value parts throw more confetti.
            _renderer.color = Color.Lerp(_baseColor, Color.white, 0.6f);
            var count = 6 + Mathf.RoundToInt(Mathf.InverseLerp(20f, 500f, Score) * 10f);
            PaperConfetti.Burst(transform.parent, transform.position, dir, Tier, count);
        }

        void Update()
        {
            if (!_peeling)
            {
                return;
            }

            _peelAge += Time.deltaTime;
            var t = Mathf.Clamp01(_peelAge / PeelLife);

            _peelVelocity.y -= 9f * Time.deltaTime;
            transform.position += _peelVelocity * Time.deltaTime;
            transform.Rotate(0f, 0f, _peelSpin * Time.deltaTime);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, t);

            var color = _renderer.color;
            color.a = 1f - t * t;
            _renderer.color = color;

            if (t >= 1f)
            {
                _peeling = false;
                _renderer.enabled = false;
                gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// A short-lived torn-paper shard flung from a broken zone. Self-animates (fly out, tumble,
    /// fall, fade) and destroys itself, so callers just fire and forget.
    /// </summary>
    public sealed class PaperConfetti : MonoBehaviour
    {
        const float Life = 0.5f;

        SpriteRenderer _renderer;
        Vector3 _velocity;
        float _spin;
        float _age;
        Color _color;

        public static void Burst(Transform parent, Vector3 origin, Vector3 direction, Color tint, int count)
        {
            var dir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.up;
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("PaperBit");
                go.transform.SetParent(parent, false);
                go.transform.position = origin + (Vector3)(Random.insideUnitCircle * 0.25f);
                go.transform.localScale = Vector3.one * Random.Range(0.08f, 0.18f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Shapes.Solid;
                sr.sortingOrder = SortingOrders.Effects + 1;

                var spread = Quaternion.Euler(0f, 0f, Random.Range(-55f, 55f));
                var bit = go.AddComponent<PaperConfetti>();
                bit._renderer = sr;
                bit._color = Color.Lerp(tint, Color.white, Random.Range(0f, 0.4f));
                bit._velocity = spread * dir * Random.Range(4f, 8f) + Vector3.up * Random.Range(0.5f, 2.5f);
                bit._spin = Random.Range(-540f, 540f);
                sr.color = bit._color;
            }
        }

        void Update()
        {
            _age += Time.deltaTime;
            var t = Mathf.Clamp01(_age / Life);

            _velocity.y -= 16f * Time.deltaTime;
            transform.position += _velocity * Time.deltaTime;
            transform.Rotate(0f, 0f, _spin * Time.deltaTime);

            _color.a = 1f - t;
            _renderer.color = _color;

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
