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
        // The margin here is down to what the cannon pad and the widest structure actually need.
        const float FieldHalfWidth = 3.7f;

        /// <summary>
        /// Floor for the camera size. Phones never reach it (their portrait aspect always asks
        /// for more), it only keeps a wide editor Game view from zooming into the field.
        /// </summary>
        const float MinOrthoSize = 6.2f;

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
        const float HudRowBottom = 0.888f;
        const float HudRowTop = 0.970f;

        /// <summary>Stage caption strip, tucked directly under the chips.</summary>
        const float HudCaptionBottom = 0.854f;
        const float HudCaptionTop = 0.884f;

        /// <summary>
        /// Bottom of the pit targets fall into. Must stay below the tallest phone viewport
        /// (~-8.8) or blocks would blink out while still on screen.
        /// </summary>
        const float ClearY = -9.5f;

        /// <summary>
        /// Low enough that a 5-row stack still clears the HUD on a 16:9 phone, which is the
        /// squarest portrait screen and therefore the shortest viewport we render into, and high
        /// enough that the pad and the pit below it both stay in frame.
        /// </summary>
        const float ShelfTopY = -1.2f;
        // Leaves a readable launch corridor between the muzzle and the pad's left face.
        // At 1.0 the muzzle sat only ~0.26 world units from that face, so normal 30–45° shots
        // struck the pad edge before they could rise into the structure.
        const float ShelfCenterX = 1.1f;
        const float BlockSize = 0.9f;

        /// <summary>
        /// Grid cell size. Must equal <see cref="BlockSize"/>: any slack makes every course drop
        /// onto the one below at spawn, and those stacked impacts shake structures apart.
        /// </summary>
        const float BlockPitch = BlockSize;

        /// <summary>
        /// Narrow enough to read as a support, wide enough that a stacked pair carrying a beam
        /// is not a knife edge.
        /// </summary>
        const float PillarWidth = 0.56f;

        /// <summary>Radius of a loose ball piece, kept in proportion with the boxes around it.</summary>
        const float BallRadius = 0.38f;

        /// <summary>
        /// Muzzle height is what matters here, not the pivot: at a typical 45° the barrel tip sits
        /// above <see cref="ShelfTopY"/>, so ordinary shots pass over the pad and into the side of
        /// the stack. Dropping the cannon further would force near-vertical arcs that strike the
        /// underside of the pad instead.
        /// </summary>
        static readonly Vector3 CannonPivot = new Vector3(-2.6f, -1.25f, 0f);

        /// <summary>Plinth surface the cannon stands on.</summary>
        const float PlinthTopY = -1.9f;

        /// <summary>
        /// Both pads are short slabs over an open pit. They used to run off the bottom of the
        /// frame, which filled the tall portrait viewport with geometry no shot ever touches and
        /// squeezed the action into a thin band across the middle.
        /// </summary>
        const float PlinthWidth = 1.8f;
        const float PlinthHeight = 1.7f;
        const float ShelfThickness = 0.5f;

        /// <summary>Slab overhang past the outermost course, and the floor for a narrow stack.</summary>
        const float ShelfMargin = 0.4f;
        const float ShelfMinWidth = 2.6f;

        /// <summary>Stub carrying the target pad; short, so the pad reads as an island.</summary>
        const float ShelfStubWidth = 1.3f;
        const float ShelfStubBottomY = -3.4f;

        /// <summary>Empty rating slot: ink wash, visible against the paper without reading as earned.</summary>
        static readonly Color UnearnedStarTint = new Color(0.55f, 0.48f, 0.38f, 0.45f);

        /// <summary>Widest loadout the ammo queue can show; longer stages would crowd the chip.</summary>
        const int MaxLoadoutPips = 6;

        [SerializeField] int reviveAmmo = 2;
        [SerializeField] RunMode runMode = RunMode.Stage;

        readonly GameLoop _loop = new GameLoop();
        readonly DailyChallenge _daily = new DailyChallenge();
        readonly List<DestructibleBlock> _blocks = new List<DestructibleBlock>();
        readonly List<Collider2D> _terrainColliders = new List<Collider2D>();
        readonly List<Button> _stageButtons = new List<Button>();
        readonly List<Text> _stageLabels = new List<Text>();
        readonly List<Image[]> _stageStarIcons = new List<Image[]>();

        IAdService _ads;
        CannonController _cannon;
        ComboPresenter _combo;
        ImpactFeedback _impactFeedback;
        ViewportFitter _viewportFitter;
        Transform _worldRoot;
        Transform _projectileRoot;
        Transform _effectRoot;
        GameObject _shelf;
        GameObject _shelfStub;
        SpriteRenderer _shelfTrim;
        float _shelfHalfWidth = ShelfMinWidth * 0.5f;
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
        Text _failGapText;
        Text _clearHintText;
        Text _failTitle;
        Text _clearTitle;
        Text _failReviveLabel;
        Text _failRetryLabel;
        Text _failMenuLabel;
        Text _clearNextLabel;
        Text _clearReplayLabel;
        Text _clearMenuLabel;
        GameObject _clearMenuButton;
        GameObject _clearReplayButton;
        Text _pauseTitle;
        Text _pauseResumeLabel;
        Text _pauseRetryLabel;
        Text _pauseHomeLabel;
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
        Image[] _clearStarIcons;
        Image[] _ammoPips;
        Text _shotHint;
        GameObject _clearStarRow;
        Image _homeStarIcon;
        Text _homeStarText;
        GameObject _hudRoot;
        GameObject _failPanel;
        GameObject _clearPanel;
        GameObject _pausePanel;
        GameObject _briefingPanel;
        GameObject _menuPanel;
        GameObject _splashPanel;
        GameObject _stageSelectPanel;
        GameObject _settingsPanel;
        Transform _canvasRoot;
        FtueHint _ftueHint;
        LevelLayout _layout;
        AppScreen _screen = AppScreen.Splash;
        bool _shotInFlight;
        bool _paused;
        int _liveProjectiles;
        float _lastDestructionTime;
        Vector3 _lastScorePosition;
        int _seed;
        int _stageIndex;
        bool _splashFinished;
        Coroutine _splashRoutine;

        /// <summary>Shots for the active run, possibly reordered on the briefing or mid-run.</summary>
        string _runLoadout = "B";
        int _targetTotal;

        int _briefingStage;
        string _briefingLoadout = "B";
        int _briefingPick = -1;
        Text _briefingTitle;
        Text _briefingHint;
        Text _briefingGoal;
        Text _briefingLoadoutCaption;
        Text _briefingStartLabel;
        Text _briefingBackLabel;
        Image[] _briefingPips;
        Button[] _briefingPipButtons;
        Transform _briefingSilhouetteRoot;
        readonly List<Transform> _stageSilhouetteRoots = new List<Transform>();
        readonly List<Image[]> _stageLoadoutPips = new List<Image[]>();
        Text _swapHint;

        public GameLoop Loop => _loop;

        /// <summary>True while the in-run pause sheet is up; hit-stop restores to this instead of 1.</summary>
        public bool IsPaused => _paused;

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
            if (_screen == AppScreen.Splash && !_splashFinished)
            {
                if (UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.touchCount > 0)
                {
                    FinishSplash();
                }

                return;
            }

            // The cannon ignores input while a shot is out, which leaves the tap free to mean
            // "go off now" for the shots that carry a special.
            if (_screen == AppScreen.Play && !_paused && _shotInFlight &&
                UnityEngine.Input.GetMouseButtonDown(0) && !PointerOverUi())
            {
                TriggerLiveSpecials();
            }
        }

        static bool PointerOverUi()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null)
            {
                return false;
            }

            if (UnityEngine.Input.touchCount > 0)
            {
                return events.IsPointerOverGameObject(UnityEngine.Input.GetTouch(0).fingerId);
            }

            return events.IsPointerOverGameObject();
        }

        void OnDestroy()
        {
            Loc.Changed -= OnLanguageChanged;
            if (_paused)
            {
                Time.timeScale = 1f;
                _paused = false;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void BeginRun(RunMode mode, int stageIndex = 0, string loadoutOverride = null)
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
            _runLoadout = string.IsNullOrEmpty(loadoutOverride) ? _layout.Loadout : loadoutOverride;
            _targetTotal = _blocks.Count;
            _loop.StartRun(mode, _runLoadout.Length, _blocks.Count, _layout.TargetScore);
            _cannon.ArmAfterPointerRelease();
            _shotInFlight = false;
            _liveProjectiles = 0;
            HideShotHint();
            SetPaused(false);
            if (_failPanel != null) _failPanel.SetActive(false);
            if (_clearPanel != null) _clearPanel.SetActive(false);
            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_briefingPanel != null) _briefingPanel.SetActive(false);
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

            var backdrop = BuildBackdrop(cam, out var grain);
            _viewportFitter = cam.gameObject.AddComponent<ViewportFitter>();
            _viewportFitter.Configure(FieldHalfWidth, MinOrthoSize, backdrop, grain);

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

        /// <summary>
        /// Returns the gradient transform so the viewport fitter can keep it full-bleed, and hands
        /// back the paper grain separately: it is tiled, so the fitter has to drive its draw size
        /// instead of its scale.
        /// </summary>
        Transform BuildBackdrop(Camera cam, out SpriteRenderer grain)
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

            // Washi grain over the flat gradient. Kept faint: it should register as texture when
            // the eye rests on an empty area, not as noise competing with the pieces.
            var grainGo = new GameObject("PaperGrain");
            grainGo.transform.SetParent(root, false);
            grain = grainGo.AddComponent<SpriteRenderer>();
            grain.sprite = Shapes.PaperFiber;
            grain.drawMode = SpriteDrawMode.Tiled;
            grain.tileMode = SpriteTileMode.Continuous;
            grain.size = new Vector2(viewWidth, viewHeight);
            grain.color = new Color(Palette.Shadow.r, Palette.Shadow.g, Palette.Shadow.b, 0.14f);
            grain.sortingOrder = SortingOrders.BackdropGrain;

            // Warm pool of light behind the target stack draws the eye to the objective.
            var glow = new GameObject("Glow");
            glow.transform.SetParent(root, false);
            glow.transform.position = new Vector3(ShelfCenterX, ShelfTopY + 1.6f, 0f);
            glow.transform.localScale = Vector3.one * 8.5f;
            var glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = Shapes.Glow;
            glowRenderer.color = new Color(Palette.BackdropGlow.r, Palette.BackdropGlow.g, Palette.BackdropGlow.b, 0.24f);
            glowRenderer.sortingOrder = SortingOrders.BackdropGlow;

            BuildSiteScenery(root);

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

                // Drifting petals. These used to be white specks, which read as dust against a
                // dark site but disappear completely on paper; the tint has to be darker than the
                // backdrop now, not lighter.
                var moteRenderer = mote.AddComponent<SpriteRenderer>();
                moteRenderer.sprite = Shapes.Circle;
                moteRenderer.color = new Color(
                    Palette.Accent.r, Palette.Accent.g, Palette.Accent.b,
                    Mathf.Lerp(0.10f, 0.22f, (float)rng.NextDouble()));
                moteRenderer.sortingOrder = SortingOrders.BackdropDecor;

                mote.AddComponent<FloatingMote>().Configure(
                    Mathf.Lerp(0.1f, 0.4f, (float)rng.NextDouble()),
                    Mathf.Lerp(0.2f, 0.6f, (float)rng.NextDouble()));
            }

            return gradient.transform;
        }

        /// <summary>
        /// Scenery for the frame above and below the pads. The supports used to run off the bottom
        /// of the screen to cover that space; skyline and rigging cover it without putting inert
        /// geometry inside the play area, and they give the pads a depth to sit in front of.
        /// </summary>
        void BuildSiteScenery(Transform root)
        {
            var rng = new System.Random(19);
            var halfWidth = FieldHalfWidth + 0.4f;

            BuildSkylineBand(root, rng, halfWidth, -3.9f, -5f, 0.9f, Palette.SkylineFar, SortingOrders.SkylineFar);
            BuildSkylineBand(root, rng, halfWidth, -4.8f, -5.9f, 1.25f, Palette.SkylineNear, SortingOrders.SkylineNear);

            // Tower crane over the site. The mast stands clear of the cannon pad so it reads
            // against the sky rather than merging into the platform silhouette.
            const float mastX = -3.15f;
            CreateBackdropShape(root, "CraneMast", new Vector3(mastX, 1.5f, 0f), new Vector2(0.16f, 5.2f), Palette.Rigging, SortingOrders.Rigging);
            CreateBackdropShape(root, "CraneJib", new Vector3(mastX + 1.1f, 3.9f, 0f), new Vector2(2.9f, 0.14f), Palette.Rigging, SortingOrders.Rigging);
            CreateBackdropShape(root, "CraneHoist", new Vector3(mastX + 2.1f, 3.35f, 0f), new Vector2(0.05f, 1f), Palette.Rigging, SortingOrders.Rigging);
            CreateBackdropShape(root, "CraneHook", new Vector3(mastX + 2.1f, 2.8f, 0f), new Vector2(0.3f, 0.16f), Palette.Rigging, SortingOrders.Rigging);

            CreateBackdropGlow(root, "PadLight", new Vector3(CannonPivot.x + 0.4f, PlinthTopY + 0.2f, 0f), 2.6f, 0.16f);
            CreateBackdropGlow(root, "PitLight", new Vector3(ShelfCenterX - 0.6f, -3.9f, 0f), 4.2f, 0.09f);
        }

        static void BuildSkylineBand(Transform root, System.Random rng, float halfWidth, float topNear, float topFar, float span, Color color, int sortingOrder)
        {
            // Far below the tallest viewport, so no phone ever sees a skyline block end.
            const float baseY = -9.4f;

            var x = -halfWidth;
            var index = 0;
            while (x < halfWidth)
            {
                var width = Mathf.Min(span * Mathf.Lerp(0.7f, 1.5f, (float)rng.NextDouble()), halfWidth - x);
                if (width < 0.15f)
                {
                    break;
                }

                var top = Mathf.Lerp(topNear, topFar, (float)rng.NextDouble());
                var height = top - baseY;
                CreateBackdropShape(root, "Skyline" + index++, new Vector3(x + width * 0.5f, top - height * 0.5f, 0f), new Vector2(width, height), color, sortingOrder);
                x += width;
            }
        }

        static void CreateBackdropShape(Transform parent, string name, Vector3 position, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Shapes.Solid;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        static void CreateBackdropGlow(Transform parent, string name, Vector3 position, float scale, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Shapes.Glow;
            renderer.color = new Color(Palette.BackdropGlow.r, Palette.BackdropGlow.g, Palette.BackdropGlow.b, alpha);
            renderer.sortingOrder = SortingOrders.BackdropGlow;
        }

        void BuildTerrain()
        {
            CreateSolid("Plinth", new Vector3(CannonPivot.x, PlinthTopY - PlinthHeight * 0.5f, 0f), new Vector2(PlinthWidth, PlinthHeight), Palette.Ground);
            CreateHazardTrim("PlinthTrim", new Vector3(CannonPivot.x, PlinthTopY - 0.09f, 0f), new Vector2(PlinthWidth, 0.18f));

            // The target pad is an island so knocked-off pieces fall clear on either side. Its
            // width is set per level by LayoutShelf; a three column stack marooned on a five column
            // slab was most of why the structure read as small.
            _shelfStub = CreateSolid("ShelfStub", new Vector3(ShelfCenterX, 0f, 0f), new Vector2(ShelfStubWidth, 1f), Palette.PadStub);
            _shelf = CreateSolid("Shelf", new Vector3(ShelfCenterX, 0f, 0f), new Vector2(ShelfMinWidth, ShelfThickness), Palette.Shelf);
            _shelfTrim = CreateHazardTrim("ShelfTrim", new Vector3(ShelfCenterX, ShelfTopY - 0.09f, 0f), new Vector2(ShelfMinWidth, 0.18f));

            LayoutShelf(ShelfMinWidth);
        }

        /// <summary>
        /// Resizes the target pad to the stack standing on it. The half width is cached because the
        /// knock-off bounds handed to every piece key off the pad edges.
        /// </summary>
        void LayoutShelf(float width)
        {
            _shelfHalfWidth = width * 0.5f;

            ResizeSolid(_shelf, new Vector3(ShelfCenterX, ShelfTopY - ShelfThickness * 0.5f, 0f), new Vector2(width, ShelfThickness));

            if (_shelfTrim != null)
            {
                _shelfTrim.size = new Vector2(width, 0.18f);
            }

            var stubTop = ShelfTopY - ShelfThickness;
            var stubHeight = stubTop - ShelfStubBottomY;
            ResizeSolid(_shelfStub, new Vector3(ShelfCenterX, stubTop - stubHeight * 0.5f, 0f), new Vector2(ShelfStubWidth, stubHeight));
        }

        static void ResizeSolid(GameObject solid, Vector3 position, Vector2 size)
        {
            if (solid == null)
            {
                return;
            }

            solid.transform.position = position;

            var renderer = solid.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.size = size;
            }

            var collider = solid.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                collider.size = size;
            }
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

            // Pause sits where "home" used to: quitting mid-run without a confirm was too easy,
            // and every demolition game in the genre puts restart behind a pause sheet.
            _hudHomeLabel = UiFactory.Pill(_hudRoot.transform, Loc.Get("hud.pause"), new Vector2(HudLeft, HudRowBottom), new Vector2(0.20f, HudRowTop), Palette.HudPanel, Palette.TextPrimary, 26, ShowPause).GetComponentInChildren<Text>();

            _scoreText = CreateHudChip(_hudRoot.transform, "ScoreChip", new Vector2(0.225f, HudRowBottom), new Vector2(0.715f, HudRowTop), Loc.Get("hud.scoreCaption"), TextAnchor.MiddleLeft, out _scoreCaption, out var scoreChip);

            // The goal bar belongs to the score, so it lives inside that chip. As a full width
            // strip of its own it was the loudest thing on screen while carrying the least meaning.
            // A groove darker than the chip, not a lighter strip: an empty track tinted brighter
            // than its panel reads as a bar that is already full.
            var goalTrack = UiFactory.Panel(scoreChip.transform, "GoalTrack", new Vector2(0.06f, 0.11f), new Vector2(0.94f, 0.21f), new Color(0f, 0f, 0f, 0.32f), 3f);
            _goalFill = UiFactory.Panel(goalTrack.transform, "GoalFill", Vector2.zero, new Vector2(0f, 1f), Palette.Accent, 3f);

            _ammoText = CreateHudChip(_hudRoot.transform, "AmmoChip", new Vector2(0.74f, HudRowBottom), new Vector2(HudRight, HudRowTop), Loc.Get("hud.ammoCaption"), TextAnchor.MiddleCenter, out _ammoCaption, out var ammoChip);

            // The loadout is visible before it matters: the player picks an aim knowing the next
            // round is a charge. Tapping the chip swaps the next two unfired shots.
            _ammoPips = CreateAmmoPips(ammoChip.transform, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.22f));
            var ammoButton = ammoChip.gameObject.AddComponent<Button>();
            ammoButton.targetGraphic = ammoChip;
            ammoButton.onClick.AddListener(SwapNextTwoShots);

            _targetsText = UiFactory.Label(_hudRoot.transform, "Targets", new Vector2(HudLeft, HudCaptionBottom), new Vector2(0.62f, HudCaptionTop), 24, TextAnchor.MiddleLeft, Palette.TextMuted);
            _swapHint = UiFactory.Label(_hudRoot.transform, "SwapHint", new Vector2(0.62f, HudCaptionBottom), new Vector2(HudRight, HudCaptionTop), 20, TextAnchor.MiddleRight, Palette.TextMuted);

            // Over the pit rather than the cannon pad: the hint fires while a shot is in the air,
            // which is exactly when the player is watching that part of the screen.
            _shotHint = UiFactory.Label(_hudRoot.transform, "ShotHint", new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.2f), 32, TextAnchor.MiddleCenter, Palette.AccentCool, FontStyle.Bold);
            UiFactory.AddDropShadow(_shotHint, 3f);
            _shotHint.gameObject.SetActive(false);

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
                    // Gap lines replace the generic "best" read on stage fails: the player already
                    // knows they lost, what they need is how far the goal still was.
                    _failGapText = UiFactory.Label(card, "Gap", new Vector2(0.06f, 0.44f), new Vector2(0.94f, 0.53f), 26, TextAnchor.MiddleCenter, Palette.Accent);
                    _failReviveLabel = UiFactory.Pill(card, Loc.Get("fail.revive"), new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.42f), Palette.Accent, Palette.TextOnAccent, 34, OnReviveClicked).GetComponentInChildren<Text>();
                    _failRetryLabel = UiFactory.Pill(card, Loc.Get("fail.retry"), new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.28f), Palette.CannonHub, Palette.TextOnAccent, 34, () =>
                    {
                        ShowInterstitialThen(RetryCurrentRun);
                    }).GetComponentInChildren<Text>();
                    _failMenuLabel = UiFactory.Pill(card, Loc.Get("common.menu"), new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.14f), Palette.HudFill, Palette.TextPrimary, 30, ShowHome).GetComponentInChildren<Text>();
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
                    _clearStarIcons = CreateStarRow(card, new Vector2(0.24f, 0.42f), new Vector2(0.76f, 0.52f), out _clearStarRow);
                    _clearHintText = UiFactory.Label(card, "Hint", new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.42f), 26, TextAnchor.MiddleCenter, Palette.Accent);
                    _clearNextLabel = UiFactory.Pill(card, Loc.Get("clear.next"), new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.33f), Palette.AccentCool, Palette.TextOnAccent, 34, OnClearNextClicked).GetComponentInChildren<Text>();
                    _clearReplayButton = UiFactory.Pill(card, Loc.Get("clear.replayStars"), new Vector2(0.08f, 0.11f), new Vector2(0.92f, 0.21f), Palette.CannonHub, Palette.TextOnAccent, 30, OnClearReplayClicked).gameObject;
                    _clearReplayLabel = _clearReplayButton.GetComponentInChildren<Text>();
                    _clearMenuButton = UiFactory.Pill(card, Loc.Get("common.menu"), new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.10f), Palette.HudFill, Palette.TextPrimary, 28, ShowHome).gameObject;
                    _clearMenuLabel = _clearMenuButton.GetComponentInChildren<Text>();
                });

            _pausePanel = BuildPause(canvasGo.transform);
            _briefingPanel = BuildBriefing(canvasGo.transform);
            _menuPanel = BuildHome(canvasGo.transform);
            _stageSelectPanel = BuildStageSelect(canvasGo.transform);
            _settingsPanel = BuildSettings(canvasGo.transform);
            _splashPanel = BuildSplash(canvasGo.transform);
            SetHudVisible(false);
        }

        GameObject BuildPause(Transform parent)
        {
            var root = UiFactory.NewUiObject("PausePanel", parent);
            UiFactory.Anchor((RectTransform)root.transform, Vector2.zero, Vector2.one);

            UiFactory.Scrim(root.transform, "Scrim", Palette.Scrim);

            var card = UiFactory.Panel(root.transform, "Card", new Vector2(0.12f, 0.30f), new Vector2(0.88f, 0.70f), Palette.CardFill, 0.45f);
            UiFactory.Panel(card.transform, "Accent", new Vector2(0.35f, 0.95f), new Vector2(0.65f, 0.975f), Palette.Accent, 3f);

            _pauseTitle = UiFactory.Label(card.transform, "Title", new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.92f), 48, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold, Loc.Get("pause.title"));
            UiFactory.AddDropShadow(_pauseTitle, 4f);

            _pauseResumeLabel = UiFactory.Pill(card.transform, Loc.Get("pause.resume"), new Vector2(0.1f, 0.52f), new Vector2(0.9f, 0.68f), Palette.AccentCool, Palette.TextOnAccent, 34, ResumePause).GetComponentInChildren<Text>();
            _pauseRetryLabel = UiFactory.Pill(card.transform, Loc.Get("pause.retry"), new Vector2(0.1f, 0.32f), new Vector2(0.9f, 0.48f), Palette.CannonHub, Palette.TextOnAccent, 32, () =>
            {
                SetPaused(false);
                ShowInterstitialThen(RetryCurrentRun);
            }).GetComponentInChildren<Text>();
            _pauseHomeLabel = UiFactory.Pill(card.transform, Loc.Get("common.menu"), new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.28f), Palette.HudFill, Palette.TextPrimary, 30, () =>
            {
                SetPaused(false);
                ShowHome();
            }).GetComponentInChildren<Text>();

            root.SetActive(false);
            return root;
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

            _menuStageProgressText = UiFactory.Label(progressCard.transform, "StageProgress", new Vector2(0.06f, 0.58f), new Vector2(0.6f, 0.92f), 32, TextAnchor.MiddleLeft, Palette.Accent, FontStyle.Bold);

            // Stars collected is the long-term goal, so it sits next to stage progress rather
            // than behind a menu: clearing every stage no longer means the game is finished.
            var starIcon = UiFactory.NewUiObject("StarIcon", progressCard.transform);
            _homeStarIcon = starIcon.AddComponent<Image>();
            _homeStarIcon.sprite = Shapes.Star;
            _homeStarIcon.preserveAspect = true;
            _homeStarIcon.raycastTarget = false;
            _homeStarIcon.color = Palette.StarGold;
            UiFactory.Anchor(_homeStarIcon.rectTransform, new Vector2(0.63f, 0.62f), new Vector2(0.73f, 0.88f));

            _homeStarText = UiFactory.Label(progressCard.transform, "StarTotal", new Vector2(0.74f, 0.58f), new Vector2(0.94f, 0.92f), 30, TextAnchor.MiddleLeft, Palette.TextPrimary, FontStyle.Bold);

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

            _menuEndlessLabel = UiFactory.Pill(root.transform, Loc.Get("menu.endless"), new Vector2(0.12f, 0.135f), new Vector2(0.88f, 0.23f), Palette.CannonHub, Palette.TextOnAccent, 30, () =>
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
            _langToggleLabel = UiFactory.Pill(card.transform, Loc.LanguageToggleCaption(), new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.43f), Palette.CannonHub, Palette.TextOnAccent, 28, ToggleLanguage).GetComponentInChildren<Text>();

            _settingsCloseLabel = UiFactory.Pill(card.transform, Loc.Get("settings.close"), new Vector2(0.2f, 0.08f), new Vector2(0.8f, 0.21f), Palette.Accent, Palette.TextOnAccent, 30, HideSettings).GetComponentInChildren<Text>();

            root.SetActive(false);
            return root;
        }

        GameObject BuildStageSelect(Transform parent)
        {
            var root = UiFactory.NewUiObject("StageSelectPanel", parent);
            UiFactory.Anchor((RectTransform)root.transform, Vector2.zero, Vector2.one);

            UiFactory.Scrim(root.transform, "Scrim", Palette.Scrim);

            var card = UiFactory.Panel(root.transform, "Card", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Palette.CardFill, 0.45f);
            UiFactory.Panel(card.transform, "Accent", new Vector2(0.35f, 0.955f), new Vector2(0.65f, 0.978f), Palette.Accent, 3f);

            _stageSelectTitle = UiFactory.Label(card.transform, "Title", new Vector2(0.06f, 0.90f), new Vector2(0.94f, 0.97f), 40, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold, Loc.Get("stage.title"));

            _stageButtons.Clear();
            _stageLabels.Clear();
            _stageStarIcons.Clear();
            _stageSilhouetteRoots.Clear();
            _stageLoadoutPips.Clear();

            const int columns = 2;
            var count = LevelLibrary.Count;
            var rows = (count + columns - 1) / columns;
            const float gridTop = 0.88f;
            const float gridBottom = 0.14f;
            var slotHeight = (gridTop - gridBottom) / rows;
            const float gap = 0.012f;

            for (var i = 0; i < count; i++)
            {
                var index = i;
                var col = i % columns;
                var row = i / columns;
                var x0 = 0.05f + col * 0.46f;
                var x1 = x0 + 0.42f;
                var top = gridTop - row * slotHeight;
                var bottom = top - slotHeight + gap;

                var button = UiFactory.Pill(
                    card.transform,
                    string.Empty,
                    new Vector2(x0, bottom),
                    new Vector2(x1, top),
                    Palette.HudFill,
                    Palette.TextPrimary,
                    22,
                    () => OnStageChosen(index));

                // Empty the default full-bleed label; name lives in a dedicated slot beside the
                // silhouette so the structure reads at a glance without opening the briefing.
                var defaultLabel = button.GetComponentInChildren<Text>();
                if (defaultLabel != null)
                {
                    defaultLabel.gameObject.SetActive(false);
                }

                var silRoot = UiFactory.NewUiObject("Silhouette", button.transform);
                UiFactory.Anchor((RectTransform)silRoot.transform, new Vector2(0.06f, 0.12f), new Vector2(0.40f, 0.88f));
                FillSilhouette(silRoot.transform, LevelLibrary.Get(i));
                _stageSilhouetteRoots.Add(silRoot.transform);

                var label = UiFactory.Label(button.transform, "Name", new Vector2(0.44f, 0.55f), new Vector2(0.96f, 0.92f), 22, TextAnchor.MiddleLeft, Palette.TextPrimary, FontStyle.Bold);
                _stageButtons.Add(button);
                _stageLabels.Add(label);
                _stageStarIcons.Add(CreateStarRow(button.transform, new Vector2(0.46f, 0.30f), new Vector2(0.94f, 0.54f), out _));
                _stageLoadoutPips.Add(CreateAmmoPips(button.transform, new Vector2(0.46f, 0.06f), new Vector2(0.94f, 0.28f)));
            }

            _stageBackLabel = UiFactory.Pill(card.transform, Loc.Get("common.back"), new Vector2(0.2f, 0.03f), new Vector2(0.8f, 0.11f), Palette.CannonHub, Palette.TextOnAccent, 28, () =>
            {
                Sfx.Instance?.Ui();
                ShowHome();
            }).GetComponentInChildren<Text>();

            root.SetActive(false);
            return root;
        }

        GameObject BuildBriefing(Transform parent)
        {
            var root = UiFactory.NewUiObject("BriefingPanel", parent);
            UiFactory.Anchor((RectTransform)root.transform, Vector2.zero, Vector2.one);

            UiFactory.Scrim(root.transform, "Scrim", Palette.Scrim);

            var card = UiFactory.Panel(root.transform, "Card", new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f), Palette.CardFill, 0.45f);
            UiFactory.Panel(card.transform, "Accent", new Vector2(0.35f, 0.95f), new Vector2(0.65f, 0.975f), Palette.Accent, 3f);

            _briefingTitle = UiFactory.Label(card.transform, "Title", new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.95f), 40, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold);
            UiFactory.AddDropShadow(_briefingTitle, 3f);

            var sil = UiFactory.NewUiObject("Silhouette", card.transform);
            UiFactory.Anchor((RectTransform)sil.transform, new Vector2(0.18f, 0.52f), new Vector2(0.82f, 0.84f));
            _briefingSilhouetteRoot = sil.transform;

            _briefingHint = UiFactory.Label(card.transform, "Hint", new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.52f), 26, TextAnchor.MiddleCenter, Palette.Accent);
            _briefingGoal = UiFactory.Label(card.transform, "Goal", new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.42f), 24, TextAnchor.MiddleCenter, Palette.TextMuted);
            _briefingLoadoutCaption = UiFactory.Label(card.transform, "LoadoutCaption", new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.35f), 22, TextAnchor.MiddleCenter, Palette.TextMuted, FontStyle.Normal, Loc.Get("stage.loadout"));

            _briefingPips = new Image[MaxLoadoutPips];
            _briefingPipButtons = new Button[MaxLoadoutPips];
            for (var i = 0; i < MaxLoadoutPips; i++)
            {
                var slot = i;
                var pip = UiFactory.Panel(card.transform, "Pip" + i, new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.28f), Palette.Projectile, 4f);
                pip.sprite = Shapes.Circle;
                pip.type = Image.Type.Simple;
                pip.preserveAspect = true;
                var button = pip.gameObject.AddComponent<Button>();
                button.targetGraphic = pip;
                button.onClick.AddListener(() => OnBriefingPipClicked(slot));
                _briefingPips[i] = pip;
                _briefingPipButtons[i] = button;
            }

            _briefingStartLabel = UiFactory.Pill(card.transform, Loc.Get("stage.start"), new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.17f), Palette.AccentCool, Palette.TextOnAccent, 34, StartFromBriefing).GetComponentInChildren<Text>();
            _briefingBackLabel = UiFactory.Pill(card.transform, Loc.Get("common.back"), new Vector2(0.2f, 0.01f), new Vector2(0.8f, 0.07f), Palette.HudFill, Palette.TextPrimary, 26, HideBriefing).GetComponentInChildren<Text>();

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

        /// <summary>
        /// One dot per shot in the stage loadout. Built at the widest supported size and laid out
        /// for real in <see cref="RefreshAmmoPips"/>, which knows the current stage's count.
        /// </summary>
        static Image[] CreateAmmoPips(Transform parent, Vector2 min, Vector2 max)
        {
            var row = UiFactory.NewUiObject("AmmoPips", parent);
            UiFactory.Anchor((RectTransform)row.transform, min, max);

            var pips = new Image[MaxLoadoutPips];
            for (var i = 0; i < pips.Length; i++)
            {
                var go = UiFactory.NewUiObject("Pip" + i, row.transform);
                var image = go.AddComponent<Image>();
                image.sprite = Shapes.Circle;
                image.preserveAspect = true;
                image.raycastTarget = false;
                pips[i] = image;
            }

            return pips;
        }

        /// <summary>
        /// Tiny coloured grid of the stage's footprint. Reads as the structure's silhouette on the
        /// stage list and briefing without spawning a second world copy.
        /// </summary>
        static void FillSilhouette(Transform root, LevelLayout layout)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(root.GetChild(i).gameObject);
            }

            if (layout == null || layout.FootprintWidth <= 0 || layout.Height <= 0)
            {
                return;
            }

            var cols = layout.FootprintWidth;
            var rows = layout.Height;
            var cellW = 1f / cols;
            var cellH = 1f / rows;
            const float inset = 0.06f;

            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    var kind = layout.PieceAt(layout.FirstColumn + col, row);
                    if (kind == PieceKind.None)
                    {
                        continue;
                    }

                    var go = UiFactory.NewUiObject("Cell", root);
                    var image = go.AddComponent<Image>();
                    image.sprite = Shapes.Solid;
                    image.color = PieceTint(kind);
                    image.raycastTarget = false;
                    UiFactory.Anchor(
                        image.rectTransform,
                        new Vector2(col * cellW + inset * cellW, row * cellH + inset * cellH),
                        new Vector2((col + 1) * cellW - inset * cellW, (row + 1) * cellH - inset * cellH));
                }
            }
        }

        static Color PieceTint(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Brittle: return Palette.Brittle;
                case PieceKind.Explosive: return Palette.Explosive;
                case PieceKind.Heavy: return Palette.Heavy;
                case PieceKind.Ball: return Palette.Can;
                case PieceKind.Pillar: return Palette.Pillar;
                case PieceKind.Beam: return Palette.Beam;
                default: return Palette.Blocks[0];
            }
        }

        static void LayoutLoadoutPips(Image[] pips, string loadout, int highlight = -1)
        {
            if (pips == null)
            {
                return;
            }

            var count = loadout != null ? Mathf.Min(loadout.Length, pips.Length) : 0;
            var cell = count > 0 ? 1f / count : 1f;
            const float gap = 0.08f;

            for (var i = 0; i < pips.Length; i++)
            {
                var pip = pips[i];
                if (pip == null)
                {
                    continue;
                }

                var inUse = i < count;
                pip.gameObject.SetActive(inUse);
                if (!inUse)
                {
                    continue;
                }

                UiFactory.Anchor(
                    pip.rectTransform,
                    new Vector2(cell * i + gap * cell, 0f),
                    new Vector2(cell * (i + 1) - gap * cell, 1f));

                var tint = ProjectileTint(LevelLayout.ShotAt(loadout, i));
                pip.color = i == highlight
                    ? Color.Lerp(tint, Color.white, 0.45f)
                    : tint;
            }
        }

        /// <summary>
        /// Three evenly spaced rating stars under a container, so a caller can hide the whole row
        /// in modes that are not rated. Tinting happens later, in <see cref="RefreshStars"/>.
        /// </summary>
        static Image[] CreateStarRow(Transform parent, Vector2 min, Vector2 max, out GameObject row)
        {
            row = UiFactory.NewUiObject("Stars", parent);
            UiFactory.Anchor((RectTransform)row.transform, min, max);

            var icons = new Image[3];
            var cell = 1f / icons.Length;
            const float padding = 0.04f;
            for (var i = 0; i < icons.Length; i++)
            {
                var go = UiFactory.NewUiObject("Star" + i, row.transform);
                var image = go.AddComponent<Image>();
                image.sprite = Shapes.Star;
                image.preserveAspect = true;
                image.raycastTarget = false;
                UiFactory.Anchor(
                    image.rectTransform,
                    new Vector2(cell * i + padding, 0f),
                    new Vector2(cell * (i + 1) - padding, 1f));
                icons[i] = image;
            }

            return icons;
        }

        static void RefreshStars(Image[] icons, int stars)
        {
            if (icons == null)
            {
                return;
            }

            for (var i = 0; i < icons.Length; i++)
            {
                if (icons[i] != null)
                {
                    icons[i].color = i < stars ? Palette.StarGold : UnearnedStarTint;
                }
            }
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

            // Centre on the columns the layout actually fills, not on the grid it was written in,
            // so a narrow stack sits on a narrow pad instead of off to one side of a wide one.
            LayoutShelf(Mathf.Max(ShelfMinWidth, _layout.FootprintWidth * BlockPitch + ShelfMargin));
            var originX = ShelfCenterX - _layout.CenterColumn * BlockPitch;

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
                    SpawnPiece(kind, x, ShelfTopY + row * BlockPitch, row, column, span);
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
        void SpawnPiece(PieceKind kind, float x, float cellBottom, int row, int column, int span)
        {
            if (kind == PieceKind.Ball)
            {
                const float radius = BallRadius;
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

            var piece = CreateSlicedRenderer(kind.ToString(), new Vector3(x, cellBottom + size.y * 0.5f, 0f), size, Course(color, row, column), SortingOrders.Block);
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
                var core = CreateCircleRenderer("Core", Vector3.zero, 0.2f, Palette.ExplosionCore, SortingOrders.Block + 1);
                core.transform.SetParent(piece.transform, false);
                core.transform.localPosition = Vector3.zero;
            }

            RegisterTarget(piece.gameObject, kind);
        }

        /// <summary>
        /// Alternates brightness like bricks in a course. Pieces of one kind share a tint, and at
        /// the current block size neighbours in a row otherwise fuse into a single bar, which hides
        /// exactly the seams the player is aiming at.
        /// </summary>
        static Color Course(Color color, int row, int column)
        {
            var shade = ((row + column) & 1) == 0 ? 1.06f : 0.94f;
            return new Color(color.r * shade, color.g * shade, color.b * shade, color.a);
        }

        void RegisterTarget(GameObject target, PieceKind kind)
        {
            var destructible = target.AddComponent<DestructibleBlock>();
            destructible.Configure(
                MaterialFor(kind),
                ShelfTopY,
                ShelfCenterX - _shelfHalfWidth,
                ShelfCenterX + _shelfHalfWidth,
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
            if (_shotInFlight)
            {
                return;
            }

            // Read the loadout before spending the shot, while the remaining count still points
            // at the round about to be fired.
            var kind = CurrentShotKind();
            if (!_loop.TryConsumeAmmo())
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
            SpawnProjectile(kind, spawn, velocity);

            HitBurst.Play(_effectRoot, spawn, Palette.Accent, 6, 3.5f);
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.06f);
            }

            ShowShotHint(kind);
            RefreshHud();
        }

        /// <summary>The shot the cannon is about to fire, from this run's (possibly reordered) loadout.</summary>
        ProjectileKind CurrentShotKind()
        {
            if (string.IsNullOrEmpty(_runLoadout))
            {
                return ProjectileKind.Ball;
            }

            return LevelLayout.ShotAt(_runLoadout, _runLoadout.Length - _loop.Ammo);
        }

        Projectile SpawnProjectile(ProjectileKind kind, Vector3 position, Vector2 velocity)
        {
            var radius = ProjectileRadius(kind);
            var tint = ProjectileTint(kind);
            var ball = CreateCircleRenderer("Projectile", position, radius, tint, SortingOrders.Projectile);
            ball.transform.SetParent(_projectileRoot);

            // Shapes.Circle is one unit wide and the transform carries the diameter, so a local
            // radius of 0.5 always matches the world-space visual radius.
            var projectileCollider = ball.gameObject.AddComponent<CircleCollider2D>();
            projectileCollider.radius = 0.5f;
            foreach (var terrainCollider in _terrainColliders)
            {
                if (terrainCollider != null)
                {
                    Physics2D.IgnoreCollision(projectileCollider, terrainCollider);
                }
            }

            if (kind == ProjectileKind.Charge)
            {
                // A bright core reads as "armed" and separates the charge from a plain ball at a
                // glance, which matters because the two fly identically.
                var core = CreateCircleRenderer("Core", position, radius, Palette.ExplosionCore, SortingOrders.Projectile + 1);
                core.transform.SetParent(ball.transform, false);
                core.transform.localPosition = Vector3.zero;
                // Local, so it scales with the shell the renderer already sized.
                core.transform.localScale = Vector3.one * 0.45f;
            }

            var trail = ball.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = radius * 1.55f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(tint.r, tint.g, tint.b, 0.75f);
            trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
            trail.sortingOrder = SortingOrders.Trail;

            var body = ball.gameObject.AddComponent<Rigidbody2D>();
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.mass = ProjectileMass(kind);
            body.linearDamping = 0.05f;
            body.sharedMaterial = ProjectileMaterial;

            var projectile = ball.gameObject.AddComponent<Projectile>();
            var hitWatcher = ball.gameObject.AddComponent<ProjectileHitWatcher>();
            hitWatcher.Hit += OnProjectileHit;
            hitWatcher.SurfaceHit += OnProjectileSurfaceHit;

            _liveProjectiles++;
            projectile.Launch(kind, velocity, OnProjectileResolved, OnProjectileSpecial);
            return projectile;
        }

        static float ProjectileRadius(ProjectileKind kind)
        {
            return kind == ProjectileKind.Fragment ? 0.15f : Projectile.Radius;
        }

        static float ProjectileMass(ProjectileKind kind)
        {
            switch (kind)
            {
                case ProjectileKind.Fragment: return 0.5f;
                case ProjectileKind.Cluster: return 1.2f;
                case ProjectileKind.Charge: return 1.2f;
                default: return 1.35f;
            }
        }

        static Color ProjectileTint(ProjectileKind kind)
        {
            switch (kind)
            {
                case ProjectileKind.Cluster:
                case ProjectileKind.Fragment: return Palette.AccentCool;
                case ProjectileKind.Charge: return Palette.Explosive;
                default: return Palette.Projectile;
            }
        }

        /// <summary>
        /// A shot ends when nothing of it is left in the air. Counting rather than resolving on
        /// the first projectile matters for clusters, whose fragments outlive the round that
        /// spawned them.
        /// </summary>
        void OnProjectileResolved(Projectile projectile)
        {
            _liveProjectiles = Mathf.Max(0, _liveProjectiles - 1);
            if (_liveProjectiles == 0)
            {
                HideShotHint();
                StartCoroutine(ResolveShotRoutine());
            }
        }

        void OnProjectileSpecial(Projectile projectile)
        {
            if (projectile.Kind == ProjectileKind.Cluster)
            {
                SplitCluster(projectile);
                return;
            }

            if (projectile.Kind == ProjectileKind.Charge)
            {
                DetonateCharge(projectile);
            }
        }

        void SplitCluster(Projectile source)
        {
            const int fragments = 3;
            const float spreadDegrees = 15f;

            var origin = source.transform.position;
            var velocity = source.Velocity;
            var heading = velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector2.up;
            var speed = Mathf.Max(velocity.magnitude, 7f) * 0.95f;

            HideShotHint();
            _impactFeedback?.PlayContact(new ImpactEvent(origin, -heading, heading * speed, 26f), Palette.AccentCool);

            // Spread across the flight path rather than along it, and drop back slightly: a split
            // triggered by impact happens while the cluster is touching a block, so spawning
            // forward would bury the fragments inside that collider and fire them out sideways.
            var lateral = new Vector2(-heading.y, heading.x);

            // Spawn before retiring the parent: the live count must never touch zero mid-split, or
            // the shot resolves and hands control back while the fragments are still in the air.
            for (var i = 0; i < fragments; i++)
            {
                var rank = i - (fragments - 1) * 0.5f;
                var direction = (Vector2)(Quaternion.Euler(0f, 0f, rank * spreadDegrees) * heading);
                var spawn = origin + (Vector3)(lateral * (rank * 0.3f) - heading * 0.12f);
                SpawnProjectile(ProjectileKind.Fragment, spawn, direction * speed);
            }

            source.Resolve();
        }

        void DetonateCharge(Projectile charge)
        {
            var centre = charge.transform.position;
            HideShotHint();
            _lastDestructionTime = Time.time;
            _impactFeedback?.PlayExplosion(centre, Mathf.Max(1, _loop.Combo));
            Explode(centre, null);
            charge.Resolve();
        }

        /// <summary>Spends the pending special on whatever is still in the air.</summary>
        void TriggerLiveSpecials()
        {
            if (_projectileRoot == null)
            {
                return;
            }

            var live = _projectileRoot.GetComponentsInChildren<Projectile>();
            foreach (var projectile in live)
            {
                if (projectile.TryTrigger())
                {
                    return;
                }
            }
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
            _cannon.CanFire = state == GameState.Playing && !_shotInFlight && !_paused;
            if (state == GameState.Failed)
            {
                Sfx.Instance?.Failed();
                Haptics.Light();
                SetPaused(false);
                ShowResult(_failScoreText, _failBestText);
                RefreshFailGaps();
                _failPanel.SetActive(true);
                _ftueHint?.Hide();
            }
            else if (state == GameState.Cleared)
            {
                var stars = 0;
                var newBest = false;
                if (runMode == RunMode.Stage)
                {
                    Progress.UnlockAfterClear(_stageIndex, LevelLibrary.Count);
                    stars = _layout != null ? _layout.StarsFor(_loop.Score, _loop.TargetsRemaining <= 0) : 0;
                    newBest = Progress.SubmitStage(_stageIndex, _loop.Score, stars);
                }

                Sfx.Instance?.Cleared();
                Haptics.Light();
                SetPaused(false);
                ShowResult(_clearScoreText, _clearBestText, newBest);
                RefreshStars(_clearStarIcons, stars);
                if (_clearStarRow != null)
                {
                    // Only stages carry a rating; endless and daily runs are scored, not graded.
                    _clearStarRow.SetActive(runMode == RunMode.Stage);
                }

                RefreshClearHint(stars);
                RefreshClearActions();
                _clearPanel.SetActive(true);
                _ftueHint?.Hide();
            }
        }

        /// <summary>Fills a result card's score line and commits the run to the best-score store.</summary>
        void ShowResult(Text scoreText, Text bestText, bool stageNewBest = false)
        {
            var score = _loop.Score;
            scoreText.text = _loop.TargetScore > 0
                ? Loc.Format("result.scoreGoal", score, _loop.TargetScore)
                : Loc.Format("result.score", score);

            if (runMode == RunMode.Stage)
            {
                // Stage runs were already committed by the caller, which knows whether this run
                // beat the stored score; re-reading the store here would always say it did not.
                if (stageNewBest)
                {
                    bestText.text = Loc.Get("result.newBest");
                    bestText.color = Palette.Accent;
                }
                else
                {
                    bestText.text = Loc.Format("result.best", Progress.StageBest(_stageIndex));
                    bestText.color = Palette.TextMuted;
                }

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
        /// On a failed stage, the useful number is how far the goal still was — a stored best does
        /// not tell the player what to aim for next attempt. Endless/daily keep the best line.
        /// </summary>
        void RefreshFailGaps()
        {
            if (_failGapText == null)
            {
                return;
            }

            if (runMode != RunMode.Stage || _layout == null)
            {
                _failGapText.gameObject.SetActive(false);
                return;
            }

            if (_layout.RequiresClearAll)
            {
                if (_failBestText != null)
                {
                    _failBestText.text = Loc.Format("fail.leftStanding", _loop.TargetsRemaining);
                    _failBestText.color = Palette.Accent;
                }

                _failGapText.gameObject.SetActive(false);
                return;
            }

            var score = _loop.Score;
            var shortOfGoal = Mathf.Max(0, _layout.TargetScore - score);
            var shortOfTwo = Mathf.Max(0, _layout.TwoStarScore - score);

            if (_failBestText != null)
            {
                _failBestText.text = Loc.Format("fail.shortGoal", shortOfGoal);
                _failBestText.color = Palette.Accent;
            }

            _failGapText.gameObject.SetActive(true);
            _failGapText.text = Loc.Format("fail.shortStars", shortOfTwo, Loc.Get("star.two"));
            _failGapText.color = Palette.TextMuted;
        }

        /// <summary>
        /// After a clear, the next star bar is the reason to replay. Without it the sheet only
        /// pushes "next stage", and one-star clears never get chased.
        /// </summary>
        void RefreshClearHint(int stars)
        {
            if (_clearHintText == null)
            {
                return;
            }

            if (runMode != RunMode.Stage || _layout == null)
            {
                _clearHintText.gameObject.SetActive(false);
                if (_clearReplayButton != null) _clearReplayButton.SetActive(false);
                return;
            }

            _clearHintText.gameObject.SetActive(true);
            if (_clearReplayButton != null) _clearReplayButton.SetActive(true);

            if (stars >= 3 || _layout.RequiresClearAll)
            {
                _clearHintText.text = Loc.Get("clear.perfect");
                _clearHintText.color = Palette.GroundEdge;
                if (_clearReplayLabel != null) _clearReplayLabel.text = Loc.Get("clear.replay");
                return;
            }

            var nextBar = stars >= 2 ? _layout.ThreeStarScore : _layout.TwoStarScore;
            var nextMark = stars >= 2 ? Loc.Get("star.three") : Loc.Get("star.two");
            var need = Mathf.Max(0, nextBar - _loop.Score);
            _clearHintText.text = Loc.Format("clear.moreFor", need, nextMark);
            _clearHintText.color = Palette.Accent;
            if (_clearReplayLabel != null) _clearReplayLabel.text = Loc.Get("clear.replayStars");
        }

        void OnClearReplayClicked()
        {
            Sfx.Instance?.Ui();
            RetryCurrentRun();
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
            if (_hudHomeLabel != null) _hudHomeLabel.text = Loc.Get("hud.pause");
            if (_failTitle != null) _failTitle.text = Loc.Get("fail.title");
            if (_clearTitle != null) _clearTitle.text = Loc.Get("clear.title");
            if (_failReviveLabel != null) _failReviveLabel.text = Loc.Get("fail.revive");
            if (_failRetryLabel != null) _failRetryLabel.text = Loc.Get("fail.retry");
            if (_failMenuLabel != null) _failMenuLabel.text = Loc.Get("common.menu");
            if (_clearMenuLabel != null) _clearMenuLabel.text = Loc.Get("common.menu");
            if (_clearReplayLabel != null)
            {
                _clearReplayLabel.text = Loc.Get("clear.replayStars");
            }

            if (_pauseTitle != null) _pauseTitle.text = Loc.Get("pause.title");
            if (_pauseResumeLabel != null) _pauseResumeLabel.text = Loc.Get("pause.resume");
            if (_pauseRetryLabel != null) _pauseRetryLabel.text = Loc.Get("pause.retry");
            if (_pauseHomeLabel != null) _pauseHomeLabel.text = Loc.Get("common.menu");
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

            if (_briefingPanel != null && _briefingPanel.activeSelf)
            {
                ShowBriefing(_briefingStage);
            }
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
            SetPaused(false);
            SetScreen(AppScreen.Home);
            _cannon.CanFire = false;
            _shotInFlight = false;
            if (_failPanel != null) _failPanel.SetActive(false);
            if (_clearPanel != null) _clearPanel.SetActive(false);
            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_briefingPanel != null) _briefingPanel.SetActive(false);
            _ftueHint?.Hide();
            ClearBoard();
            RefreshHomeStats();
        }

        void ShowStageSelect()
        {
            SetPaused(false);
            SetScreen(AppScreen.StageSelect);
            _cannon.CanFire = false;
            _shotInFlight = false;
            if (_failPanel != null) _failPanel.SetActive(false);
            if (_clearPanel != null) _clearPanel.SetActive(false);
            if (_pausePanel != null) _pausePanel.SetActive(false);
            if (_briefingPanel != null) _briefingPanel.SetActive(false);
            _ftueHint?.Hide();
            ClearBoard();
            RefreshStageSelectLabels();
        }

        void ShowPause()
        {
            if (_screen != AppScreen.Play)
            {
                return;
            }

            if (_loop.State != GameState.Playing && _loop.State != GameState.Resolve)
            {
                return;
            }

            // Fail/clear sheets own the screen; pause must not stack under them.
            if ((_failPanel != null && _failPanel.activeSelf) || (_clearPanel != null && _clearPanel.activeSelf))
            {
                return;
            }

            Sfx.Instance?.Ui();
            SetPaused(true);
            if (_pausePanel != null)
            {
                _pausePanel.SetActive(true);
            }
        }

        void ResumePause()
        {
            Sfx.Instance?.Ui();
            SetPaused(false);
            if (_pausePanel != null)
            {
                _pausePanel.SetActive(false);
            }

            // ArmAfterPointerRelease forces CanFire on; only do that when a fresh shot is legal.
            if (!_shotInFlight && _loop.State == GameState.Playing)
            {
                _cannon.ArmAfterPointerRelease();
            }
        }

        void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (_cannon != null)
            {
                _cannon.CanFire = !paused && _loop.State == GameState.Playing && !_shotInFlight;
            }
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

            if (_pausePanel != null && screen != AppScreen.Play)
            {
                _pausePanel.SetActive(false);
            }

            if (_briefingPanel != null && screen != AppScreen.StageSelect)
            {
                _briefingPanel.SetActive(false);
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

            if (_homeStarText != null)
            {
                _homeStarText.text = Loc.Format(
                    "menu.stars",
                    Progress.TotalStars(LevelLibrary.Count),
                    LevelLibrary.Count * 3);
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

                if (i < _stageStarIcons.Count)
                {
                    RefreshStars(_stageStarIcons[i], unlocked ? Progress.StageStars(i) : 0);
                }

                if (i < _stageLoadoutPips.Count)
                {
                    LayoutLoadoutPips(_stageLoadoutPips[i], unlocked ? layout.Loadout : null);
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
            ShowBriefing(index);
        }

        void ShowBriefing(int index)
        {
            _briefingStage = index;
            var layout = LevelLibrary.Get(index);
            _briefingLoadout = layout.Loadout;
            _briefingPick = -1;

            if (_briefingTitle != null)
            {
                _briefingTitle.text = Loc.Format("stage.item", index + 1, Loc.LevelName(layout.Name));
            }

            if (_briefingHint != null)
            {
                _briefingHint.text = Loc.Get(layout.HintKey);
            }

            if (_briefingGoal != null)
            {
                _briefingGoal.text = layout.RequiresClearAll
                    ? Loc.Get("stage.goal.clear")
                    : Loc.Format("stage.goal.score", layout.TargetScore);
            }

            if (_briefingLoadoutCaption != null)
            {
                _briefingLoadoutCaption.text = Loc.Get("stage.loadout");
            }

            if (_briefingStartLabel != null) _briefingStartLabel.text = Loc.Get("stage.start");
            if (_briefingBackLabel != null) _briefingBackLabel.text = Loc.Get("common.back");

            FillSilhouette(_briefingSilhouetteRoot, layout);
            RefreshBriefingPips();

            if (_briefingPanel != null)
            {
                // Built before the stage list, so without this it would open underneath the grid
                // and look like the tap did nothing.
                _briefingPanel.transform.SetAsLastSibling();
                _briefingPanel.SetActive(true);
            }
        }

        void HideBriefing()
        {
            Sfx.Instance?.Ui();
            if (_briefingPanel != null)
            {
                _briefingPanel.SetActive(false);
            }

            _briefingPick = -1;
        }

        void StartFromBriefing()
        {
            Sfx.Instance?.Ui();
            var loadout = _briefingLoadout;
            if (_briefingPanel != null)
            {
                _briefingPanel.SetActive(false);
            }

            BeginRun(RunMode.Stage, _briefingStage, loadout);
        }

        void OnBriefingPipClicked(int index)
        {
            if (string.IsNullOrEmpty(_briefingLoadout) || index < 0 || index >= _briefingLoadout.Length)
            {
                return;
            }

            Sfx.Instance?.Ui();
            if (_briefingPick < 0 || _briefingPick == index)
            {
                _briefingPick = _briefingPick == index ? -1 : index;
                RefreshBriefingPips();
                return;
            }

            _briefingLoadout = SwapChars(_briefingLoadout, _briefingPick, index);
            _briefingPick = -1;
            RefreshBriefingPips();
        }

        void RefreshBriefingPips()
        {
            LayoutLoadoutPips(_briefingPips, _briefingLoadout, _briefingPick);
        }

        /// <summary>
        /// Swaps the next two unfired shots. Mid-run loadout edits are limited to the upcoming
        /// pair so the player cannot reshuffle spent rounds or invent ammo.
        /// </summary>
        void SwapNextTwoShots()
        {
            if (_screen != AppScreen.Play || _paused || _shotInFlight || _loop.State != GameState.Playing)
            {
                return;
            }

            if (string.IsNullOrEmpty(_runLoadout) || _loop.Ammo < 2)
            {
                return;
            }

            var next = _runLoadout.Length - _loop.Ammo;
            _runLoadout = SwapChars(_runLoadout, next, next + 1);
            Sfx.Instance?.Ui();
            RefreshAmmoPips();
            _cannon.ArmAfterPointerRelease();
        }

        static string SwapChars(string source, int a, int b)
        {
            if (string.IsNullOrEmpty(source) || a < 0 || b < 0 || a >= source.Length || b >= source.Length || a == b)
            {
                return source;
            }

            var chars = source.ToCharArray();
            (chars[a], chars[b]) = (chars[b], chars[a]);
            return new string(chars);
        }

        void RetryCurrentRun()
        {
            // Retries keep the player's last briefing order so a tuned queue is not thrown away
            // after a near miss.
            BeginRun(runMode, _stageIndex, runMode == RunMode.Stage ? _runLoadout : null);
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

                // Replay stays even on the last stage: that is where "chase three stars" matters most.
                SetSecondaryClearVisible(!isLast);
                return;
            }

            if (runMode == RunMode.Daily)
            {
                _clearNextLabel.text = Loc.Get("clear.home");
                if (_clearTitle != null) _clearTitle.text = Loc.Get("clear.title");
                SetSecondaryClearVisible(false);
                if (_clearReplayButton != null) _clearReplayButton.SetActive(false);
                return;
            }

            _clearNextLabel.text = Loc.Get("menu.endless");
            if (_clearTitle != null) _clearTitle.text = Loc.Get("clear.title");
            SetSecondaryClearVisible(true);
            if (_clearReplayButton != null) _clearReplayButton.SetActive(false);
        }

        /// <summary>
        /// The secondary action always goes home, so it has to disappear whenever the primary
        /// already does; two buttons reading HOME leaves the player picking between identical twins.
        /// </summary>
        void SetSecondaryClearVisible(bool visible)
        {
            if (_clearMenuButton != null)
            {
                _clearMenuButton.SetActive(visible);
            }
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
            if (_scoreCaption != null)
            {
                _scoreCaption.text = _loop.RequiresClearAll
                    ? Loc.Get("hud.clearCaption")
                    : Loc.Get("hud.scoreCaption");
            }

            if (_scoreText != null)
            {
                if (_loop.RequiresClearAll)
                {
                    _scoreText.text = Loc.Format("hud.clearGoal", _loop.TargetsRemaining);
                }
                else
                {
                    _scoreText.text = _loop.TargetScore > 0
                        ? Loc.Format("hud.scoreGoal", _loop.Score, _loop.TargetScore)
                        : _loop.Score.ToString();
                }
            }

            if (_ammoText != null)
            {
                _ammoText.text = _loop.Ammo.ToString();
            }

            RefreshAmmoPips();

            if (_goalFill != null)
            {
                float progress;
                if (_loop.RequiresClearAll)
                {
                    progress = _targetTotal > 0
                        ? Mathf.Clamp01(1f - _loop.TargetsRemaining / (float)_targetTotal)
                        : 0f;
                }
                else
                {
                    progress = _loop.TargetScore > 0
                        ? Mathf.Clamp01(_loop.Score / (float)_loop.TargetScore)
                        : 0f;
                }

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

            if (_swapHint != null)
            {
                var canSwap = _screen == AppScreen.Play && _loop.State == GameState.Playing && !_shotInFlight && _loop.Ammo >= 2;
                _swapHint.text = canSwap ? Loc.Get("stage.swapNext") : string.Empty;
            }
        }

        void RefreshAmmoPips()
        {
            if (_ammoPips == null)
            {
                return;
            }

            var loadout = _runLoadout;
            var count = !string.IsNullOrEmpty(loadout) ? Mathf.Min(loadout.Length, _ammoPips.Length) : 0;
            var fired = !string.IsNullOrEmpty(loadout) ? loadout.Length - _loop.Ammo : 0;
            var cell = count > 0 ? 1f / count : 1f;

            for (var i = 0; i < _ammoPips.Length; i++)
            {
                var pip = _ammoPips[i];
                if (pip == null)
                {
                    continue;
                }

                var inUse = i < count;
                pip.gameObject.SetActive(inUse);
                if (!inUse)
                {
                    continue;
                }

                const float gap = 0.02f;
                UiFactory.Anchor(
                    pip.rectTransform,
                    new Vector2(cell * i + gap, 0f),
                    new Vector2(cell * (i + 1) - gap, 1f));

                var tint = ProjectileTint(LevelLayout.ShotAt(loadout, i));
                pip.color = i < fired ? new Color(tint.r, tint.g, tint.b, 0.22f) : tint;
            }
        }

        /// <summary>Announces that this shot answers a tap. Plain balls say nothing.</summary>
        void ShowShotHint(ProjectileKind kind)
        {
            if (_shotHint == null)
            {
                return;
            }

            if (!Projectile.HasSpecial(kind))
            {
                HideShotHint();
                return;
            }

            _shotHint.text = Loc.Get(kind == ProjectileKind.Cluster ? "shot.tapCluster" : "shot.tapCharge");
            _shotHint.color = ProjectileTint(kind);
            _shotHint.gameObject.SetActive(true);
        }

        void HideShotHint()
        {
            if (_shotHint != null)
            {
                _shotHint.gameObject.SetActive(false);
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

        /// <summary>
        /// Hazard tape along a platform lip. Tiled so the diagonal stripes stay square regardless
        /// of how long the trim is, which is what sells the demolition-site read.
        /// </summary>
        SpriteRenderer CreateHazardTrim(string name, Vector3 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_worldRoot);
            go.transform.position = position;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Shapes.Hazard;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = size;
            renderer.sortingOrder = SortingOrders.Structure + 1;
            return renderer;
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
