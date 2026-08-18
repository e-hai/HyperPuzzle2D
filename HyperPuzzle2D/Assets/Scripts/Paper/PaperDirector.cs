using System.Collections;
using System.Collections.Generic;
using HyperPuzzle2D.Art;
using HyperPuzzle2D.Core;
using HyperPuzzle2D.Feedback;
using HyperPuzzle2D.Localization;
using HyperPuzzle2D.Meta;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HyperPuzzle2D.Paper
{
    /// <summary>
    /// Shell around the paper-target gameplay: splash, home, stage select, the in-run HUD and the
    /// result card. It owns the run state and progression; <see cref="PaperTargetField"/> owns the
    /// world and reports each resolved shot back here.
    /// </summary>
    public sealed class PaperDirector : MonoBehaviour
    {
        enum Screen
        {
            Splash,
            Home,
            Stages,
            Play,
        }

        /// <summary>Hold after the intro animation, before the fade into home.</summary>
        const float SplashSeconds = 0.7f;

        readonly GameLoop _loop = new GameLoop();
        PaperTargetField _field;
        Canvas _canvas;

        GameObject _splashRoot;
        GameObject _homeRoot;
        GameObject _stagesRoot;
        GameObject _playRoot;
        GameObject _settingsRoot;
        GameObject _resultRoot;

        CanvasGroup _splashGroup;
        CanvasGroup _splashWordmark;
        RectTransform _splashMark;

        Text _homeStarsText;
        Text _continueLabel;
        readonly List<StageCard> _cards = new List<StageCard>();

        Text _scoreText;
        Text _stageNameText;
        Text _hintText;
        CanvasGroup _hintGroup;
        Image _goalFill;
        Image _goalTick;
        Image _twoStarTick;
        Text _ammoCaption;
        Image _flash;
        RectTransform _ammoRow;
        readonly List<Image> _ammoPips = new List<Image>();
        int _shotsFired;

        Text _resultTitle;
        Text _resultScore;
        Text _replayLabel;
        Button _nextButton;
        readonly List<Image> _resultStars = new List<Image>();

        Text _sfxLabel;
        Text _hapticsLabel;
        Text _languageLabel;

        int _stageIndex;
        PaperLevel _level;

        /// <summary>UI handles for one stage entry, kept so locks and stars can refresh in place.</summary>
        sealed class StageCard
        {
            public int Index;
            public Button Button;
            public Image Portrait;
            public Image Lock;
            public Text Name;
            public readonly List<Image> Stars = new List<Image>();
        }

        void Awake()
        {
            // Stage numbers here refer to characters, not the demolition levels that share the
            // same save keys, so this mode keeps its own unlocks and stars.
            Progress.Profile = "paper_";
            _field = gameObject.AddComponent<PaperTargetField>();
            SetupCanvas();
        }

        void Start()
        {
            _field.ShotResolved += OnShotResolved;
            _field.AimKindChanged += OnAimKindChanged;
            _loop.StateChanged += OnStateChanged;

            BuildSplash();
            BuildHome();
            BuildStages();
            BuildPlay();
            BuildSettings();

            Show(Screen.Splash);
            StartCoroutine(SplashThenHome());
        }

        IEnumerator SplashThenHome()
        {
            yield return StartCoroutine(AnimateSplash());
            yield return new WaitForSeconds(SplashSeconds);

            const float fadeOut = 0.25f;
            for (var t = 0f; t < fadeOut && _splashRoot.activeSelf; t += Time.unscaledDeltaTime)
            {
                _splashGroup.alpha = 1f - Mathf.Clamp01(t / fadeOut);
                yield return null;
            }

            if (_splashRoot != null && _splashRoot.activeSelf)
            {
                Show(Screen.Home);
            }

            _splashGroup.alpha = 1f;
        }

        void SetupCanvas()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var canvasGo = new GameObject("PaperCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
        }

        void Show(Screen screen)
        {
            _splashRoot.SetActive(screen == Screen.Splash);
            _homeRoot.SetActive(screen == Screen.Home);
            _stagesRoot.SetActive(screen == Screen.Stages);
            _playRoot.SetActive(screen == Screen.Play);

            _field.SetWorldVisible(screen == Screen.Play);
            _field.InputEnabled = screen == Screen.Play && _loop.State == GameState.Playing;

            if (screen == Screen.Home)
            {
                RefreshHome();
            }
            else if (screen == Screen.Stages)
            {
                RefreshStages();
            }
        }

        // ---------------------------------------------------------------- splash

        void BuildSplash()
        {
            _splashRoot = FullScreenRoot("Splash");
            PaperBackdrop(_splashRoot.transform);

            // The launcher icon artwork itself, so the boot screen and the installed icon are the
            // same drawing instead of two similar-looking placeholders.
            var markGo = UiFactory.NewUiObject("Mark", _splashRoot.transform);
            var mark = markGo.AddComponent<Image>();
            mark.sprite = BrandMark.MarkSprite;
            mark.preserveAspect = true;
            mark.raycastTarget = false;
            UiFactory.Anchor(mark.rectTransform, new Vector2(0.28f, 0.50f), new Vector2(0.72f, 0.72f));
            _splashMark = mark.rectTransform;

            var title = UiFactory.Label(_splashRoot.transform, "Title", new Vector2(0.1f, 0.41f), new Vector2(0.9f, 0.49f), 58, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold,
                Loc.NeedsCjk ? "纸片射击场" : "PAPER RANGE");

            // Flat ink dash rather than a rounded panel, which turns into a lozenge at this height.
            var ruleGo = UiFactory.NewUiObject("Rule", _splashRoot.transform);
            var rule = ruleGo.AddComponent<Image>();
            rule.sprite = Shapes.Solid;
            rule.color = new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, 0.85f);
            rule.raycastTarget = false;
            UiFactory.Anchor(rule.rectTransform, new Vector2(0.42f, 0.3955f), new Vector2(0.58f, 0.3985f));

            var sub = UiFactory.Label(_splashRoot.transform, "Sub", new Vector2(0.1f, 0.33f), new Vector2(0.9f, 0.385f), 26, TextAnchor.MiddleCenter, Palette.TextMuted, FontStyle.Normal,
                Loc.NeedsCjk ? "一发一片，撕出高分" : "ONE SHOT, ONE PIECE");

            _splashWordmark = UiFactory.NewUiObject("Wordmark", _splashRoot.transform).AddComponent<CanvasGroup>();
            UiFactory.Anchor(_splashWordmark.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            title.transform.SetParent(_splashWordmark.transform, true);
            rule.transform.SetParent(_splashWordmark.transform, true);
            sub.transform.SetParent(_splashWordmark.transform, true);

            _splashGroup = _splashRoot.AddComponent<CanvasGroup>();
        }

        /// <summary>Gradient plus paper fibre, matching the play field so screens share one surface.</summary>
        static void PaperBackdrop(Transform parent)
        {
            var bgGo = UiFactory.NewUiObject("Bg", parent);
            var bg = bgGo.AddComponent<Image>();
            bg.sprite = Shapes.VerticalGradient(Palette.BackdropBottom, Palette.BackdropTop);
            bg.raycastTarget = false;
            UiFactory.Anchor(bg.rectTransform, Vector2.zero, Vector2.one);

            var grainGo = UiFactory.NewUiObject("Grain", parent);
            var grain = grainGo.AddComponent<Image>();
            grain.sprite = Shapes.PaperFiber;
            grain.type = Image.Type.Tiled;
            grain.color = new Color(Palette.Shadow.r, Palette.Shadow.g, Palette.Shadow.b, 0.12f);
            grain.raycastTarget = false;
            UiFactory.Anchor(grain.rectTransform, Vector2.zero, Vector2.one);
        }

        /// <summary>
        /// Mark settles in like a reticle locking on, wordmark follows, then the whole card fades
        /// into the home screen. Unscaled time so it is unaffected by any pause.
        /// </summary>
        IEnumerator AnimateSplash()
        {
            _splashGroup.alpha = 0f;
            _splashWordmark.alpha = 0f;

            const float markIn = 0.45f;
            for (var t = 0f; t < markIn; t += Time.unscaledDeltaTime)
            {
                var k = Mathf.Clamp01(t / markIn);
                var ease = 1f - Mathf.Pow(1f - k, 3f);
                _splashGroup.alpha = ease;
                _splashMark.localScale = Vector3.one * Mathf.Lerp(1.18f, 1f, ease);
                yield return null;
            }

            _splashGroup.alpha = 1f;
            _splashMark.localScale = Vector3.one;

            const float wordIn = 0.3f;
            for (var t = 0f; t < wordIn; t += Time.unscaledDeltaTime)
            {
                _splashWordmark.alpha = Mathf.Clamp01(t / wordIn);
                yield return null;
            }

            _splashWordmark.alpha = 1f;
        }

        // ---------------------------------------------------------------- home

        void BuildHome()
        {
            _homeRoot = FullScreenRoot("Home");
            PaperBackdrop(_homeRoot.transform);

            var banner = UiFactory.Panel(_homeRoot.transform, "Banner", new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.88f), Palette.CardFill, 0.45f);
            UiFactory.AddDropShadow(banner, 5f);
            UiFactory.Label(banner.transform, "Title", new Vector2(0.06f, 0.5f), new Vector2(0.94f, 0.88f), 62, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold,
                Loc.NeedsCjk ? "纸片射击场" : "PAPER RANGE");
            UiFactory.Label(banner.transform, "Sub", new Vector2(0.06f, 0.28f), new Vector2(0.94f, 0.5f), 26, TextAnchor.MiddleCenter, Palette.TextMuted, FontStyle.Normal,
                Loc.NeedsCjk ? "撕下衣片，拼出高分" : "TEAR THE CLOTHES, STACK THE SCORE");
            _homeStarsText = UiFactory.Label(banner.transform, "Stars", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.28f), 34, TextAnchor.MiddleCenter, Palette.StarGold, FontStyle.Bold);

            _continueLabel = UiFactory.Pill(_homeRoot.transform, "PLAY", new Vector2(0.14f, 0.48f), new Vector2(0.86f, 0.60f), Palette.Accent, Palette.TextOnAccent, 42, ContinueRun)
                .GetComponentInChildren<Text>();

            UiFactory.Pill(_homeRoot.transform, Loc.NeedsCjk ? "选择角色" : "CHARACTERS", new Vector2(0.14f, 0.34f), new Vector2(0.86f, 0.44f), Palette.CardFill, Palette.TextPrimary, 34,
                () => Show(Screen.Stages));

            UiFactory.Pill(_homeRoot.transform, Loc.NeedsCjk ? "设置" : "SETTINGS", new Vector2(0.34f, 0.22f), new Vector2(0.66f, 0.30f), Palette.HudPanel, Palette.TextMuted, 28,
                () => SetSettingsVisible(true));
        }

        void RefreshHome()
        {
            var total = Progress.TotalStars(PaperLevelLibrary.Count);
            var max = PaperLevelLibrary.Count * 3;
            _homeStarsText.text = $"★ {total} / {max}";

            var resume = Mathf.Clamp(Progress.LastPlayedStage, 0, PaperLevelLibrary.Count - 1);
            var fresh = Progress.TotalStars(PaperLevelLibrary.Count) == 0 && resume == 0;
            _continueLabel.text = fresh
                ? (Loc.NeedsCjk ? "开始游戏" : "PLAY")
                : (Loc.NeedsCjk ? "继续 · 第 " + (resume + 1) + " 关" : "CONTINUE · " + (resume + 1));
        }

        void ContinueRun()
        {
            var resume = Mathf.Clamp(Progress.LastPlayedStage, 0, PaperLevelLibrary.Count - 1);
            if (!Progress.IsStageUnlocked(resume))
            {
                resume = Mathf.Max(0, Progress.StageUnlocked - 1);
            }

            StartStage(resume);
        }

        // ---------------------------------------------------------------- stage select

        void BuildStages()
        {
            _stagesRoot = FullScreenRoot("Stages");
            PaperBackdrop(_stagesRoot.transform);

            UiFactory.Label(_stagesRoot.transform, "Title", new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.95f), 46, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold,
                Loc.NeedsCjk ? "选择角色" : "CHARACTERS");

            UiFactory.Pill(_stagesRoot.transform, Loc.NeedsCjk ? "返回" : "BACK", new Vector2(0.34f, 0.06f), new Vector2(0.66f, 0.14f), Palette.CardFill, Palette.TextPrimary, 30,
                () => Show(Screen.Home));

            // Two columns; the portrait is the thumbnail, so the player picks a character rather
            // than an abstract level number.
            const int columns = 2;
            const float left = 0.07f;
            const float right = 0.93f;
            const float top = 0.85f;
            const float bottom = 0.18f;
            const float gap = 0.03f;

            var rows = Mathf.CeilToInt(PaperLevelLibrary.Count / (float)columns);
            var cellW = (right - left - gap * (columns - 1)) / columns;
            var cellH = (top - bottom - gap * (rows - 1)) / rows;

            for (var i = 0; i < PaperLevelLibrary.Count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var x0 = left + col * (cellW + gap);
                var y1 = top - row * (cellH + gap);
                BuildStageCard(i, new Vector2(x0, y1 - cellH), new Vector2(x0 + cellW, y1));
            }
        }

        void BuildStageCard(int index, Vector2 min, Vector2 max)
        {
            var level = PaperLevelLibrary.Get(index);
            var card = UiFactory.Panel(_stagesRoot.transform, "Stage" + index, min, max, Palette.CardFill, 0.45f);
            UiFactory.AddDropShadow(card, 4f);

            var entry = new StageCard { Index = index };

            var portraitGo = UiFactory.NewUiObject("Portrait", card.transform);
            entry.Portrait = portraitGo.AddComponent<Image>();
            entry.Portrait.preserveAspect = true;
            entry.Portrait.raycastTarget = false;
            entry.Portrait.sprite = LoadPortrait(level);
            UiFactory.Anchor(entry.Portrait.rectTransform, new Vector2(0.1f, 0.30f), new Vector2(0.9f, 0.95f));

            entry.Name = UiFactory.Label(card.transform, "Name", new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.30f), 26, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold,
                Loc.NeedsCjk ? level.NameCn : level.NameEn);

            for (var s = 0; s < 3; s++)
            {
                var starGo = UiFactory.NewUiObject("Star" + s, card.transform);
                var star = starGo.AddComponent<Image>();
                star.sprite = Shapes.Star;
                star.raycastTarget = false;
                var x = 0.30f + s * 0.14f;
                UiFactory.Anchor(star.rectTransform, new Vector2(x, 0.05f), new Vector2(x + 0.12f, 0.17f));
                entry.Stars.Add(star);
            }

            var lockGo = UiFactory.NewUiObject("Lock", card.transform);
            entry.Lock = lockGo.AddComponent<Image>();
            entry.Lock.sprite = Shapes.RoundedRect;
            entry.Lock.type = Image.Type.Sliced;
            entry.Lock.pixelsPerUnitMultiplier = 0.45f;
            entry.Lock.color = new Color(Palette.Scrim.r, Palette.Scrim.g, Palette.Scrim.b, 0.62f);
            entry.Lock.raycastTarget = false;
            UiFactory.Anchor(entry.Lock.rectTransform, Vector2.zero, Vector2.one);
            UiFactory.Label(lockGo.transform, "Glyph", Vector2.zero, Vector2.one, 28, TextAnchor.MiddleCenter, Palette.TextOnAccent, FontStyle.Bold,
                Loc.NeedsCjk ? "未解锁" : "LOCKED");

            entry.Button = card.gameObject.AddComponent<Button>();
            entry.Button.targetGraphic = card;
            var captured = index;
            entry.Button.onClick.AddListener(() => StartStage(captured));

            _cards.Add(entry);
        }

        static Sprite LoadPortrait(PaperLevel level)
        {
            var tex = Resources.Load<Texture2D>(level.PortraitPath)
                      ?? Resources.Load<Texture2D>(level.BasePath)
                      ?? Resources.Load<Texture2D>("Art/Paper" + level.Character);
            if (tex == null)
            {
                return Shapes.RoundedRect;
            }

            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        void RefreshStages()
        {
            for (var i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                var unlocked = Progress.IsStageUnlocked(card.Index);
                var stars = Progress.StageStars(card.Index);

                card.Lock.gameObject.SetActive(!unlocked);
                card.Button.interactable = unlocked;
                card.Portrait.color = unlocked ? Color.white : new Color(0.7f, 0.66f, 0.6f, 1f);

                for (var s = 0; s < card.Stars.Count; s++)
                {
                    card.Stars[s].color = s < stars ? Palette.StarGold : new Color(0.55f, 0.48f, 0.38f, 0.45f);
                }
            }
        }

        // ---------------------------------------------------------------- play

        void BuildPlay()
        {
            _playRoot = FullScreenRoot("Play");

            // Everything the run needs to report lives in one bar, so the eye has a single place
            // to check: who you are shooting, what you have scored, how far the stars are, what is left.
            // Sits clear of the notch / dynamic island, which overlaps roughly the top 5%.
            var bar = UiFactory.Panel(_playRoot.transform, "TopBar", new Vector2(0.035f, 0.838f), new Vector2(0.965f, 0.936f), Palette.HudPanel);
            UiFactory.AddDropShadow(bar, 4f);

            var homeButton = UiFactory.Pill(bar.transform, "‹", new Vector2(0.02f, 0.18f), new Vector2(0.115f, 0.82f), Palette.HudFill, Palette.TextPrimary, 38,
                () => Show(Screen.Home));
            homeButton.transform.SetAsFirstSibling();

            _stageNameText = UiFactory.Label(bar.transform, "StageName", new Vector2(0.15f, 0.56f), new Vector2(0.55f, 0.9f), 20, TextAnchor.LowerLeft, Palette.TextMuted, FontStyle.Bold);
            _scoreText = UiFactory.Label(bar.transform, "Score", new Vector2(0.15f, 0.2f), new Vector2(0.55f, 0.56f), 36, TextAnchor.UpperLeft, Palette.TextPrimary, FontStyle.Bold);

            _ammoCaption = UiFactory.Label(bar.transform, "AmmoCaption", new Vector2(0.55f, 0.58f), new Vector2(0.975f, 0.88f), 18, TextAnchor.LowerRight, Palette.TextMuted, FontStyle.Bold,
                Loc.NeedsCjk ? "剩余弹药" : "SHOTS LEFT");

            var ammoGo = UiFactory.NewUiObject("AmmoRow", bar.transform);
            _ammoRow = ammoGo.GetComponent<RectTransform>();
            UiFactory.Anchor(_ammoRow, new Vector2(0.55f, 0.3f), new Vector2(0.975f, 0.52f));

            // The track runs to the three-star score, with ticks for pass and two stars, so the
            // player can see what another shot is worth instead of only "goal reached".
            var goalTrack = Bar(bar.transform, "GoalTrack", new Vector2(0.15f, 0.09f), new Vector2(0.975f, 0.17f), Palette.BackdropBottom);
            _goalFill = Bar(goalTrack.transform, "GoalFill", Vector2.zero, new Vector2(0.01f, 1f), Palette.Accent);
            _goalTick = Bar(goalTrack.transform, "TickGoal", new Vector2(0.3f, -0.6f), new Vector2(0.304f, 1.6f), Palette.TextMuted);
            _twoStarTick = Bar(goalTrack.transform, "TickTwoStar", new Vector2(0.6f, -0.6f), new Vector2(0.604f, 1.6f), Palette.StarGold);

            // Coaching only: it fades out once the player has clearly got the gesture.
            var hintChip = UiFactory.Panel(_playRoot.transform, "HintChip", new Vector2(0.14f, 0.20f), new Vector2(0.86f, 0.248f), Palette.HudFill);
            _hintGroup = hintChip.gameObject.AddComponent<CanvasGroup>();
            _hintGroup.blocksRaycasts = false;
            _hintText = UiFactory.Label(hintChip.transform, "Hint", Vector2.zero, Vector2.one, 24, TextAnchor.MiddleCenter, Palette.Accent, FontStyle.Bold,
                Loc.NeedsCjk ? "拖动瞄准 · 松手发射" : "DRAG TO AIM · RELEASE TO FIRE");

            _flash = UiFactory.Scrim(_playRoot.transform, "Flash", new Color(1f, 1f, 1f, 0f));
            _flash.raycastTarget = false;

            BuildResult();
        }

        /// <summary>Flat rectangle. Rounded panels turn into lozenges at track / tick thickness.</summary>
        static Image Bar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = UiFactory.NewUiObject(name, parent);
            var image = go.AddComponent<Image>();
            image.sprite = Shapes.Solid;
            image.color = color;
            image.raycastTarget = false;
            UiFactory.Anchor(image.rectTransform, anchorMin, anchorMax);
            return image;
        }

        /// <summary>Rebuilds the magazine so remaining shots are countable at a glance.</summary>
        void BuildAmmoPips(int count)
        {
            for (var i = _ammoRow.childCount - 1; i >= 0; i--)
            {
                Destroy(_ammoRow.GetChild(i).gameObject);
            }

            _ammoPips.Clear();
            if (count <= 0)
            {
                return;
            }

            var slot = 1f / count;
            var gap = slot * 0.42f;
            for (var i = 0; i < count; i++)
            {
                var pip = UiFactory.Panel(
                    _ammoRow, "Pip" + i,
                    new Vector2(i * slot + gap * 0.5f, 0f),
                    new Vector2((i + 1) * slot - gap * 0.5f, 1f),
                    // Higher multiplier keeps the corner radius small, so pips read as bullets in a
                    // magazine rather than as a row of red ovals.
                    Palette.Accent, 1.4f);
                pip.raycastTarget = false;
                _ammoPips.Add(pip);
            }
        }

        void BuildResult()
        {
            _resultRoot = FullScreenRoot("Result", _playRoot.transform);
            UiFactory.Scrim(_resultRoot.transform, "Scrim", Palette.Scrim);

            var card = UiFactory.Panel(_resultRoot.transform, "Card", new Vector2(0.1f, 0.28f), new Vector2(0.9f, 0.72f), Palette.CardFill, 0.45f);
            UiFactory.AddDropShadow(card, 6f);

            _resultTitle = UiFactory.Label(card.transform, "Title", new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f), 56, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold);

            for (var s = 0; s < 3; s++)
            {
                var starGo = UiFactory.NewUiObject("Star" + s, card.transform);
                var star = starGo.AddComponent<Image>();
                star.sprite = Shapes.Star;
                star.raycastTarget = false;
                var x = 0.28f + s * 0.16f;
                UiFactory.Anchor(star.rectTransform, new Vector2(x, 0.56f), new Vector2(x + 0.14f, 0.76f));
                _resultStars.Add(star);
            }

            _resultScore = UiFactory.Label(card.transform, "Score", new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.55f), 34, TextAnchor.MiddleCenter, Palette.TextMuted, FontStyle.Bold);

            _nextButton = UiFactory.Pill(card.transform, Loc.NeedsCjk ? "下一关" : "NEXT", new Vector2(0.10f, 0.24f), new Vector2(0.90f, 0.38f), Palette.Accent, Palette.TextOnAccent, 36, PlayNext);
            _replayLabel = UiFactory.Pill(card.transform, Loc.NeedsCjk ? "重试" : "RETRY", new Vector2(0.10f, 0.08f), new Vector2(0.48f, 0.21f), Palette.HudPanel, Palette.TextPrimary, 30,
                () => StartStage(_stageIndex)).GetComponentInChildren<Text>();
            UiFactory.Pill(card.transform, Loc.NeedsCjk ? "主页" : "HOME", new Vector2(0.52f, 0.08f), new Vector2(0.90f, 0.21f), Palette.HudPanel, Palette.TextPrimary, 30,
                () => Show(Screen.Home));

            _resultRoot.SetActive(false);
        }

        // ---------------------------------------------------------------- settings

        void BuildSettings()
        {
            _settingsRoot = FullScreenRoot("Settings");
            UiFactory.Scrim(_settingsRoot.transform, "Scrim", Palette.Scrim);

            var card = UiFactory.Panel(_settingsRoot.transform, "Card", new Vector2(0.12f, 0.32f), new Vector2(0.88f, 0.68f), Palette.CardFill, 0.45f);
            UiFactory.AddDropShadow(card, 6f);

            UiFactory.Label(card.transform, "Title", new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.95f), 40, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold,
                Loc.NeedsCjk ? "设置" : "SETTINGS");

            _sfxLabel = UiFactory.Pill(card.transform, string.Empty, new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.76f), Palette.HudPanel, Palette.TextPrimary, 30, ToggleSfx)
                .GetComponentInChildren<Text>();
            _hapticsLabel = UiFactory.Pill(card.transform, string.Empty, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.59f), Palette.HudPanel, Palette.TextPrimary, 30, ToggleHaptics)
                .GetComponentInChildren<Text>();
            _languageLabel = UiFactory.Pill(card.transform, string.Empty, new Vector2(0.1f, 0.28f), new Vector2(0.9f, 0.42f), Palette.HudPanel, Palette.TextPrimary, 30, ToggleLanguage)
                .GetComponentInChildren<Text>();

            UiFactory.Pill(card.transform, Loc.NeedsCjk ? "关闭" : "CLOSE", new Vector2(0.28f, 0.08f), new Vector2(0.72f, 0.22f), Palette.Accent, Palette.TextOnAccent, 32,
                () => SetSettingsVisible(false));

            _settingsRoot.SetActive(false);
        }

        void SetSettingsVisible(bool visible)
        {
            _settingsRoot.SetActive(visible);
            if (visible)
            {
                RefreshSettings();
            }
        }

        void RefreshSettings()
        {
            var on = Loc.NeedsCjk ? "开" : "ON";
            var off = Loc.NeedsCjk ? "关" : "OFF";
            _sfxLabel.text = (Loc.NeedsCjk ? "音效  " : "SOUND  ") + (Progress.SfxEnabled ? on : off);
            _hapticsLabel.text = (Loc.NeedsCjk ? "震动  " : "HAPTICS  ") + (Progress.HapticsEnabled ? on : off);
            _languageLabel.text = Loc.LanguageToggleCaption();
        }

        void ToggleSfx()
        {
            Progress.SfxEnabled = !Progress.SfxEnabled;
            RefreshSettings();
        }

        void ToggleHaptics()
        {
            Progress.HapticsEnabled = !Progress.HapticsEnabled;
            RefreshSettings();
        }

        void ToggleLanguage()
        {
            Loc.ToggleZhEn();
            UiFactory.RebindFonts(_canvas.transform);
            RefreshSettings();
        }

        // ---------------------------------------------------------------- run

        void StartStage(int index)
        {
            _stageIndex = Mathf.Clamp(index, 0, PaperLevelLibrary.Count - 1);
            _level = PaperLevelLibrary.Get(_stageIndex);
            Progress.LastPlayedStage = _stageIndex;
            Progress.MarkRunStarted();

            _field.LoadLevel(_level);
            _loop.StartRun(_level.Shots, _field.Zones.Count, _level.Goal);

            _resultRoot.SetActive(false);
            _stageNameText.text = (Loc.NeedsCjk ? _level.NameCn : _level.NameEn) + "  ·  " + (_stageIndex + 1) + "/" + PaperLevelLibrary.Count;
            BuildAmmoPips(_level.Shots);
            PlaceStarTicks();
            _shotsFired = 0;
            _hintGroup.alpha = 1f;
            OnAimKindChanged(PaperAimKind.Empty);
            RefreshHud();
            Show(Screen.Play);
        }

        void PlayNext()
        {
            var next = _stageIndex + 1;
            if (next >= PaperLevelLibrary.Count)
            {
                Show(Screen.Home);
                return;
            }

            StartStage(next);
        }

        void OnAimKindChanged(PaperAimKind kind)
        {
            if (_hintText == null)
            {
                return;
            }

            switch (kind)
            {
                case PaperAimKind.Face:
                    // Corrective, so it shows even after the intro hint has faded away.
                    _hintText.text = Loc.NeedsCjk ? "瞄准衣服" : "AIM AT THE CLOTHES";
                    _hintText.color = Palette.TextMuted;
                    _hintGroup.alpha = 1f;
                    break;
                case PaperAimKind.Clothes:
                    _hintText.text = Loc.NeedsCjk ? "松手撕下衣片" : "RELEASE TO TEAR";
                    _hintText.color = Palette.Accent;
                    _hintGroup.alpha = 1f;
                    break;
                default:
                    _hintText.text = Loc.NeedsCjk ? "拖动瞄准 · 松手发射" : "DRAG TO AIM · RELEASE TO FIRE";
                    _hintText.color = Palette.Accent;
                    _hintGroup.alpha = _shotsFired >= 2 ? 0f : 1f;
                    break;
            }
        }

        void OnShotResolved(PaperZone zone, Vector3 point)
        {
            if (!_loop.TryConsumeAmmo())
            {
                return;
            }

            _shotsFired++;
            if (zone != null)
            {
                _loop.RegisterDestruction(zone.Score, ScoreReason.Broken);
                _loop.RegisterTargetCleared();
                ScorePopup.Play(_field.EffectRoot, zone.transform.position, new ScoreAward(zone.Score, zone.Score, 1, ScoreReason.Broken));
            }

            _loop.NotifyShotResolved();
            RefreshHud();
            _field.InputEnabled = _loop.State == GameState.Playing;
        }

        void RefreshHud()
        {
            var reached = _loop.Score >= _level.Goal;
            _scoreText.text = reached
                ? $"{_loop.Score}  ✓"
                : $"{_loop.Score} / {_level.Goal}";
            _scoreText.color = reached ? Palette.Accent : Palette.TextPrimary;

            var span = Mathf.Max(1, _level.ThreeStarScore);
            var fill = Mathf.Clamp01(_loop.Score / (float)span);
            _goalFill.enabled = fill > 0.001f;
            _goalFill.rectTransform.anchorMax = new Vector2(fill, 1f);
            _goalFill.color = reached ? Palette.StarGold : Palette.Accent;

            for (var i = 0; i < _ammoPips.Count; i++)
            {
                var spent = i >= _loop.Ammo;
                _ammoPips[i].color = spent
                    ? new Color(Palette.TextMuted.r, Palette.TextMuted.g, Palette.TextMuted.b, 0.28f)
                    : Palette.Accent;
            }

            // The last shots are the tense ones, so they read gold instead of another red pip.
            if (_loop.Ammo > 0 && _loop.Ammo <= 2 && _loop.Ammo <= _ammoPips.Count)
            {
                _ammoPips[_loop.Ammo - 1].color = Palette.StarGold;
            }
        }

        void PlaceStarTicks()
        {
            var span = Mathf.Max(1, _level.ThreeStarScore);
            SetTick(_goalTick, _level.Goal / (float)span);
            SetTick(_twoStarTick, _level.TwoStarScore / (float)span);
        }

        static void SetTick(Image tick, float fraction)
        {
            var x = Mathf.Clamp(fraction, 0.02f, 0.99f);
            tick.rectTransform.anchorMin = new Vector2(x, -0.35f);
            tick.rectTransform.anchorMax = new Vector2(x + 0.006f, 1.35f);
        }

        void OnStateChanged(GameState state)
        {
            if (state != GameState.Cleared && state != GameState.Failed)
            {
                return;
            }

            _field.InputEnabled = false;
            var cleared = state == GameState.Cleared;
            var stars = cleared ? _level.StarsFor(_loop.Score) : 0;

            if (cleared)
            {
                Sfx.Instance?.Cleared();
                Progress.SubmitStage(_stageIndex, _loop.Score, stars);
                Progress.UnlockAfterClear(_stageIndex, PaperLevelLibrary.Count);
                _resultTitle.text = Loc.NeedsCjk ? "过关！" : "CLEARED!";
                _resultScore.text = Loc.NeedsCjk ? $"得分 {_loop.Score}" : $"SCORE {_loop.Score}";
            }
            else
            {
                Sfx.Instance?.Failed();
                var gap = Mathf.Max(0, _level.Goal - _loop.Score);
                _resultTitle.text = Loc.NeedsCjk ? "弹药耗尽" : "OUT OF AMMO";
                _resultScore.text = Loc.NeedsCjk ? $"还差 {gap} 分" : $"{gap} SHORT OF GOAL";
            }

            for (var i = 0; i < _resultStars.Count; i++)
            {
                _resultStars[i].color = i < stars ? Palette.StarGold : new Color(0.55f, 0.48f, 0.38f, 0.45f);
            }

            _replayLabel.text = cleared
                ? (Loc.NeedsCjk ? "再来" : "REPLAY")
                : (Loc.NeedsCjk ? "重试" : "RETRY");
            _nextButton.gameObject.SetActive(cleared && _stageIndex + 1 < PaperLevelLibrary.Count);

            if (cleared)
            {
                CameraShake.Instance?.Shake(0.35f);
            }

            StartCoroutine(RevealResult(cleared));
        }

        /// <summary>
        /// Holds the result card back for a beat so the last peel, confetti and (on a clear) the
        /// white flash all land before the overlay covers the field.
        /// </summary>
        IEnumerator RevealResult(bool cleared)
        {
            if (cleared && _flash != null)
            {
                StartCoroutine(FlashPulse());
            }

            yield return new WaitForSeconds(cleared ? 0.55f : 0.35f);
            _resultRoot.SetActive(true);
        }

        IEnumerator FlashPulse()
        {
            var peak = 0.5f;
            const float duration = 0.4f;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                var a = Mathf.Lerp(peak, 0f, t / duration);
                _flash.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }

            _flash.color = new Color(1f, 1f, 1f, 0f);
        }

        GameObject FullScreenRoot(string name, Transform parent = null)
        {
            var go = UiFactory.NewUiObject(name, parent != null ? parent : _canvas.transform);
            UiFactory.Anchor((RectTransform)go.transform, Vector2.zero, Vector2.one);
            return go;
        }
    }
}
