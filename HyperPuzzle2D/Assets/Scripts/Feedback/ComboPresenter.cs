using HyperPuzzle2D.Art;
using HyperPuzzle2D.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace HyperPuzzle2D.Feedback
{
    /// <summary>
    /// Combo callout with a scale-pop and fade-out, tinted hotter as the combo climbs.
    /// </summary>
    public sealed class ComboPresenter : MonoBehaviour
    {
        [SerializeField] Text comboText;
        [SerializeField] float showSeconds = 0.9f;

        RectTransform _rect;
        Color _tint;
        float _age;
        bool _playing;

        public void Bind(Text text)
        {
            comboText = text;
            _rect = text != null ? text.rectTransform : null;
            if (comboText != null)
            {
                comboText.gameObject.SetActive(false);
            }
        }

        public void ShowCombo(int combo)
        {
            if (comboText == null || combo <= 0)
            {
                return;
            }

            comboText.text = combo >= 7
                ? Loc.Get("feedback.collapse")
                : combo >= 4
                    ? Loc.Format("feedback.chain", combo)
                    : combo == 1 ? Loc.Get("feedback.smash") : Loc.Format("feedback.smashx", combo);
            _tint = Palette.ComboTint(combo);
            _age = 0f;
            _playing = true;
            comboText.gameObject.SetActive(true);

            if (combo >= 4) Haptics.Heavy();
            else if (combo >= 2) Haptics.Medium();
        }

        void Update()
        {
            if (!_playing)
            {
                return;
            }

            _age += Time.unscaledDeltaTime;
            var t = _age / showSeconds;
            if (t >= 1f)
            {
                _playing = false;
                comboText.gameObject.SetActive(false);
                return;
            }

            // Overshoot early, settle, then drift up while fading.
            var pop = 1f + 0.45f * Mathf.Exp(-9f * t) * Mathf.Cos(18f * t);
            _rect.localScale = Vector3.one * pop;
            _rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 40f, t));

            var alpha = t < 0.6f ? 1f : Mathf.InverseLerp(1f, 0.6f, t);
            comboText.color = new Color(_tint.r, _tint.g, _tint.b, alpha);
        }
    }
}
