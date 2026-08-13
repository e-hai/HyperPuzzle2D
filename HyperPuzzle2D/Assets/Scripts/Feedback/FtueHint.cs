using UnityEngine;
using UnityEngine.UI;

namespace HyperPuzzle2D.Feedback
{
    /// <summary>
    /// Pulsing drag-to-aim caption shown until the player fires once. Lives as a sibling of the
    /// HUD so it can be toggled without touching the rest of the UI tree.
    /// </summary>
    public sealed class FtueHint : MonoBehaviour
    {
        Text _label;
        float _born;

        public static FtueHint Create(Transform parent, string copy)
        {
            var go = new GameObject("FtueHint", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.08f, 0.12f);
            rect.anchorMax = new Vector2(0.92f, 0.2f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = go.AddComponent<Text>();
            label.font = Art.UiFactory.Body;
            label.text = copy;
            label.fontSize = 34;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Art.Palette.Accent;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            Art.UiFactory.AddDropShadow(label, 3f);

            var hint = go.AddComponent<FtueHint>();
            hint._label = label;
            hint._born = Time.unscaledTime;
            go.SetActive(false);
            return hint;
        }

        public void SetCopy(string copy)
        {
            if (_label != null)
            {
                _label.text = copy;
                _label.font = Art.UiFactory.Body;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _born = Time.unscaledTime;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (_label == null)
            {
                return;
            }

            var pulse = 0.55f + 0.45f * Mathf.Sin((Time.unscaledTime - _born) * 3.2f);
            var c = _label.color;
            c.a = pulse;
            _label.color = c;
        }
    }
}
