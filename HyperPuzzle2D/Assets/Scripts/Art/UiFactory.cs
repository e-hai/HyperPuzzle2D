using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// Builds the runtime uGUI hierarchy. Layout is expressed in normalised anchors so it
    /// survives any aspect ratio the CanvasScaler throws at it.
    /// </summary>
    public static class UiFactory
    {
        /// <summary>
        /// Built-in font asset names have changed across Unity versions, and a Text with a null
        /// font renders nothing without logging anything. Try each known name, then fall back to
        /// an OS font, and only give up loudly.
        /// </summary>
        static readonly string[] BuiltinFontNames = { "LegacyRuntime.ttf", "Arial.ttf" };

        /// <summary>Latin-capable OS fonts. Used when the active language does not need CJK glyphs.</summary>
        static readonly string[] LatinOsFonts =
        {
            "Helvetica Neue", "Helvetica", "Arial", "Segoe UI", "Roboto", "DejaVu Sans",
        };

        /// <summary>
        /// CJK-capable OS fonts. Builtin Arial / LegacyRuntime have no Chinese glyphs, so Chinese
        /// UI must come from the OS. Order prefers the fonts shipping with current macOS / iOS /
        /// Windows / Android builds.
        /// </summary>
        static readonly string[] CjkOsFonts =
        {
            "PingFang SC", "Heiti SC", "STHeiti", "Hiragino Sans GB",
            "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC",
            "Microsoft YaHei", "微软雅黑", "SimHei", "黑体",
            "Droid Sans Fallback", "Noto Sans CJK",
        };

        static Font _latinFont;
        static Font _cjkFont;

        public static Font Body => Localization.Loc.NeedsCjk ? CjkBody : LatinBody;

        static Font LatinBody
        {
            get
            {
                if (_latinFont != null)
                {
                    return _latinFont;
                }

                foreach (var name in BuiltinFontNames)
                {
                    _latinFont = Resources.GetBuiltinResource<Font>(name);
                    if (_latinFont != null)
                    {
                        return _latinFont;
                    }
                }

                _latinFont = Font.CreateDynamicFontFromOSFont(LatinOsFonts, 48);
                if (_latinFont == null)
                {
                    Debug.LogError("UiFactory: no usable Latin font resolved; UI text may be invisible.");
                }

                return _latinFont;
            }
        }

        static Font CjkBody
        {
            get
            {
                if (_cjkFont != null)
                {
                    return _cjkFont;
                }

                _cjkFont = Font.CreateDynamicFontFromOSFont(CjkOsFonts, 48);
                if (_cjkFont == null)
                {
                    // Last resort: a Latin font will at least show something (boxes for CJK).
                    Debug.LogWarning("UiFactory: no CJK OS font found; falling back to Latin.");
                    _cjkFont = LatinBody;
                }

                return _cjkFont;
            }
        }

        /// <summary>Rebinds every Text under <paramref name="root"/> to the font matching the active language.</summary>
        public static void RebindFonts(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var font = Body;
            var texts = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
            }
        }

        public static Text Label(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            int fontSize,
            TextAnchor alignment,
            Color color,
            FontStyle style = FontStyle.Normal,
            string content = null)
        {
            var go = NewUiObject(name, parent);

            var text = go.AddComponent<Text>();
            text.font = Body;
            text.text = content ?? string.Empty;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            Anchor(text.rectTransform, anchorMin, anchorMax);
            return text;
        }

        public static Image Panel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            float cornerScale = 0.55f)
        {
            var go = NewUiObject(name, parent);

            var image = go.AddComponent<Image>();
            image.sprite = Shapes.RoundedRect;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = cornerScale;
            image.color = color;
            Anchor(image.rectTransform, anchorMin, anchorMax);
            return image;
        }

        /// <summary>Full-bleed flat colour, used for scrims.</summary>
        public static Image Scrim(Transform parent, string name, Color color)
        {
            var go = NewUiObject(name, parent);

            var image = go.AddComponent<Image>();
            image.sprite = Shapes.Solid;
            image.color = color;
            Anchor(image.rectTransform, Vector2.zero, Vector2.one);
            return image;
        }

        public static Button Pill(
            Transform parent,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color fill,
            Color textColor,
            int fontSize,
            UnityAction onClick)
        {
            var image = Panel(parent, "Button_" + label, anchorMin, anchorMax, fill, 0.42f);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            Label(image.transform, "Label", Vector2.zero, Vector2.one, fontSize, TextAnchor.MiddleCenter, textColor, FontStyle.Bold, label);
            return button;
        }

        /// <summary>
        /// Creates the RectTransform up front. Letting AddComponent convert a plain Transform
        /// works, but the converted rect starts with defaults that are easy to miss.
        /// </summary>
        public static GameObject NewUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void AddDropShadow(Graphic graphic, float distance = 3f)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(distance, -distance);
        }
    }
}
