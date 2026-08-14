using HyperPuzzle2D.Art;
using HyperPuzzle2D.Core;
using UnityEngine;

namespace HyperPuzzle2D.Feedback
{
    /// <summary>Short world-space score acknowledgement at the place the destruction happened.</summary>
    public sealed class ScorePopup : MonoBehaviour
    {
        const float Life = 0.75f;
        TextMesh _text;
        Color _color;
        float _age;

        public static void Play(Transform parent, Vector3 position, ScoreAward award)
        {
            var go = new GameObject("ScorePopup");
            go.transform.SetParent(parent, false);
            go.transform.position = position + Vector3.up * 0.25f;
            var popup = go.AddComponent<ScorePopup>();
            popup.Build(award);
        }

        void Build(ScoreAward award)
        {
            _text = gameObject.AddComponent<TextMesh>();
            _text.font = UiFactory.Body;
            _text.fontSize = 64;
            _text.characterSize = 0.055f;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.fontStyle = FontStyle.Bold;
            _text.text = award.Chain > 1 ? $"+{award.Points}  x{award.Chain}" : $"+{award.Points}";
            _color = award.Reason == ScoreReason.Explosion ? Palette.ExplosionCore : Palette.TextPrimary;
            _text.color = _color;

            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = SortingOrders.Effects + 2;
                if (_text.font != null)
                {
                    renderer.sharedMaterial = _text.font.material;
                }
            }
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_age / Life);
            transform.position += Vector3.up * (0.7f * Time.unscaledDeltaTime);
            transform.localScale = Vector3.one * (1f + 0.2f * Mathf.Sin(t * Mathf.PI));
            if (_text != null)
            {
                _text.color = new Color(_color.r, _color.g, _color.b, 1f - t * t);
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
