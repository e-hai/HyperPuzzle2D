using System.Collections;
using System.Collections.Generic;
using HyperPuzzle2D.Ads;
using HyperPuzzle2D.Art;
using HyperPuzzle2D.Board;
using HyperPuzzle2D.Feedback;
using HyperPuzzle2D.Input;
using HyperPuzzle2D.Localization;
using HyperPuzzle2D.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace HyperPuzzle2D.Core
{
    enum AppScreen
    {
        Splash,
        Home,
        StageSelect,
        Play,
    }

    /// <summary>
    /// W1 playable director: builds a smash level and wires cannon / ads / HUD.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        // Play field is authored against these half-extents; the camera fits them to any aspect.
        // The field is deliberately narrow: the camera has to show the full width, so every extra
        // unit of width buys nothing on a phone and only pushes the zoom out, shrinking the action.
        const float FieldHalfWidth = 4.2f;

        /// <summary>
        /// Floor for the camera size. Phones never reach it (their portrait aspect always asks
        /// for more), it only keeps a wide editor Game view from zooming into the field.
        /// </summary>
        const float MinOrthoSize = 7.2f;

        /// <summary>Narrowest portrait aspect worth supporting (~21:9); sizes the off-screen margins.</summary>
        const float TallestPhoneAspect = 0.42f;
        /// <summary>
        /// Kept short because the engine splash already showed the same mark while loading;
        /// this screen only has to bridge into the home screen, and a tap skips it.
        /// </summary>
        const float SplashDuration = 0.9f;

        // Shared edges for the HUD row, so the chips line up instead of drifting apart per element.
        const float HudLeft = 0.05f;
        const float HudRight = 0.95f;
        const float HudRowBottom = 0.862f;
        const float HudRowTop = 0.958f;

        /// <summary>
        /// Bottom of the pit targets fall into. Must stay below the tallest phone viewport
        /// (~-10) or blocks would blink out while still on screen.
        /// </summary>
        const float ClearY = -11.5f;

        /// <summary>
        /// Low enough that a 5-row stack still clears the HUD on a 16:9 phone, which is the
        /// squarest portrait screen and therefore the shortest viewport we render into.
        /// </summary>
        const float ShelfTopY = -0.1f;
        // Leaves a readable launch corridor between the muzzle and the shelf's left face.
        // At 1.0 the muzzle sat only ~0.26 world units from that face, so normal 30–45° shots
        // struck the shelf edge before they could rise into the structure.
        const float ShelfCenterX = 1.4f;
        const float BlockSize = 0.8f;

        /// <summary>
        /// Grid cell size. Must equal <see cref="BlockSize"/>: any slack makes every course drop
        /// onto the one below at spawn, and those stacked impacts shake structures apart.
        /// </summary>
        const float BlockPitch = BlockSize;

        /// <summary>
        /// Narrow enough to read as a support, wide enough that a stacked pair carrying a beam
        /// is not a knife edge.
        /// </summary>
        const float PillarWidth = 0.5f;

        /// <summary>
        /// Sits just below the shelf on purpose. A big drop between cannon and target forces a
        /// near-vertical arc, and those shots hit the underside of the shelf instead of the stack.
        /// </summary>
        static readonly Vector3 CannonPivot = new Vector3(-2.35f, -1.9f, 0f);

        /// <summary>Plinth surface the cannon stands on.</summary>
        const float PlinthTopY = -2.55f;

        [SerializeField] int reviveAmmo = 2;
        [SerializeField] RunMode runMode = RunMode.Stage;

        readonly GameLoop _loop = new GameLoop();
        readonly DailyChallenge _daily = new DailyChallenge();
        readonly List<DestructibleBlock> _blocks = new List<DestructibleBlock>();
        readonly List<Collider2D> _terrainColliders = new List<Collider2D>();
        readonly List<Button> _stageButtons = new List<Button>();
        readonly List<Text> _stageLabels = new List<Text>();

        IAdService _ads;
        CannonController _cannon;
        ComboPresenter _combo;
        ImpactFeedback _impactFeedback;
        ViewportFitter _viewportFitter;
        Transform _worldRoot;
        Transform _projectileRoot;
        Transform _effectRoot;
        Text _scoreText;
        Text _ammoText;
        Text _scoreCaption;
        Text _ammoCaption;
        Text _hudHomeLabel;
        Text _targetsText;
        Text _failScoreText;
        Text _clearScoreText;
        Text _failBestText;
        Text _clearBestText;
        Text _failTitle;
        Text _clearTitle;
        Text _failReviveLabel;
        Text _failRetryLabel;
        Text _failMenuLabel;
        Text _clearNextLabel;
        Text _clearMenuLabel;
        Text _menuTitle;
        Text _menuTagline;
        Text _menuBestText;
        Text _menuDailyBestText;
        Text _menuStageProgressText;
        Text _menuStagesLabel;
        Text _menuEndlessLabel;
        Text _menuDailyLabel;
        Text _sfxToggleLabel;
        Text _hapticsToggleLabel;
        Text _langToggleLabel;
        Text _splashTitle;
        Text _splashTagline;
        Text _splashHint;
        Text _stageSelectTitle;
        Text _stageBackLabel;
        Text _homeSettingsLabel;
        Text _settingsTitle;
        Text _settingsCloseLabel;
        Image _splashFill;
        Image _goalFill;
        Image _homeProgressFill;
        GameObject _hudRoot;
        GameObject _failPanel;
        GameObject _clearPanel;
        GameObject _menuPanel;
        GameObject _splashPanel;
        GameObject _stageSelectPanel;
        GameObject _settingsPanel;
        Transform _canvasRoot;
        FtueHint _ftueHint;
        LevelLayout _layout;
        AppScreen _screen = AppScreen.Splash;
        bool _shotInFlight;
        float _lastDestructionTime;
        Vector3 _lastScorePosition;
        int _seed;
        int _stageIndex;
        bool _splashFinished;
        Coroutine _splashRoutine;

        public GameLoop Loop => _loop;

        void Awake()
        {
            Instance = this;
            _ads = new MockAdService();
            gameObject.AddComponent<Sfx>();
            Loc.Changed += OnLanguageChanged;
            BuildWorld();
            BuildUi();
            _ads.ShowBanner();
            RefreshLocalizedUi();
        }

        void Start()
        {
            ShowSplash();
        }

        void Update()
        {
            if (_screen != AppScreen.Splash || _splashFinished)
            {
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.touchCount > 0)
            {
                FinishSplash();
            }
        }

        void OnDestroy()
        {
            Loc.Changed -= OnLanguageChanged;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void BeginRun(RunMode mode, int stageIndex = 0)
        {
            runMode = mode;
            _stageIndex = Mathf.Clamp(stageIndex, 0, Mathf.Max(0, LevelLibrary.Count - 1));
            Progress.MarkRunStarted();

            if (mode == RunMode.Daily)
            {
                _seed = _daily.TodaySeed;
            }
            else if (mode == RunMode.Stage)
            {
                _seed = _stageIndex;
                Progress.LastPlayedStage = _stageIndex;
            }
            else
            {
                _seed = Random.Range(1, 999999);
            }

            SetScreen(AppScreen.Play);
            ClearBoard();
            SpawnStructure();
            _loop.StartRun(mode, _layout.Ammo, _blocks.Count, _layout.TargetScore);
            _cannon.ArmAfterPointerRelease();
            _shotInFlight = false;
            if (_failPanel != null) _failPanel.SetActive(false);
            if (_clearPanel != null) _clearPanel.SetActive(false);
            RefreshHud();
            RefreshFtue();
            RefreshClearActions();
        }

        void BuildWorld()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.BackdropTop;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            var aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
            cam.orthographicSize = ViewportFitter.OrthoSizeFor(aspect, FieldHalfWidth, MinOrthoSize);

            if (FindAnyObjectByType<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }

            cam.gameObject.AddComponent<CameraShake>().SyncRestPosition();

            Physics2D.gravity = new Vector2(0f, -18f);

            // Default iteration counts are too low for tall stacks under this gravity: contacts
            // stay unresolved for a few frames and the structure visibly shivers apart.
            Physics2D.velocityIterations = 12;
            Physics2D.positionIterations = 8;

            _worldRoot = new GameObject("World").transform;
            _projectileRoot = new GameObject("Projectiles").transform;
            _effectRoot = new GameObject("Effects").transform;

            var backdrop = BuildBackdrop(cam);
            _viewportFitter = cam.gameObject.AddComponent<ViewportFitter>();
            _viewportFitter.Configure(FieldHalfWidth, MinOrthoSize, backdrop);

            BuildTerrain();
            BuildCannon();

            _combo = gameObject.AddComponent<ComboPresenter>();
            _impactFeedback = gameObject.AddComponent<ImpactFeedback>();
            _impactFeedback.Bind(_effectRoot);
            _loop.StateChanged += OnStateChanged;
            _loop.ScoreChanged += _ => RefreshHud();
            _loop.AmmoChanged += _ => RefreshHud();
            _loop.TargetsChanged += _ => RefreshHud();
            _loop.ComboChanged += c => _combo.ShowCombo(c);
            _loop.ScoreAwarded += OnScoreAwarded;
        }

        /// <summary>Returns the gradient transform so the viewport fitter can keep it full-bleed.</summary>
        Transform BuildBackdrop(Camera cam)
        {
            var root = new GameObject("Backdrop").transform;
            root.SetParent(_worldRoot);

            var viewHeight = cam.orthographicSize * 2f;
            var viewWidth = viewHeight * cam.aspect;

            var gradient = new GameObject("Gradient");
            gradient.transform.SetParent(root, false);
            gradient.transform.localScale = new Vector3(viewWidth, viewHeight, 1f);
            var gradientRenderer = gradient.AddComponent<SpriteRenderer>();
            gradientRenderer.sprite = Shapes.VerticalGradient(Palette.BackdropBottom, Palette.BackdropTop);
            gradientRenderer.sortingOrder = SortingOrders.Backdrop;

            // Warm pool of light behind the target stack draws the eye to the objective.
            var glow = new GameObject("Glow");
            glow.transform.SetParent(root, false);
            glow.transform.position = new Vector3(ShelfCenterX, ShelfTopY + 1.6f, 0f);
            glow.transform.localScale = Vector3.one * 11f;
            var glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = Shapes.Glow;
            glowRenderer.color = new Color(Palette.BackdropGlow.r, Palette.BackdropGlow.g, Palette.BackdropGlow.b, 0.32f);
            glowRenderer.sortingOrder = SortingOrders.BackdropGlow;

            // Spread over the tallest viewport a phone can ask for, so the squarer models do not
            // get a band of motes bunched into the middle of the screen.
            var moteHalfHeight = ViewportFitter.OrthoSizeFor(TallestPhoneAspect, FieldHalfWidth, MinOrthoSize);
            var rng = new System.Random(7);
            for (var i = 0; i < 22; i++)
            {
                var mote = new GameObject("Mote");
                mote.transform.SetParent(root, false);
                mote.transform.position = new Vector3(
                    Mathf.Lerp(-FieldHalfWidth, FieldHalfWidth, (float)rng.NextDouble()),
                    Mathf.Lerp(-moteHalfHeight, moteHalfHeight, (float)rng.NextDouble()),
                    0f);

                var size = Mathf.Lerp(0.08f, 0.26f, (float)rng.NextDouble());
                mote.transform.localScale = Vector3.one * size;

                var moteRenderer = mote.AddComponent<SpriteRenderer>();
                moteRenderer.sprite = Shapes.Circle;
                moteRenderer.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.04f, 0.14f, (float)rng.NextDouble()));
                moteRenderer.sortingOrder = SortingOrders.BackdropDecor;

                mote.AddComponent<FloatingMote>().Configure(
                    Mathf.Lerp(0.1f, 0.4f, (float)rng.NextDouble()),
                    Mathf.Lerp(0.2f, 0.6f, (float)rng.NextDouble()));
            }

            return gradient.transform;
        }

        void BuildTerrain()
        {
            // Both supports run off the bottom of the frame: the play field is far taller than the
            // action, and grounded pillars fill that space instead of leaving it empty.
            var plinthHeight = PlinthTopY - ClearY;
            CreateSolid("Plinth", new Vector3(CannonPivot.x, PlinthTopY - plinthHeight * 0.5f, 0f), new Vector2(2.6f, plinthHeight), Palette.Ground);
            CreateTrim("PlinthTrim", new Vector3(CannonPivot.x, PlinthTopY - 0.04f, 0f), new Vector2(2.6f, 0.09f), Palette.GroundEdge);

            // Support column: the target shelf is an island, so knocked-off blocks fall away.
            var shelfBottom = ShelfTopY - 0.5f;
            var columnHeight = shelfBottom - ClearY;
            CreateSolid("Column", new Vector3(ShelfCenterX, shelfBottom - columnHeight * 0.5f, 0f), new Vector2(1.1f, columnHeight), Palette.Wall);

            CreateSolid("Shelf", new Vector3(ShelfCenterX, ShelfTopY - 0.25f, 0f), new Vector2(4.4f, 0.5f), Palette.Shelf);
            CreateTrim("ShelfTrim", new Vector3(ShelfCenterX, ShelfTopY - 0.02f, 0f), new Vector2(4.4f, 0.09f), Palette.GroundEdge);
        }

        void BuildCannon()
        {
            var baseRenderer = CreateSlicedRenderer("CannonBase", new Vector3(CannonPivot.x, CannonPivot.y - 0.37f, 0f), new Vector2(1.7f, 0.56f), Palette.CannonBase, SortingOrders.Cannon);
            baseRenderer.transform.SetParent(_worldRoot);

            var cannonGo = new GameObject("Cannon");
            cannonGo.transform.SetParent(_worldRoot);
            cannonGo.transform.position = CannonPivot;

            var barrel = CreateSlicedRenderer("Barrel", Vector3.zero, new Vector2(0.44f, 1.15f), Palette.CannonBarrel, SortingOrders.Cannon);
            barrel.transform.SetParent(cannonGo.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            var hub = new GameObject("Hub");
            hub.transform.SetParent(cannonGo.transform, false);
            hub.transform.localScale = Vector3.one * 0.92f;
            var hubRenderer = hub.AddComponent<SpriteRenderer>();
            hubRenderer.sprite = Shapes.Circle;
            hubRenderer.color = Palette.CannonHub;
            hubRenderer.sortingOrder = SortingOrders.Cannon + 1;

            var muzzle = new GameObject("MuzzleGlow");
            muzzle.transform.SetParent(cannonGo.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            muzzle.transform.localScale = Vector3.one * 1.4f;
            var muzzleRenderer = muzzle.AddComponent<SpriteRenderer>();
            muzzleRenderer.sprite = Shapes.Glow;
            muzzleRenderer.color = new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, 0.45f);
            muzzleRenderer.sortingOrder = SortingOrders.Cannon + 2;

            _cannon = cannonGo.AddComponent<CannonController>();
            _cannon.Fired += OnCannonFired;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas");
            _canvasRoot = canvasGo.transform;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                var baseInput = es.AddComponent<UnityEngine.EventSystems.BaseInput>();
                var inputModule = es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                inputModule.inputOverride = baseInput;
            }

            _hudRoot = UiFactory.NewUiObject("Hud", canvasGo.transform);
            UiFactory.Anchor((RectTransform)_hudRoot.transform, Vector2.zero, Vector2.one);

            // One row, equal gutters, shared chip height: home on the left where a back action is
            // expected, the objective in the middle at the largest size, ammo on the right.
            _hudHomeLabel = UiFactory.Pill(_hudRoot.transform, Loc.Get("common.menu"), new Vector2(HudLeft, HudRowBottom), new Vector2(0.20f, HudRowTop), Palette.HudPanel, Palette.TextPrimary, 26, ShowHome).GetComponentInChildren<Text>();

            _scoreText = CreateHudChip(_hudRoot.transform, "ScoreChip", new Vector2(0.225f, HudRowBottom), new Vector2(0.715f, HudRowTop), Loc.Get("hud.scoreCaption"), TextAnchor.MiddleLeft, out _scoreCaption, out var scoreChip);

            // The goal bar belongs to the score, so it lives inside that chip. As a full width
            // strip of its own it was the loudest thing on screen while carrying the least meaning.
            // A groove darker than the chip, not a lighter strip: an empty track tinted brighter
            // than its panel reads as a bar that is already full.
            var goalTrack = UiFactory.Panel(scoreChip.transform, "GoalTrack", new Vector2(0.06f, 0.11f), new Vector2(0.94f, 0.21f), new Color(0f, 0f, 0f, 0.32f), 3f);
            _goalFill = UiFactory.Panel(goalTrack.transform, "GoalFill", Vector2.zero, new Vector2(0f, 1f), Palette.Accent, 3f);

            _ammoText = CreateHudChip(_hudRoot.transform, "AmmoChip", new Vector2(0.74f, HudRowBottom), new Vector2(HudRight, HudRowTop), Loc.Get("hud.ammoCaption"), TextAnchor.MiddleCenter, out _ammoCaption, out _);

            _targetsText = UiFactory.Label(_hudRoot.transform, "Targets", new Vector2(HudLeft, 0.812f), new Vector2(HudRight, 0.852f), 26, TextAnchor.MiddleCenter, Palette.TextMuted);

            var comboLabel = UiFactory.Label(_hudRoot.transform, "Combo", new Vector2(0.1f, 0.56f), new Vector2(0.9f, 0.68f), 84, TextAnchor.MiddleCenter, Palette.Accent, FontStyle.Bold);
            UiFactory.AddDropShadow(comboLabel, 5f);
            comboLabel.gameObject.SetActive(false);
            _combo.Bind(comboLabel);

            _ftueHint = FtueHint.Create(_hudRoot.transform, Loc.Get("ftue.aim"));

            _failPanel = BuildResultPanel(
                canvasGo.transform,
                "FailPanel",
                Loc.Get("fail.title"),
                out _failScoreText,
                out _failBestText,
                out _failTitle,
                card =>
                {
                    _failReviveLabel = UiFactory.Pill(card, Loc.Get("fail.revive"), new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.47f), Palette.Accent, Palette.TextOnAccent, 34, OnReviveClicked).GetComponentInChildren<Text>();
                    _failRetryLabel = UiFactory.Pill(card, Loc.Get("fail.retry"), new Vector2(0.08f, 0.19f), new Vector2(0.92f, 0.32f), Palette.CannonHub, Palette.TextPrimary, 34, () =>
                    {
                        ShowInterstitialThen(RetryCurrentRun);
                    }).GetComponentInChildren<Text>();
                    _failMenuLabel = UiFactory.Pill(card, Loc.Get("common.menu"), new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.17f), Palette.HudFill, Palette.TextPrimary, 30, ShowHome).GetComponentInChildren<Text>();
                });

            _clearPanel = BuildResultPanel(
                canvasGo.transform,
                "ClearPanel",
                Loc.Get("clear.title"),
                out _clearScoreText,
                out _clearBestText,
                out _clearTitle,
                card =>
                {
                    _clearNextLabel = UiFactory.Pill(card, Loc.Get("clear.next"), new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.4f), Palette.AccentCool, Palette.TextOnAccent, 38, OnClearNextClicked).GetComponentInChildren<Text>();
                    _clearMenuLabel = UiFactory.Pill(card, Loc.Get("common.menu"), new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.21f), Palette.HudFill, Palette.TextPrimary, 32, ShowHome).GetComponentInChildren<Text>();
                });

            _menuPanel = BuildHome(canvasGo.transform);
            _stageSelectPanel = BuildStageSelect(canvasGo.transform);
            _settingsPanel = BuildSettings(canvasGo.transform);
            _splashPanel = BuildSplash(canvasGo.transform);
            SetHudVisible(false);
        }

        GameObject BuildSplash(Transform parent)
        {
            var root = UiFactory.NewUiObject("SplashPanel", parent);
            UiFactory.Anchor((RectTransform)root.transform, Vector2.zero, Vector2.one);

            UiFactory.Scrim(root.transform, "Scrim", Palette.BackdropTop);

            // Same artwork and background as the engine splash, so the handover between the two
            // reads as one screen that simply becomes interactive.
            var mark = UiFactory.NewUiObject("Mark", root.transform);
            UiFactory.Anchor((RectTransform)mark.transform, new Vector2(0.28f, 0.68f), new Vector2(0.72f, 0.86f));
            var markImage = mark.AddComponent<Image>();
            markImage.sprite = BrandMark.MarkSprite;
            markImage.preserveAspect = true;

            _splashTitle = UiFactory.Label(root.transform, "Title", new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.66f), 72, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold, Loc.Get("menu.title"));
            UiFactory.AddDropShadow(_splashTitle, 5f);

            _splashTagline = UiFactory.Label(root.transform, "Tagline", new Vector2(0.1f, 0.44f), new Vector2(0.9f, 0.52f), 28, TextAnchor.MiddleCenter, Palette.TextMuted, FontStyle.Normal, Loc.Get("menu.tagline"));

            var track = UiFactory.Panel(root.transform, "ProgressTrack", new Vector2(0.22f, 0.34f), new Vector2(0.78f, 0.365f), Palette.HudFill, 4f);
            _splashFill = UiFactory.Panel(track.transform, "ProgressFill", new Vector2(0f, 0f), new Vector2(0.08f, 1f), Palette.Accent, 4f);

            _splashHint = UiFactory.Label(root.transform, "Hint", new Vector2(0.1f, 0.24f), new Vector2(0.9f, 0.3f), 24, TextAnchor.MiddleCenter, Palette.TextMuted, FontStyle.Normal, Loc.Get("splash.tap"));

            root.SetActive(false);
            return root;
        }

        /// <summary>
        /// A full screen of its own, not a dialog: the home screen owns progression and mode
        /// choice, while the settings dialog on top of it owns the toggles.
        /// </summary>
        GameObject BuildHome(Transform parent)
        {
            var root = UiFactory.NewUiObject("HomePanel", parent);
            UiFactory.Anchor((RectTransform)root.transform, Vector2.zero, Vector2.one);

            UiFactory.Scrim(root.transform, "Backdrop", Palette.BackdropTop);

            _menuTitle = UiFactory.Label(root.transform, "Title", new Vector2(0.08f, 0.79f), new Vector2(0.92f, 0.875f), 66, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold, Loc.Get("menu.title"));
            UiFactory.AddDropShadow(_menuTitle, 5f);

            _menuTagline = UiFactory.Label(root.transform, "Tagline", new Vector2(0.08f, 0.745f), new Vector2(0.92f, 0.785f), 26, TextAnchor.MiddleCenter, Palette.TextMuted, FontStyle.Normal, Loc.Get("menu.tagline"));

            // Kept clear of the status bar / notch, which the canvas does not inset for us.
            _homeSettingsLabel = UiFactory.Pill(root.transform, Loc.Get("menu.settings"), new Vector2(0.7f, 0.895f), new Vector2(0.94f, 0.945f), Palette.HudFill, Palette.TextPrimary, 24, ShowSettings).GetComponentInChildren<Text>();

            var progressCard = UiFactory.Panel(root.transform, "ProgressCard", new Vector2(0.08f, 0.525f), new Vector2(0.92f, 0.7f), Palette.CardFill, 0.5f);

            _menuStageProgressText = UiFactory.Label(progressCard.transform, "StageProgress", new Vector2(0.06f, 0.58f), new Vector2(0.94f, 0.92f), 32, TextAnchor.MiddleCenter, Palette.Accent, FontStyle.Bold);

            var progressTrack = UiFactory.Panel(progressCard.transform, "Track", new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.53f), Palette.HudFill, 4f);
            _homeProgressFill = UiFactory.Panel(progressTrack.transform, "Fill", Vector2.zero, new Vector2(0f, 1f), Palette.Accent, 4f);

            _menuBestText = UiFactory.Label(progressCard.transform, "Best", new Vector2(0.08f, 0.1f), new Vector2(0.5f, 0.36f), 24, TextAnchor.MiddleLeft, Palette.AccentCool, FontStyle.Bold);
            _menuDailyBestText = UiFactory.Label(progressCard.transform, "DailyBest", new Vector2(0.5f, 0.1f), new Vector2(0.92f, 0.36f), 22, TextAnchor.MiddleRight, Palette.TextMuted);

            _menuStagesLabel = UiFactory.Pill(root.transform, Loc.Get("menu.stages"), new Vector2(0.12f, 0.365f), new Vector2(0.88f, 0.475f), Palette.Accent, Palette.TextOnAccent, 40, () =>
            {
                Sfx.Instance?.Ui();
                ShowStageSelect();
            }).GetComponentInChildren<Text>();

            _menuDailyLabel = UiFactory.Pill(root.transform, Loc.Get("menu.daily"), new Vector2(0.12f, 0.25f), new Vector2(0.88f, 0.345f), Palette.AccentCool, Palette.TextOnAccent, 30, () =>
            {
                Sfx.Instance?.Ui();
                BeginRun(RunMode.Daily);
            }).GetComponentInChildren<Text>();

            _menuEndlessLabel = UiFactory.Pill(root.transform, Loc.Get("menu.endless"), new Vector2(0.12f, 0.135f), new Vector2(0.88f, 0.23f), Palette.CannonHub, Palette.TextPrimary, 30, () =>
            {
                Sfx.Instance?.Ui();
                BeginRun(RunMode.Endless);
            }).GetComponentInChildren<Text>();

            root.SetActive(false);
            return root;
        }

        GameObject BuildSettings(Transform parent)
        {
            var root = UiFactory.NewUiObject("SettingsPanel", parent);
            UiFactory.Anchor((RectTransform)root.transform, Vector2.zero, Vector2.one);

            UiFactory.Scrim(root.transform, "Scrim", Palette.Scrim);

            var card = UiFactory.Panel(root.transform, "Card", new Vector2(0.1f, 0.31f), new Vector2(0.9f, 0.69f), Palette.CardFill, 0.45f);
            UiFactory.Panel(card.transform, "Accent", new Vector2(0.35f, 0.95f), new Vector2(0.65f, 0.975f), Palette.Accent, 3f);

            _settingsTitle = UiFactory.Label(card.transform, "Title", new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.92f), 40, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold, Loc.Get("settings.title"));

            _sfxToggleLabel = UiFactory.Pill(card.transform, SettingsCaption(true), new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.75f), Palette.HudFill, Palette.TextPrimary, 28, ToggleSfx).GetComponentInChildren<Text>();
            _hapticsToggleLabel = UiFactory.Pill(card.transform, SettingsCaption(false), new Vector2(0.1f, 0.46f), new Vector2(0.9f, 0.59f), Palette.HudFill, Palette.TextPrimary, 28, ToggleHaptics).GetComponentInChildren<Text>();
            _langToggleLabel = UiFactory.Pill(card.transform, Loc.LanguageToggleCaption(), new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.43f), Palette.CannonHub, Palette.TextPrimary, 28, ToggleLanguage).GetComponentInChildren<Text>();

            _settingsCloseLabel = UiFactory.Pill(card.transform, Loc.Get("settings.close"), new Vector2(0.2f, 0.08f), new Vector2(0.8f, 0.21f), Palette.Accent, Palette.TextOnAccent, 30, HideSettings).GetComponentInChildren<Text>();

            root.SetActive(false);
            return root;
        }

        GameObject BuildStageSelect(Transform parent)
        {
            var root = UiFactory.NewUiObject("StageSelectPanel", parent);
            UiFactory.Anchor((RectTransform)root.transform, Vector2.zero, Vector2.one);

            UiFactory.Scrim(root.transform, "Scrim", Palette.Scrim);

            var card = UiFactory.Panel(root.transform, "Card", new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.9f), Palette.CardFill, 0.45f);
            UiFactory.Panel(card.transform, "Accent", new Vector2(0.35f, 0.955f), new Vector2(0.65f, 0.978f), Palette.Accent, 3f);

            _stageSelectTitle = UiFactory.Label(card.transform, "Title", new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.95f), 42, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold, Loc.Get("stage.title"));

            _stageButtons.Clear();
            _stageLabels.Clear();

            const int columns = 2;
            var count = LevelLibrary.Count;
            for (var i = 0; i < count; i++)
            {
                var index = i;
                var col = i % columns;
                var row = i / columns;
                var rows = (count + columns - 1) / columns;

                var x0 = 0.08f + col * 0.44f;
                var x1 = x0 + 0.38f;
                var top = 0.82f - row * (0.58f / Mathf.Max(1, rows));
                var bottom = top - 0.1f;

                var button = UiFactory.Pill(
                    card.transform,
                    Loc.Format("stage.item", i + 1, Loc.LevelName(LevelLibrary.Get(i).Name)),
                    new Vector2(x0, bottom),
                    new Vector2(x1, top),
                    Palette.HudFill,
                    Palette.TextPrimary,
                    24,
                    () => OnStageChosen(index));

                _stageButtons.Add(button);
                _stageLabels.Add(button.GetComponentInChildren<Text>());
            }

            _stageBackLabel = UiFactory.Pill(card.transform, Loc.Get("common.back"), new Vector2(0.2f, 0.04f), new Vector2(0.8f, 0.12f), Palette.CannonHub, Palette.TextPrimary, 28, () =>
            {
                Sfx.Instance?.Ui();
                ShowHome();
            }).GetComponentInChildren<Text>();

            root.SetActive(false);
            return root;
        }

        static string SettingsCaption(bool sfx)
        {
            if (sfx)
            {
                return Progress.SfxEnabled ? Loc.Get("menu.sfx.on") : Loc.Get("menu.sfx.off");
            }

            return Progress.HapticsEnabled ? Loc.Get("menu.haptics.on") : Loc.Get("menu.haptics.off");
        }

        void ToggleLanguage()
        {
            Sfx.Instance?.Ui();
            Loc.ToggleZhEn();
        }

        void ToggleSfx()
        {
            Progress.SfxEnabled = !Progress.SfxEnabled;
            if (_sfxToggleLabel != null)
            {
                _sfxToggleLabel.text = SettingsCaption(true);
            }

            if (Progress.SfxEnabled)
            {
                Sfx.Instance?.Ui();
            }
        }

        void ToggleHaptics()
        {
            Progress.HapticsEnabled = !Progress.HapticsEnabled;
            if (_hapticsToggleLabel != null)
            {
                _hapticsToggleLabel.text = SettingsCaption(false);
            }

            Sfx.Instance?.Ui();
            if (Progress.HapticsEnabled)
            {
                Haptics.Light();
            }
        }

        /// <summary>
        /// Every chip shares these internal proportions so captions and values line up across the
        /// row. The band under the value is left empty on purpose: the score chip fills it with the
        /// goal bar, and reserving it everywhere keeps the numbers on a common baseline.
        /// </summary>
        static Text CreateHudChip(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string caption, TextAnchor alignment, out Text captionText, out Image chip)
        {
            chip = UiFactory.Panel(parent, name, anchorMin, anchorMax, Palette.HudPanel);
            captionText = UiFactory.Label(chip.transform, "Caption", new Vector2(0.06f, 0.60f), new Vector2(0.94f, 0.93f), 24, alignment, Palette.TextMuted, FontStyle.Normal, caption);
            var value = UiFactory.Label(chip.transform, "Value", new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.62f), 46, alignment, Palette.TextPrimary, FontStyle.Bold);
            return value;
        }

        static GameObject BuildResultPanel(Transform parent, string name, string title, out Text scoreText, out Text bestText, out Text titleText, System.Action<Transform> buildActions)
        {
            var root = UiFactory.NewUiObject(name, parent);
            UiFactory.Anchor((RectTransform)root.transform, Vector2.zero, Vector2.one);

            UiFactory.Scrim(root.transform, "Scrim", Palette.Scrim);

            var card = UiFactory.Panel(root.transform, "Card", new Vector2(0.1f, 0.24f), new Vector2(0.9f, 0.76f), Palette.CardFill, 0.45f);
            UiFactory.Panel(card.transform, "Accent", new Vector2(0.35f, 0.95f), new Vector2(0.65f, 0.975f), Palette.Accent, 3f);

            titleText = UiFactory.Label(card.transform, "Title", new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.92f), 58, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold, title);
            UiFactory.AddDropShadow(titleText, 4f);

            scoreText = UiFactory.Label(card.transform, "Score", new Vector2(0.06f, 0.63f), new Vector2(0.94f, 0.76f), 38, TextAnchor.MiddleCenter, Palette.TextMuted);
            bestText = UiFactory.Label(card.transform, "Best", new Vector2(0.06f, 0.53f), new Vector2(0.94f, 0.62f), 30, TextAnchor.MiddleCenter, Palette.TextMuted);

            buildActions(card.transform);
            root.SetActive(false);
            return root;
        }

        void SpawnStructure()
        {
            _layout = runMode == RunMode.Stage
                ? LevelLibrary.Get(_stageIndex)
                : LevelLibrary.Pick(_seed);
            var originX = ShelfCenterX - (_layout.Width - 1) * BlockPitch * 0.5f;

            for (var row = 0; row < _layout.Height; row++)
            {
                var column = 0;
                while (column < _layout.Width)
                {
                    var kind = _layout.PieceAt(column, row);
                    if (kind == PieceKind.None)
                    {
                        column++;
                        continue;
                    }

                    // Adjacent beam cells fuse into one body so they can bridge their supports.
                    var span = 1;
                    if (kind == PieceKind.Beam)
                    {
                        while (column + span < _layout.Width && _layout.PieceAt(column + span, row) == PieceKind.Beam)
                        {
                            span++;
                        }
                    }

                    var x = originX + (column + (span - 1) * 0.5f) * BlockPitch;
                    SpawnPiece(kind, x, ShelfTopY + row * BlockPitch, row, span);
                    column += span;
                }
            }

            // Freeze the finished structure. It stays exactly as authored until the first shot.
            foreach (var block in _blocks)
            {
                block.Park();
            }
        }

        /// <summary>Builds one piece. Pieces are bottom-aligned in their cell so courses stack cleanly.</summary>
        void SpawnPiece(PieceKind kind, float x, float cellBottom, int row, int span)
        {
            if (kind == PieceKind.Ball)
            {
                const float radius = 0.34f;
                var ball = CreateCircleRenderer("Ball", new Vector3(x, cellBottom + radius, 0f), radius, Palette.Can, SortingOrders.Block);
                ball.transform.SetParent(_worldRoot);
                AddCircleShadow(ball.transform);

                var ballCollider = ball.gameObject.AddComponent<CircleCollider2D>();
                ballCollider.radius = 0.5f;
                ballCollider.sharedMaterial = PieceMaterial;

                var ballBody = ball.gameObject.AddComponent<Rigidbody2D>();
                ballBody.mass = 0.5f;
                ballBody.linearDamping = 0.2f;

                // A ball on a flat top sits in neutral equilibrium: nothing pushes it back once
                // it starts rolling, so it needs far more angular drag than the boxes.
                ballBody.angularDamping = 3.5f;

                RegisterTarget(ball.gameObject, kind);
                return;
            }

            Vector2 size;
            Color color;
            float mass;

            switch (kind)
            {
                case PieceKind.Pillar:
                    size = new Vector2(PillarWidth, BlockSize);
                    color = Palette.Pillar;
                    mass = 0.6f;
                    break;
                case PieceKind.Beam:
                    size = new Vector2(span * BlockPitch - (BlockPitch - BlockSize), BlockSize);
                    color = Palette.Beam;
                    mass = span;
                    break;
                case PieceKind.Heavy:
                    size = new Vector2(BlockSize, BlockSize);
                    color = Palette.Heavy;
                    mass = 3.2f;
                    break;
                case PieceKind.Brittle:
                    size = new Vector2(BlockSize, BlockSize);
                    color = Color.Lerp(Palette.Brittle, Palette.Blocks[4], Mathf.Clamp01(row * 0.16f));
                    mass = 0.72f;
                    break;
                case PieceKind.Explosive:
                    size = new Vector2(BlockSize, BlockSize);
                    color = Palette.Explosive;
                    mass = 0.8f;
                    break;
                default:
                    size = new Vector2(BlockSize, BlockSize);
                    color = Palette.Blocks[row % Palette.Blocks.Length];
                    mass = 1f;
                    break;
            }

            var piece = CreateSlicedRenderer(kind.ToString(), new Vector3(x, cellBottom + size.y * 0.5f, 0f), size, color, SortingOrders.Block);
            piece.transform.SetParent(_worldRoot);
            AddBoxShadow(piece.transform, size);
            var collider = piece.gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.sharedMaterial = PieceMaterial;

            var body = piece.gameObject.AddComponent<Rigidbody2D>();
            body.mass = mass;
            body.linearDamping = 0.2f;
            body.angularDamping = 0.4f;

            if (kind == PieceKind.Explosive)
            {
                var core = CreateCircleRenderer("Core", Vector3.zero, 0.18f, Palette.ExplosionCore, SortingOrders.Block + 1);
                core.transform.SetParent(piece.transform, false);
                core.transform.localPosition = Vector3.zero;
            }

            RegisterTarget(piece.gameObject, kind);
        }

        void RegisterTarget(GameObject target, PieceKind kind)
        {
            var destructible = target.AddComponent<DestructibleBlock>();
            destructible.Configure(
                MaterialFor(kind),
                ShelfTopY,
                ShelfCenterX - 2.2f,
                ShelfCenterX + 2.2f,
                ClearY);
            destructible.Damaged += OnBlockDamaged;
            destructible.Broken += OnBlockBroken;
            destructible.KnockedOff += OnBlockKnockedOff;
            _blocks.Add(destructible);
        }

        static DestructionMaterial MaterialFor(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Brittle: return DestructionMaterial.Brittle;
                case PieceKind.Explosive: return DestructionMaterial.Explosive;
                case PieceKind.Heavy: return DestructionMaterial.Heavy;
                case PieceKind.Pillar: return DestructionMaterial.Support;
                case PieceKind.Beam: return DestructionMaterial.Beam;
                case PieceKind.Ball: return DestructionMaterial.Ball;
                default: return DestructionMaterial.Normal;
            }
        }

        void ClearBoard()
        {
            foreach (var block in _blocks)
            {
                if (block != null)
                {
                    Destroy(block.gameObject);
                }
            }

            _blocks.Clear();
            for (var i = _projectileRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_projectileRoot.GetChild(i).gameObject);
            }

            for (var i = _effectRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_effectRoot.GetChild(i).gameObject);
            }
        }

        void OnCannonFired(Vector2 velocity)
        {
            if (_shotInFlight || !_loop.TryConsumeAmmo())
            {
                return;
            }

            _shotInFlight = true;
            _lastDestructionTime = Time.time;
            _cannon.CanFire = false;
            _cannon.Kick();
            CompleteFtue();
            Sfx.Instance?.Fire();
            Haptics.Light();

            foreach (var block in _blocks)
            {
                if (block != null)
                {
                    block.Release();
                }
            }

            var spawn = _cannon.MuzzlePosition;
            const float projectileRadius = 0.22f;
            var ball = CreateCircleRenderer("Projectile", spawn, projectileRadius, Palette.Projectile, SortingOrders.Projectile);
            ball.transform.SetParent(_projectileRoot);
            // Shapes.Circle is one unit wide and the transform carries the 0.44 diameter, so a
            // local radius of 0.5 exactly matches the 0.22 world-space visual radius.
            var projectileCollider = ball.gameObject.AddComponent<CircleCollider2D>();
            projectileCollider.radius = 0.5f;
            foreach (var terrainCollider in _terrainColliders)
            {
                if (terrainCollider != null)
                {
                    Physics2D.IgnoreCollision(projectileCollider, terrainCollider);
                }
            }

            var trail = ball.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = 0.34f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, 0.75f);
            trail.endColor = new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, 0f);
            trail.sortingOrder = SortingOrders.Trail;

            var body = ball.gameObject.AddComponent<Rigidbody2D>();
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.mass = 1.35f;
            body.linearDamping = 0.05f;
            body.sharedMaterial = ProjectileMaterial;

            var projectile = ball.gameObject.AddComponent<Projectile>();
            var hitWatcher = ball.gameObject.AddComponent<ProjectileHitWatcher>();
            hitWatcher.Hit += OnProjectileHit;
            hitWatcher.SurfaceHit += OnProjectileSurfaceHit;

            HitBurst.Play(_effectRoot, spawn, Palette.Accent, 6, 3.5f);
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.06f);
            }

            projectile.Launch(velocity, _ => StartCoroutine(ResolveShotRoutine()));
            RefreshHud();
        }

        void OnProjectileHit(DestructibleBlock block, ImpactEvent impact)
        {
            _lastScorePosition = impact.Point;
            _loop.RegisterDirectHit(block.HitScore);
            block.ApplyImpact(impact);
            ApplyShockwave(impact.Point, 1.6f, impact.Energy * 0.85f, block);
        }

        void OnProjectileSurfaceHit(ImpactEvent impact)
        {
            // Shelf/ground impacts are still satisfying and forgiving: their shock travels into
            // nearby pieces instead of turning a shot a few pixels low into a silent miss.
            _impactFeedback?.PlayContact(impact, Palette.GroundEdge);
            ApplyShockwave(impact.Point, 1.25f, impact.Energy * 0.78f, null);
        }

        IEnumerator ResolveShotRoutine()
        {
            // Destruction continues visually, but input only waits for a short quiet window.
            // This preserves the cascade without turning each shot into several seconds of idle.
            var deadline = Time.time + 1f;
            while (Time.time < deadline && Time.time - _lastDestructionTime < 0.45f)
            {
                yield return null;
            }

            _shotInFlight = false;
            _loop.NotifyShotResolved();
            if (_loop.State == GameState.Playing)
            {
                _loop.ResetCombo();
                _cannon.CanFire = true;
            }

            RefreshHud();
        }

        bool AnyTargetSettling()
        {
            foreach (var block in _blocks)
            {
                if (block != null && block.IsSettling)
                {
                    return true;
                }
            }

            return false;
        }

        void OnBlockDamaged(DestructibleBlock block, ImpactEvent impact)
        {
            var renderer = block.GetComponent<SpriteRenderer>();
            _impactFeedback?.PlayContact(impact, renderer != null ? renderer.color : Palette.Accent);
        }

        void OnBlockBroken(DestructibleBlock block, ImpactEvent impact)
        {
            _lastDestructionTime = Time.time;
            _lastScorePosition = block.transform.position;
            _loop.RegisterTargetCleared();
            var reason = block.Material == DestructionMaterial.Explosive ? ScoreReason.Explosion : ScoreReason.Broken;
            _loop.RegisterDestruction(block.BreakScore, reason);

            var renderer = block.GetComponent<SpriteRenderer>();
            var tint = renderer != null ? renderer.color : Palette.Accent;
            if (block.Material == DestructionMaterial.Explosive)
            {
                _impactFeedback?.PlayExplosion(block.transform.position, _loop.Combo);
                Explode(block.transform.position, block);
            }
            else
            {
                _impactFeedback?.PlayBreak(block.transform.position, tint, _loop.Combo);
            }
        }

        void OnBlockKnockedOff(DestructibleBlock block)
        {
            _lastDestructionTime = Time.time;
            _lastScorePosition = block.transform.position;
            _loop.RegisterTargetCleared();
            _loop.RegisterDestruction(block.KnockOffScore, ScoreReason.KnockedOff);
            var renderer = block.GetComponent<SpriteRenderer>();
            _impactFeedback?.PlayKnockOff(block.transform.position, renderer != null ? renderer.color : Palette.Accent, _loop.Combo);
        }

        void OnScoreAwarded(ScoreAward award)
        {
            // A barrel can destroy eight pieces in the same frame. Showing eight labels at one
            // coordinate becomes an unreadable white blob, so acknowledge the opening events and
            // only the meaningful chain milestones after that.
            if (award.Reason == ScoreReason.AmmoBonus ||
                (award.Chain > 2 && award.Chain != 4 && award.Chain != 7))
            {
                return;
            }

            ScorePopup.Play(_effectRoot, _lastScorePosition, award);
        }

        void ApplyShockwave(Vector2 centre, float radius, float energy, DestructibleBlock source)
        {
            var hits = Physics2D.OverlapCircleAll(centre, radius);
            foreach (var hit in hits)
            {
                var block = hit.GetComponent<DestructibleBlock>();
                if (block == null || block == source || block.IsCleared)
                {
                    continue;
                }

                var delta = (Vector2)block.transform.position - centre;
                var falloff = 1f - Mathf.Clamp01(delta.magnitude / radius);
                if (falloff <= 0f)
                {
                    continue;
                }

                var direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector2.up;
                var body = block.GetComponent<Rigidbody2D>();
                body?.AddForce(direction * energy * falloff * 0.18f, ForceMode2D.Impulse);
                block.ApplyImpact(new ImpactEvent(block.transform.position, -direction, direction, energy * falloff * 0.7f));
            }
        }

        void Explode(Vector2 centre, DestructibleBlock source)
        {
            const float radius = 1.75f;
            const float energy = 125f;
            var hits = Physics2D.OverlapCircleAll(centre, radius);
            foreach (var hit in hits)
            {
                var block = hit.GetComponent<DestructibleBlock>();
                if (block == null || block == source || block.IsCleared)
                {
                    continue;
                }

                var delta = (Vector2)block.transform.position - centre;
                var falloff = 1f - Mathf.Clamp01(delta.magnitude / radius);
                var direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector2.up;
                block.GetComponent<Rigidbody2D>()?.AddForce(direction * (6f + 8f * falloff), ForceMode2D.Impulse);
                block.ApplyImpact(new ImpactEvent(block.transform.position, -direction, direction * 10f, energy * Mathf.Lerp(0.35f, 1f, falloff), true));
            }
        }

        void OnStateChanged(GameState state)
        {
            _cannon.CanFire = state == GameState.Playing && !_shotInFlight;
            if (state == GameState.Failed)
            {
                Sfx.Instance?.Failed();
                Haptics.Light();
                ShowResult(_failScoreText, _failBestText);
                _failPanel.SetActive(true);
                _ftueHint?.Hide();
            }
            else if (state == GameState.Cleared)
            {
                if (runMode == RunMode.Stage)
                {
                    Progress.UnlockAfterClear(_stageIndex, LevelLibrary.Count);
                }

                Sfx.Instance?.Cleared();
                Haptics.Light();
                ShowResult(_clearScoreText, _clearBestText);
                RefreshClearActions();
                _clearPanel.SetActive(true);
                _ftueHint?.Hide();
            }
        }

        /// <summary>Fills a result card's score line and commits the run to the best-score store.</summary>
        void ShowResult(Text scoreText, Text bestText)
        {
            var score = _loop.Score;
            scoreText.text = _loop.TargetScore > 0
                ? Loc.Format("result.scoreGoal", score, _loop.TargetScore)
                : Loc.Format("result.score", score);

            if (runMode == RunMode.Stage)
            {
                var unlocked = Mathf.Clamp(Progress.StageUnlocked, 1, LevelLibrary.Count);
                bestText.text = Loc.Format("menu.stage.progress", unlocked, LevelLibrary.Count);
                bestText.color = Palette.AccentCool;
                return;
            }

            var (best, isNewRecord) = CommitScore(score);
            if (isNewRecord)
            {
                bestText.text = Loc.Get("result.newBest");
                bestText.color = Palette.Accent;
            }
            else
            {
                bestText.text = Loc.Format("result.best", best);
                bestText.color = Palette.TextMuted;
            }
        }

        /// <summary>
        /// Persists the finished run to the right store (per-seed for Daily, all-time for Endless)
        /// and reports the resulting record plus whether this run set it.
        /// </summary>
        (int best, bool isNewRecord) CommitScore(int score)
        {
            if (runMode == RunMode.Daily)
            {
                var previous = _daily.LoadBestScore(_seed);
                _daily.SaveBestScore(_seed, score);
                return (Mathf.Max(previous, score), score > previous);
            }

            if (runMode == RunMode.Stage)
            {
                // Stage runs keep their own score display but do not overwrite endless best.
                return (score, false);
            }

            var previousBest = Progress.EndlessBest;
            var isNewRecord = Progress.SubmitEndless(score);
            return (Mathf.Max(previousBest, score), isNewRecord);
        }

        void OnLanguageChanged()
        {
            RefreshLocalizedUi();
        }

        void RefreshLocalizedUi()
        {
            UiFactory.RebindFonts(_canvasRoot);

            if (_scoreCaption != null) _scoreCaption.text = Loc.Get("hud.scoreCaption");
            if (_ammoCaption != null) _ammoCaption.text = Loc.Get("hud.ammoCaption");
            if (_hudHomeLabel != null) _hudHomeLabel.text = Loc.Get("common.menu");
            if (_failTitle != null) _failTitle.text = Loc.Get("fail.title");
            if (_clearTitle != null) _clearTitle.text = Loc.Get("clear.title");
            if (_failReviveLabel != null) _failReviveLabel.text = Loc.Get("fail.revive");
            if (_failRetryLabel != null) _failRetryLabel.text = Loc.Get("fail.retry");
            if (_failMenuLabel != null) _failMenuLabel.text = Loc.Get("common.menu");
            if (_clearMenuLabel != null) _clearMenuLabel.text = Loc.Get("common.menu");
            if (_menuTitle != null) _menuTitle.text = Loc.Get("menu.title");
            if (_menuTagline != null) _menuTagline.text = Loc.Get("menu.tagline");
            if (_menuStagesLabel != null) _menuStagesLabel.text = Loc.Get("menu.stages");
            if (_menuEndlessLabel != null) _menuEndlessLabel.text = Loc.Get("menu.endless");
            if (_menuDailyLabel != null) _menuDailyLabel.text = Loc.Get("menu.daily");
            if (_sfxToggleLabel != null) _sfxToggleLabel.text = SettingsCaption(true);
            if (_hapticsToggleLabel != null) _hapticsToggleLabel.text = SettingsCaption(false);
            if (_langToggleLabel != null) _langToggleLabel.text = Loc.LanguageToggleCaption();
            if (_splashTitle != null) _splashTitle.text = Loc.Get("menu.title");
            if (_splashTagline != null) _splashTagline.text = Loc.Get("menu.tagline");
            if (_splashHint != null) _splashHint.text = Loc.Get("splash.tap");
            if (_stageSelectTitle != null) _stageSelectTitle.text = Loc.Get("stage.title");
            if (_stageBackLabel != null) _stageBackLabel.text = Loc.Get("common.back");
            if (_homeSettingsLabel != null) _homeSettingsLabel.text = Loc.Get("menu.settings");
            if (_settingsTitle != null) _settingsTitle.text = Loc.Get("settings.title");
            if (_settingsCloseLabel != null) _settingsCloseLabel.text = Loc.Get("settings.close");
            _ftueHint?.SetCopy(Loc.Get("ftue.aim"));

            RefreshHomeStats();
            RefreshStageSelectLabels();
            RefreshClearActions();
            RefreshHud();
        }

        void ShowSplash()
        {
            SetScreen(AppScreen.Splash);
            _splashFinished = false;
            if (_splashFill != null)
            {
                _splashFill.rectTransform.anchorMax = new Vector2(0.08f, 1f);
            }

            if (_splashRoutine != null)
            {
                StopCoroutine(_splashRoutine);
            }

            _splashRoutine = StartCoroutine(SplashRoutine());
        }

        IEnumerator SplashRoutine()
        {
            var elapsed = 0f;
            while (elapsed < SplashDuration && !_splashFinished)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / SplashDuration);
                if (_splashFill != null)
                {
                    _splashFill.rectTransform.anchorMax = new Vector2(Mathf.Lerp(0.08f, 1f, t), 1f);
                }

                yield return null;
            }

            FinishSplash();
        }

        void FinishSplash()
        {
            if (_splashFinished)
            {
                return;
            }

            _splashFinished = true;
            if (_splashRoutine != null)
            {
                StopCoroutine(_splashRoutine);
                _splashRoutine = null;
            }

            ShowHome();
        }

        void ShowHome()
        {
            SetScreen(AppScreen.Home);
            _cannon.CanFire = false;
            _shotInFlight = false;
            if (_failPanel != null) _failPanel.SetActive(false);
            if (_clearPanel != null) _clearPanel.SetActive(false);
            _ftueHint?.Hide();
            ClearBoard();
            RefreshHomeStats();
        }

        void ShowStageSelect()
        {
            SetScreen(AppScreen.StageSelect);
            _cannon.CanFire = false;
            _shotInFlight = false;
            if (_failPanel != null) _failPanel.SetActive(false);
            if (_clearPanel != null) _clearPanel.SetActive(false);
            _ftueHint?.Hide();
            ClearBoard();
            RefreshStageSelectLabels();
        }

        void ShowSettings()
        {
            Sfx.Instance?.Ui();
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(true);
            }
        }

        void HideSettings()
        {
            Sfx.Instance?.Ui();
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }
        }

        void SetScreen(AppScreen screen)
        {
            _screen = screen;
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }

            if (_splashPanel != null) _splashPanel.SetActive(screen == AppScreen.Splash);
            if (_menuPanel != null) _menuPanel.SetActive(screen == AppScreen.Home);
            if (_stageSelectPanel != null) _stageSelectPanel.SetActive(screen == AppScreen.StageSelect);
            SetHudVisible(screen == AppScreen.Play);
        }

        void SetHudVisible(bool visible)
        {
            if (_hudRoot != null)
            {
                _hudRoot.SetActive(visible);
            }
        }

        void RefreshHomeStats()
        {
            var unlocked = Mathf.Clamp(Progress.StageUnlocked, 1, LevelLibrary.Count);
            if (_menuStageProgressText != null)
            {
                _menuStageProgressText.text = Loc.Format("menu.stage.progress", unlocked, LevelLibrary.Count);
            }

            if (_homeProgressFill != null)
            {
                // Cleared stages, not unlocked ones: the newest unlocked stage is still ahead.
                var cleared = Mathf.Clamp01((unlocked - 1) / (float)LevelLibrary.Count);
                _homeProgressFill.rectTransform.anchorMax = new Vector2(cleared, 1f);
            }

            if (_menuBestText != null)
            {
                _menuBestText.text = Loc.Format("menu.best", Progress.EndlessBest);
            }

            if (_menuDailyBestText != null)
            {
                _menuDailyBestText.text = Loc.Format("menu.daily.best", _daily.LoadBestScore(_daily.TodaySeed));
            }
        }

        void RefreshStageSelectLabels()
        {
            for (var i = 0; i < _stageButtons.Count; i++)
            {
                var unlocked = Progress.IsStageUnlocked(i);
                var layout = LevelLibrary.Get(i);
                var label = unlocked
                    ? Loc.Format("stage.item", i + 1, Loc.LevelName(layout.Name))
                    : Loc.Format("stage.item", i + 1, Loc.Get("stage.locked"));

                if (_stageLabels[i] != null)
                {
                    _stageLabels[i].text = label;
                    _stageLabels[i].color = unlocked ? Palette.TextPrimary : Palette.TextMuted;
                }

                _stageButtons[i].interactable = unlocked;
                var image = _stageButtons[i].targetGraphic as Image;
                if (image != null)
                {
                    image.color = unlocked ? Palette.HudFill : new Color(Palette.HudFill.r, Palette.HudFill.g, Palette.HudFill.b, 0.45f);
                }
            }
        }

        void OnStageChosen(int index)
        {
            if (!Progress.IsStageUnlocked(index))
            {
                return;
            }

            Sfx.Instance?.Ui();
            BeginRun(RunMode.Stage, index);
        }

        void RetryCurrentRun()
        {
            BeginRun(runMode, _stageIndex);
        }

        void OnClearNextClicked()
        {
            ShowInterstitialThen(() =>
            {
                if (runMode == RunMode.Stage)
                {
                    if (_stageIndex + 1 < LevelLibrary.Count)
                    {
                        BeginRun(RunMode.Stage, _stageIndex + 1);
                    }
                    else
                    {
                        ShowHome();
                    }

                    return;
                }

                if (runMode == RunMode.Daily)
                {
                    ShowHome();
                    return;
                }

                BeginRun(RunMode.Endless);
            });
        }

        void RefreshClearActions()
        {
            if (_clearNextLabel == null)
            {
                return;
            }

            if (runMode == RunMode.Stage)
            {
                var isLast = _stageIndex + 1 >= LevelLibrary.Count;
                _clearNextLabel.text = isLast ? Loc.Get("clear.home") : Loc.Get("clear.next");
                if (_clearTitle != null)
                {
                    _clearTitle.text = isLast && _loop.State == GameState.Cleared
                        ? Loc.Get("clear.all")
                        : Loc.Get("clear.title");
                }

                return;
            }

            if (runMode == RunMode.Daily)
            {
                _clearNextLabel.text = Loc.Get("clear.home");
                if (_clearTitle != null) _clearTitle.text = Loc.Get("clear.title");
                return;
            }

            _clearNextLabel.text = Loc.Get("menu.endless");
            if (_clearTitle != null) _clearTitle.text = Loc.Get("clear.title");
        }

        void RefreshFtue()
        {
            if (_ftueHint == null)
            {
                return;
            }

            if (Progress.FtueDone)
            {
                _ftueHint.Hide();
            }
            else
            {
                _ftueHint.Show();
            }
        }

        void CompleteFtue()
        {
            if (Progress.FtueDone)
            {
                return;
            }

            Progress.FtueDone = true;
            _ftueHint?.Hide();
        }

        /// <summary>
        /// Skips interstitials for the first few runs so the FTUE loop is not interrupted by ads.
        /// </summary>
        void ShowInterstitialThen(System.Action next)
        {
            if (Progress.ShouldDelayInterstitial)
            {
                next?.Invoke();
                return;
            }

            _ads.ShowInterstitial(next);
        }

        void OnReviveClicked()
        {
            _ads.ShowRewarded(success =>
            {
                if (!success)
                {
                    return;
                }

                if (_loop.TryRevive(reviveAmmo))
                {
                    _failPanel.SetActive(false);
                    _cannon.ArmAfterPointerRelease();
                    RefreshHud();
                }
            });
        }

        void RefreshHud()
        {
            if (_scoreText != null)
            {
                _scoreText.text = _loop.TargetScore > 0
                    ? Loc.Format("hud.scoreGoal", _loop.Score, _loop.TargetScore)
                    : _loop.Score.ToString();
            }

            if (_ammoText != null)
            {
                _ammoText.text = _loop.Ammo.ToString();
            }

            if (_goalFill != null)
            {
                var progress = _loop.TargetScore > 0
                    ? Mathf.Clamp01(_loop.Score / (float)_loop.TargetScore)
                    : 0f;
                _goalFill.rectTransform.anchorMax = new Vector2(progress, 1f);
                _goalFill.color = _loop.GoalMet ? Palette.AccentCool : Palette.Accent;
            }

            if (_targetsText != null)
            {
                if (_layout == null)
                {
                    _targetsText.text = string.Empty;
                }
                else if (runMode == RunMode.Stage)
                {
                    _targetsText.text = Loc.Format(
                        "hud.stage.targets",
                        _stageIndex + 1,
                        Loc.LevelName(_layout.Name),
                        _loop.TargetsRemaining);
                }
                else
                {
                    _targetsText.text = Loc.Format("hud.targets", Loc.LevelName(_layout.Name), _loop.TargetsRemaining);
                }
            }
        }

        GameObject CreateSolid(string name, Vector3 position, Vector2 size, Color color)
        {
            var renderer = CreateSlicedRenderer(name, position, size, color, SortingOrders.Structure);
            renderer.transform.SetParent(_worldRoot);
            var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.sharedMaterial = PieceMaterial;
            _terrainColliders.Add(collider);
            return renderer.gameObject;
        }

        /// <summary>Thin decorative strip; too slim for a 9-sliced sprite's corners.</summary>
        void CreateTrim(string name, Vector3 position, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_worldRoot);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Shapes.Solid;
            renderer.color = new Color(color.r, color.g, color.b, 0.7f);
            renderer.sortingOrder = SortingOrders.Structure + 1;
        }

        static SpriteRenderer CreateSlicedRenderer(string name, Vector3 position, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Shapes.BlockRect;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        static SpriteRenderer CreateCircleRenderer(string name, Vector3 position, float radius, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (radius * 2f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Shapes.Circle;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        static PhysicsMaterial2D _pieceMaterial;
        static PhysicsMaterial2D _projectileMaterial;

        /// <summary>
        /// Grippy and dead. Unity's default (friction 0.4, some restitution) lets stacked pieces
        /// creep and bounce against each other while the structure is meant to be at rest.
        /// </summary>
        static PhysicsMaterial2D PieceMaterial
        {
            get
            {
                if (_pieceMaterial == null)
                {
                    _pieceMaterial = new PhysicsMaterial2D("Piece") { friction = 0.7f, bounciness = 0f };
                }

                return _pieceMaterial;
            }
        }

        static PhysicsMaterial2D ProjectileMaterial
        {
            get
            {
                if (_projectileMaterial == null)
                {
                    _projectileMaterial = new PhysicsMaterial2D("Projectile")
                    {
                        friction = 0.12f,
                        bounciness = 0.08f,
                    };
                }

                return _projectileMaterial;
            }
        }

        static readonly Color ShadowTint = new Color(0f, 0f, 0f, 0.3f);

        static void AddBoxShadow(Transform target, Vector2 size)
        {
            var renderer = CreateSlicedRenderer("Shadow", Vector3.zero, size, ShadowTint, SortingOrders.Shadow);
            renderer.transform.SetParent(target, false);
            renderer.transform.localPosition = new Vector3(0.07f, -0.09f, 0f);
        }

        /// <summary>Sizes itself from the parent's scale, which already encodes the circle's diameter.</summary>
        static void AddCircleShadow(Transform target)
        {
            var renderer = CreateCircleRenderer("Shadow", Vector3.zero, 0.5f, ShadowTint, SortingOrders.Shadow);
            renderer.transform.SetParent(target, false);
            renderer.transform.localScale = Vector3.one;
            renderer.transform.localPosition = new Vector3(0.1f, -0.13f, 0f);
        }
    }

    /// <summary>
    /// Reports measured projectile contacts. Damage, scoring and feedback consume the same event,
    /// so a glancing touch cannot look or score like a full-power centre hit.
    /// </summary>
    public sealed class ProjectileHitWatcher : MonoBehaviour
    {
        readonly HashSet<DestructibleBlock> _hitBlocks = new HashSet<DestructibleBlock>();
        public event System.Action<DestructibleBlock, ImpactEvent> Hit;
        public event System.Action<ImpactEvent> SurfaceHit;

        void OnCollisionEnter2D(Collision2D collision)
        {
            var block = collision.collider.GetComponent<DestructibleBlock>();
            var contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
            var velocity = collision.relativeVelocity;
            var body = GetComponent<Rigidbody2D>();
            var mass = body != null ? body.mass : 1f;
            var energy = 0.5f * mass * velocity.sqrMagnitude;
            var impact = new ImpactEvent(contact.point, contact.normal, velocity, energy);

            if (block == null)
            {
                SurfaceHit?.Invoke(impact);
                return;
            }

            if (block.IsCleared || !_hitBlocks.Add(block))
            {
                return;
            }

            Hit?.Invoke(block, impact);
        }
    }
}
