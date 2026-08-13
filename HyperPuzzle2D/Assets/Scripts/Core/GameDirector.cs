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
    /// <summary>
    /// W1 playable director: builds a smash level and wires cannon / ads / HUD.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        // Play field is authored against these half-extents; the camera fits them to any aspect.
        const float FieldHalfWidth = 7.2f;
        const float FieldHalfHeight = 6.6f;

        const float ShelfTopY = -2.25f;
        const float ShelfCenterX = 2f;
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

        static readonly Vector3 CannonPivot = new Vector3(-4.4f, -3.55f, 0f);

        [SerializeField] int reviveAmmo = 2;
        [SerializeField] RunMode runMode = RunMode.Endless;

        readonly GameLoop _loop = new GameLoop();
        readonly DailyChallenge _daily = new DailyChallenge();
        readonly List<DestructibleBlock> _blocks = new List<DestructibleBlock>();

        IAdService _ads;
        CannonController _cannon;
        ComboPresenter _combo;
        Transform _worldRoot;
        Transform _projectileRoot;
        Transform _effectRoot;
        Text _scoreText;
        Text _ammoText;
        Text _scoreCaption;
        Text _ammoCaption;
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
        Text _menuPlayLabel;
        Text _menuDailyLabel;
        Text _sfxToggleLabel;
        Text _hapticsToggleLabel;
        Text _langToggleLabel;
        GameObject _failPanel;
        GameObject _clearPanel;
        GameObject _menuPanel;
        Transform _canvasRoot;
        FtueHint _ftueHint;
        LevelLayout _layout;
        bool _shotInFlight;
        int _pendingHits;
        int _seed;

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
            ShowMenu();
        }

        void OnDestroy()
        {
            Loc.Changed -= OnLanguageChanged;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void BeginRun(RunMode mode)
        {
            runMode = mode;
            Progress.MarkRunStarted();
            _seed = mode == RunMode.Daily ? _daily.TodaySeed : Random.Range(1, 999999);
            ClearBoard();
            SpawnStructure(_seed);
            _loop.StartRun(mode, _layout.Ammo, _blocks.Count);
            _cannon.CanFire = true;
            _shotInFlight = false;
            if (_failPanel != null) _failPanel.SetActive(false);
            if (_clearPanel != null) _clearPanel.SetActive(false);
            RefreshHud();
            RefreshFtue();
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
            cam.orthographicSize = Mathf.Max(FieldHalfHeight, FieldHalfWidth / aspect);

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
            cam.gameObject.AddComponent<ViewportFitter>()
                .Configure(new Vector2(FieldHalfWidth, FieldHalfHeight), backdrop);

            BuildTerrain();
            BuildCannon();

            _combo = gameObject.AddComponent<ComboPresenter>();
            _loop.StateChanged += OnStateChanged;
            _loop.ScoreChanged += _ => RefreshHud();
            _loop.AmmoChanged += _ => RefreshHud();
            _loop.ComboChanged += c => _combo.ShowCombo(c);
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
            glow.transform.position = new Vector3(ShelfCenterX, 0.4f, 0f);
            glow.transform.localScale = Vector3.one * 13f;
            var glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = Shapes.Glow;
            glowRenderer.color = new Color(Palette.BackdropGlow.r, Palette.BackdropGlow.g, Palette.BackdropGlow.b, 0.32f);
            glowRenderer.sortingOrder = SortingOrders.BackdropGlow;

            var rng = new System.Random(7);
            for (var i = 0; i < 22; i++)
            {
                var mote = new GameObject("Mote");
                mote.transform.SetParent(root, false);
                mote.transform.position = new Vector3(
                    Mathf.Lerp(-FieldHalfWidth, FieldHalfWidth, (float)rng.NextDouble()),
                    Mathf.Lerp(-FieldHalfHeight, FieldHalfHeight, (float)rng.NextDouble()),
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
            // Cannon plinth on the left.
            CreateSolid("Plinth", new Vector3(-4.4f, -5f, 0f), new Vector2(4.8f, 1.6f), Palette.Ground);
            CreateTrim("PlinthTrim", new Vector3(-4.4f, -4.16f, 0f), new Vector2(4.8f, 0.09f), Palette.GroundEdge);

            // Support column: the target shelf is an island, so knocked-off blocks fall away.
            CreateSolid("Column", new Vector3(ShelfCenterX, -4.85f, 0f), new Vector2(1.2f, 4.2f), Palette.Wall);

            CreateSolid("Shelf", new Vector3(ShelfCenterX, ShelfTopY - 0.25f, 0f), new Vector2(4.6f, 0.5f), Palette.Shelf);
            CreateTrim("ShelfTrim", new Vector3(ShelfCenterX, ShelfTopY - 0.02f, 0f), new Vector2(4.6f, 0.09f), Palette.GroundEdge);

            // Framing walls keep stray shots on screen.
            CreateSolid("WallL", new Vector3(-FieldHalfWidth + 0.3f, 0f, 0f), new Vector2(0.6f, FieldHalfHeight * 2f), Palette.Wall);
            CreateSolid("WallR", new Vector3(FieldHalfWidth - 0.3f, 0f, 0f), new Vector2(0.6f, FieldHalfHeight * 2f), Palette.Wall);
        }

        void BuildCannon()
        {
            var baseRenderer = CreateSlicedRenderer("CannonBase", new Vector3(CannonPivot.x, -3.92f, 0f), new Vector2(1.7f, 0.56f), Palette.CannonBase, SortingOrders.Cannon);
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
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            _scoreText = CreateHudChip(canvasGo.transform, "ScoreChip", new Vector2(0.05f, 0.876f), new Vector2(0.475f, 0.958f), Loc.Get("hud.scoreCaption"), TextAnchor.MiddleLeft, out _scoreCaption);
            _ammoText = CreateHudChip(canvasGo.transform, "AmmoChip", new Vector2(0.525f, 0.876f), new Vector2(0.95f, 0.958f), Loc.Get("hud.ammoCaption"), TextAnchor.MiddleRight, out _ammoCaption);

            _targetsText = UiFactory.Label(canvasGo.transform, "Targets", new Vector2(0.2f, 0.828f), new Vector2(0.8f, 0.868f), 30, TextAnchor.MiddleCenter, Palette.TextMuted);

            var comboLabel = UiFactory.Label(canvasGo.transform, "Combo", new Vector2(0.1f, 0.56f), new Vector2(0.9f, 0.68f), 84, TextAnchor.MiddleCenter, Palette.Accent, FontStyle.Bold);
            UiFactory.AddDropShadow(comboLabel, 5f);
            comboLabel.gameObject.SetActive(false);
            _combo.Bind(comboLabel);

            _ftueHint = FtueHint.Create(canvasGo.transform, Loc.Get("ftue.aim"));

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
                        ShowInterstitialThen(() => BeginRun(runMode));
                    }).GetComponentInChildren<Text>();
                    _failMenuLabel = UiFactory.Pill(card, Loc.Get("common.menu"), new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.17f), Palette.HudFill, Palette.TextPrimary, 30, ShowMenu).GetComponentInChildren<Text>();
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
                    _clearNextLabel = UiFactory.Pill(card, Loc.Get("clear.next"), new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.4f), Palette.AccentCool, Palette.TextOnAccent, 38, () =>
                    {
                        ShowInterstitialThen(() => BeginRun(RunMode.Endless));
                    }).GetComponentInChildren<Text>();
                    _clearMenuLabel = UiFactory.Pill(card, Loc.Get("common.menu"), new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.21f), Palette.HudFill, Palette.TextPrimary, 32, ShowMenu).GetComponentInChildren<Text>();
                });

            _menuPanel = BuildMenu(canvasGo.transform);
        }

        GameObject BuildMenu(Transform parent)
        {
            var root = UiFactory.NewUiObject("MenuPanel", parent);
            UiFactory.Anchor((RectTransform)root.transform, Vector2.zero, Vector2.one);

            UiFactory.Scrim(root.transform, "Scrim", Palette.Scrim);

            var card = UiFactory.Panel(root.transform, "Card", new Vector2(0.1f, 0.16f), new Vector2(0.9f, 0.84f), Palette.CardFill, 0.45f);
            UiFactory.Panel(card.transform, "Accent", new Vector2(0.35f, 0.955f), new Vector2(0.65f, 0.978f), Palette.Accent, 3f);

            _menuTitle = UiFactory.Label(card.transform, "Title", new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.94f), 60, TextAnchor.MiddleCenter, Palette.TextPrimary, FontStyle.Bold, Loc.Get("menu.title"));
            UiFactory.AddDropShadow(_menuTitle, 4f);

            _menuTagline = UiFactory.Label(card.transform, "Tagline", new Vector2(0.06f, 0.77f), new Vector2(0.94f, 0.83f), 24, TextAnchor.MiddleCenter, Palette.TextMuted, FontStyle.Normal, Loc.Get("menu.tagline"));

            _menuBestText = UiFactory.Label(card.transform, "Best", new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.75f), 30, TextAnchor.MiddleCenter, Palette.AccentCool, FontStyle.Bold);
            _menuDailyBestText = UiFactory.Label(card.transform, "DailyBest", new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.675f), 22, TextAnchor.MiddleCenter, Palette.TextMuted);

            _menuPlayLabel = UiFactory.Pill(card.transform, Loc.Get("menu.play"), new Vector2(0.1f, 0.44f), new Vector2(0.9f, 0.57f), Palette.Accent, Palette.TextOnAccent, 38, () =>
            {
                Sfx.Instance?.Ui();
                StartFromMenu(RunMode.Endless);
            }).GetComponentInChildren<Text>();
            _menuDailyLabel = UiFactory.Pill(card.transform, Loc.Get("menu.daily"), new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.42f), Palette.AccentCool, Palette.TextOnAccent, 32, () =>
            {
                Sfx.Instance?.Ui();
                StartFromMenu(RunMode.Daily);
            }).GetComponentInChildren<Text>();

            var sfxButton = UiFactory.Pill(card.transform, SettingsCaption(true), new Vector2(0.1f, 0.16f), new Vector2(0.48f, 0.26f), Palette.HudFill, Palette.TextPrimary, 22, ToggleSfx);
            _sfxToggleLabel = sfxButton.GetComponentInChildren<Text>();

            var hapticsButton = UiFactory.Pill(card.transform, SettingsCaption(false), new Vector2(0.52f, 0.16f), new Vector2(0.9f, 0.26f), Palette.HudFill, Palette.TextPrimary, 22, ToggleHaptics);
            _hapticsToggleLabel = hapticsButton.GetComponentInChildren<Text>();

            _langToggleLabel = UiFactory.Pill(card.transform, Loc.LanguageToggleCaption(), new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.13f), Palette.CannonHub, Palette.TextPrimary, 26, ToggleLanguage).GetComponentInChildren<Text>();

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

        static Text CreateHudChip(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string caption, TextAnchor alignment, out Text captionText)
        {
            var chip = UiFactory.Panel(parent, name, anchorMin, anchorMax, Palette.HudFill);
            captionText = UiFactory.Label(chip.transform, "Caption", new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.92f), 26, alignment, Palette.TextMuted, FontStyle.Normal, caption);
            var value = UiFactory.Label(chip.transform, "Value", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.52f), 50, alignment, Palette.TextPrimary, FontStyle.Bold);
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

        void SpawnStructure(int seed)
        {
            _layout = LevelLibrary.Pick(seed);
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

                RegisterTarget(ball.gameObject);
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

            RegisterTarget(piece.gameObject);
        }

        void RegisterTarget(GameObject target)
        {
            var destructible = target.AddComponent<DestructibleBlock>();
            destructible.Cleared += OnBlockCleared;
            _blocks.Add(destructible);
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
            _pendingHits = 0;
            _cannon.CanFire = false;
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
            var ball = CreateCircleRenderer("Projectile", spawn, 0.22f, Palette.Projectile, SortingOrders.Projectile);
            ball.transform.SetParent(_projectileRoot);
            ball.gameObject.AddComponent<CircleCollider2D>().radius = 0.5f;

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

            var projectile = ball.gameObject.AddComponent<Projectile>();
            var hitWatcher = ball.gameObject.AddComponent<ProjectileHitWatcher>();
            hitWatcher.Hit += OnProjectileHit;

            HitBurst.Play(_effectRoot, spawn, Palette.Accent, 6, 3.5f);
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.06f);
            }

            projectile.Launch(velocity, _ => StartCoroutine(ResolveShotRoutine()));
            RefreshHud();
        }

        void OnProjectileHit(DestructibleBlock block)
        {
            _pendingHits++;
            var chain = _pendingHits > 1;
            _loop.RegisterHit(block.ScoreValue, chain);
            Sfx.Instance?.Hit(chain);
            Haptics.Light();

            var tint = block.GetComponent<SpriteRenderer>();
            HitBurst.Play(_effectRoot, block.transform.position, tint != null ? tint.color : Palette.Accent, 8, 4.5f);
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.12f);
            }
        }

        IEnumerator ResolveShotRoutine()
        {
            // Allow physics cascade to settle.
            yield return new WaitForSeconds(1.1f);

            // Targets knocked off the shelf are still falling out of play; judging the run now
            // would call a clearable board a failure on the last shot.
            var deadline = Time.time + 2.5f;
            while (Time.time < deadline && AnyTargetSettling())
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

        void OnBlockCleared(DestructibleBlock block)
        {
            _loop.RegisterTargetCleared();
            RefreshHud();
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
                Sfx.Instance?.Cleared();
                Haptics.Light();
                ShowResult(_clearScoreText, _clearBestText);
                _clearPanel.SetActive(true);
                _ftueHint?.Hide();
            }
        }

        /// <summary>Fills a result card's score line and commits the run to the best-score store.</summary>
        void ShowResult(Text scoreText, Text bestText)
        {
            var score = _loop.Score;
            scoreText.text = Loc.Format("result.score", score);

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
            if (_failTitle != null) _failTitle.text = Loc.Get("fail.title");
            if (_clearTitle != null) _clearTitle.text = Loc.Get("clear.title");
            if (_failReviveLabel != null) _failReviveLabel.text = Loc.Get("fail.revive");
            if (_failRetryLabel != null) _failRetryLabel.text = Loc.Get("fail.retry");
            if (_failMenuLabel != null) _failMenuLabel.text = Loc.Get("common.menu");
            if (_clearNextLabel != null) _clearNextLabel.text = Loc.Get("clear.next");
            if (_clearMenuLabel != null) _clearMenuLabel.text = Loc.Get("common.menu");
            if (_menuTitle != null) _menuTitle.text = Loc.Get("menu.title");
            if (_menuTagline != null) _menuTagline.text = Loc.Get("menu.tagline");
            if (_menuPlayLabel != null) _menuPlayLabel.text = Loc.Get("menu.play");
            if (_menuDailyLabel != null) _menuDailyLabel.text = Loc.Get("menu.daily");
            if (_sfxToggleLabel != null) _sfxToggleLabel.text = SettingsCaption(true);
            if (_hapticsToggleLabel != null) _hapticsToggleLabel.text = SettingsCaption(false);
            if (_langToggleLabel != null) _langToggleLabel.text = Loc.LanguageToggleCaption();
            _ftueHint?.SetCopy(Loc.Get("ftue.aim"));

            if (_menuPanel != null && _menuPanel.activeSelf)
            {
                _menuBestText.text = Loc.Format("menu.best", Progress.EndlessBest);
                _menuDailyBestText.text = Loc.Format("menu.daily.best", _daily.LoadBestScore(_daily.TodaySeed));
            }

            RefreshHud();
        }

        void ShowMenu()
        {
            _cannon.CanFire = false;
            _shotInFlight = false;
            if (_failPanel != null) _failPanel.SetActive(false);
            if (_clearPanel != null) _clearPanel.SetActive(false);
            _ftueHint?.Hide();
            ClearBoard();

            _menuBestText.text = Loc.Format("menu.best", Progress.EndlessBest);
            _menuDailyBestText.text = Loc.Format("menu.daily.best", _daily.LoadBestScore(_daily.TodaySeed));
            if (_sfxToggleLabel != null) _sfxToggleLabel.text = SettingsCaption(true);
            if (_hapticsToggleLabel != null) _hapticsToggleLabel.text = SettingsCaption(false);
            _menuPanel.SetActive(true);
        }

        void StartFromMenu(RunMode mode)
        {
            _menuPanel.SetActive(false);
            BeginRun(mode);
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
                    _cannon.CanFire = true;
                    RefreshHud();
                }
            });
        }

        void RefreshHud()
        {
            if (_scoreText != null)
            {
                _scoreText.text = _loop.Score.ToString();
            }

            if (_ammoText != null)
            {
                _ammoText.text = _loop.Ammo.ToString();
            }

            if (_targetsText != null)
            {
                _targetsText.text = Loc.Format("hud.targets", Loc.LevelName(_layout.Name), _loop.TargetsRemaining);
            }
        }

        GameObject CreateSolid(string name, Vector3 position, Vector2 size, Color color)
        {
            var renderer = CreateSlicedRenderer(name, position, size, color, SortingOrders.Structure);
            renderer.transform.SetParent(_worldRoot);
            var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.sharedMaterial = PieceMaterial;
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
            renderer.sprite = Shapes.RoundedRect;
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
    /// Reports projectile collisions against destructible blocks for combo scoring.
    /// </summary>
    public sealed class ProjectileHitWatcher : MonoBehaviour
    {
        readonly HashSet<DestructibleBlock> _hitBlocks = new HashSet<DestructibleBlock>();
        public event System.Action<DestructibleBlock> Hit;

        void OnCollisionEnter2D(Collision2D collision)
        {
            var block = collision.collider.GetComponent<DestructibleBlock>();
            if (block == null || block.IsCleared)
            {
                return;
            }

            if (!_hitBlocks.Add(block))
            {
                return;
            }

            Hit?.Invoke(block);
        }
    }
}
