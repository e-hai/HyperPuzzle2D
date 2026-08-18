using System;
using System.Collections.Generic;
using HyperPuzzle2D.Art;
using HyperPuzzle2D.Feedback;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HyperPuzzle2D.Paper
{
    public enum PaperAimKind
    {
        Empty,
        Face,
        Clothes,
    }

    /// <summary>
    /// The play layer: backdrop, muzzle, reticle and the clothes jigsaw over a face/limb underlay.
    /// It owns aiming and hit-scan only, and reports each resolved shot through
    /// <see cref="ShotResolved"/> so scoring, progression and screens stay in <see cref="PaperDirector"/>.
    /// </summary>
    public sealed class PaperTargetField : MonoBehaviour
    {
        /// <summary>Every sheet is drawn this wide in world units; the camera fits it to any aspect.</summary>
        const float TargetWorldWidth = 4.2f;

        /// <summary>Fraction of the visible width the sheet fills, leaving thin side margins.</summary>
        const float WidthFill = 0.9f;
        const float MinOrthoSize = 4.0f;

        static readonly Vector3 TargetCentre = new Vector3(0f, 0.9f, 0f);
        const float MuzzleY = -3.4f;

        /// <summary>Reticle travel per world unit of finger travel: small drags cover the sheet.</summary>
        const float AimSensitivity = 1.6f;

        /// <summary>Assist range. Small on purpose: the crosshair snaps onto what it grabs, so a
        /// wide magnet would make the crosshair lie about the target.</summary>
        const float MagnetRadius = 0.42f;

        /// <summary>Reticle stays this far above the muzzle so the shot always reads as an arc.</summary>
        const float MinAimHeight = -1.6f;

        /// <summary>Slack around the sheet so edge pieces stay reachable.</summary>
        const float AimMargin = 0.35f;

        /// <summary>Bottom of the HUD strip in viewport space; the reticle stops below it.</summary>
        const float HudFloorViewport = 0.85f;

        /// <summary>Raised once per shot with the zone that was hit, or null for a miss.</summary>
        public event Action<PaperZone, Vector3> ShotResolved;

        /// <summary>Raised while aiming so the HUD can say "aim at clothes" over face/limbs.</summary>
        public event Action<PaperAimKind> AimKindChanged;

        Camera _camera;
        Transform _worldRoot;
        Transform _effectRoot;
        Transform _targetRoot;
        SpriteRenderer _grain;

        readonly List<PaperZone> _zones = new List<PaperZone>();
        readonly List<Texture2D> _zoneTextures = new List<Texture2D>();
        Texture2D _sheet;
        Texture2D _baseSheet;
        Texture2D _underlayTexture;
        bool[] _clothesMask;

        byte[] _region;
        int _regionWidth;
        int _regionHeight;
        PaperAimKind _lastAimKind = PaperAimKind.Empty;

        Transform _reticle;
        Transform _thumbDot;
        TextMesh _aimValue;
        Transform _aimChip;
        readonly List<SpriteRenderer> _reticleParts = new List<SpriteRenderer>();
        readonly List<Color> _reticleColors = new List<Color>();
        LineRenderer _aimLine;
        bool _aiming;
        Vector3 _reticleWorld;
        Vector3 _dragOrigin;
        Vector3 _aimOrigin;
        Vector3 _muzzleWorld;
        PaperZone _lockedZone;

        float _lastAspect = -1f;
        float _imageHalfWidth = 2.1f;
        float _imageHalfHeight = 3.1f;

        public bool InputEnabled { get; set; }
        public Transform EffectRoot => _effectRoot;
        public IReadOnlyList<PaperZone> Zones => _zones;

        void Awake()
        {
            SetupCamera();
            if (Sfx.Instance == null)
            {
                new GameObject("Sfx").AddComponent<Sfx>();
            }

            _worldRoot = new GameObject("PaperWorld").transform;
            _effectRoot = new GameObject("PaperEffects").transform;

            BuildBackdrop();
            BuildMuzzle();
            BuildReticle();
            SetWorldVisible(false);
        }

        void SetupCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _camera = go.AddComponent<Camera>();
            }

            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Palette.BackdropTop;
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            if (_camera.GetComponent<AudioListener>() == null)
            {
                _camera.gameObject.AddComponent<AudioListener>();
            }

            if (_camera.GetComponent<CameraShake>() == null)
            {
                _camera.gameObject.AddComponent<CameraShake>();
            }
        }

        void FitCamera()
        {
            var aspect = _camera.aspect > 0.01f ? _camera.aspect : 0.46f;
            if (Mathf.Approximately(aspect, _lastAspect))
            {
                return;
            }

            _lastAspect = aspect;
            var visibleHalfWidth = (TargetWorldWidth * 0.5f) / WidthFill;
            _camera.orthographicSize = Mathf.Max(MinOrthoSize, visibleHalfWidth / aspect);

            var h = _camera.orthographicSize * 2f;
            if (_grain != null)
            {
                _grain.size = new Vector2(h * aspect, h);
            }
        }

        void BuildBackdrop()
        {
            FitCamera();
            var h = _camera.orthographicSize * 2f;
            var w = h * (_camera.aspect > 0.01f ? _camera.aspect : 0.46f);

            var gradient = new GameObject("Gradient");
            gradient.transform.SetParent(_worldRoot, false);
            gradient.transform.localScale = new Vector3(w * 2f, h * 2f, 1f);
            var gr = gradient.AddComponent<SpriteRenderer>();
            gr.sprite = Shapes.VerticalGradient(Palette.BackdropBottom, Palette.BackdropTop);
            gr.sortingOrder = SortingOrders.Backdrop;

            var grainGo = new GameObject("PaperGrain");
            grainGo.transform.SetParent(_worldRoot, false);
            _grain = grainGo.AddComponent<SpriteRenderer>();
            _grain.sprite = Shapes.PaperFiber;
            _grain.drawMode = SpriteDrawMode.Tiled;
            _grain.tileMode = SpriteTileMode.Continuous;
            _grain.size = new Vector2(w * 2f, h * 2f);
            _grain.color = new Color(Palette.Shadow.r, Palette.Shadow.g, Palette.Shadow.b, 0.14f);
            _grain.sortingOrder = SortingOrders.BackdropGrain;

            // Sunlight pooled behind the target so the illustration reads as the single subject.
            var glow = new GameObject("Glow");
            glow.transform.SetParent(_worldRoot, false);
            glow.transform.position = TargetCentre + Vector3.up * 0.4f;
            glow.transform.localScale = Vector3.one * 9f;
            var gg = glow.AddComponent<SpriteRenderer>();
            gg.sprite = Shapes.Glow;
            gg.color = new Color(Palette.BackdropGlow.r, Palette.BackdropGlow.g, Palette.BackdropGlow.b, 0.35f);
            gg.sortingOrder = SortingOrders.BackdropGlow;
        }

        /// <summary>
        /// A slim launcher: with aiming moved onto the whole screen the muzzle is only a
        /// visual anchor for the shot line, so it stays small and out of the thumb's way.
        /// </summary>
        void BuildMuzzle()
        {
            _muzzleWorld = new Vector3(0f, MuzzleY, 0f);

            AddMuzzlePart("Stand", Shapes.RoundedRect, new Vector3(0f, MuzzleY - 0.72f, 0f),
                new Vector3(1.9f, 0.26f, 1f), Palette.CannonBase, SortingOrders.Cannon);
            AddMuzzlePart("Tube", Shapes.RoundedRect, new Vector3(0f, MuzzleY - 0.36f, 0f),
                new Vector3(0.52f, 0.78f, 1f), Palette.CannonBarrel, SortingOrders.Cannon + 1);
            AddMuzzlePart("Bore", Shapes.Circle, _muzzleWorld,
                new Vector3(0.3f, 0.16f, 1f), Palette.CannonBase, SortingOrders.Cannon + 2);
        }

        void AddMuzzlePart(string name, Sprite sprite, Vector3 position, Vector3 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
        }

        void BuildReticle()
        {
            var root = new GameObject("Reticle");
            root.transform.SetParent(_worldRoot, false);
            _reticle = root.transform;
            _reticle.position = TargetCentre;

            // Open crosshair: the piece under the reticle has to stay readable, so nothing
            // opaque sits over it.
            AddReticleRing("Halo", 0.86f, new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, 0.12f), SortingOrders.AimGuide);
            AddReticleRing("Dot", 0.12f, Palette.Accent, SortingOrders.AimGuide + 2);
            AddReticleTick("TickUp", new Vector3(0f, 0.25f, 0f), new Vector3(0.045f, 0.14f, 1f));
            AddReticleTick("TickDown", new Vector3(0f, -0.25f, 0f), new Vector3(0.045f, 0.14f, 1f));
            AddReticleTick("TickLeft", new Vector3(-0.25f, 0f, 0f), new Vector3(0.14f, 0.045f, 1f));
            AddReticleTick("TickRight", new Vector3(0.25f, 0f, 0f), new Vector3(0.14f, 0.045f, 1f));
            SetReticleActive(false);

            // Ring under the thumb: confirms the touch was picked up without covering the target.
            var dotGo = new GameObject("ThumbDot");
            dotGo.transform.SetParent(_worldRoot, false);
            _thumbDot = dotGo.transform;
            _thumbDot.position = _muzzleWorld;
            _thumbDot.localScale = Vector3.one * 0.7f;
            var dot = dotGo.AddComponent<SpriteRenderer>();
            dot.sprite = Shapes.Circle;
            dot.color = new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, 0.3f);
            dot.sortingOrder = SortingOrders.AimGuide + 3;
            dotGo.SetActive(false);

            // Value of the piece under the crosshair: the reason to aim for one piece over another.
            var chipGo = new GameObject("AimValueChip");
            chipGo.transform.SetParent(_worldRoot, false);
            _aimChip = chipGo.transform;
            var chip = chipGo.AddComponent<SpriteRenderer>();
            chip.sprite = Shapes.RoundedRect;
            chip.color = new Color(Palette.HudPanel.r, Palette.HudPanel.g, Palette.HudPanel.b, 0.94f);
            chip.sortingOrder = SortingOrders.AimGuide + 4;
            chipGo.SetActive(false);

            var valueGo = new GameObject("AimValue");
            valueGo.transform.SetParent(_worldRoot, false);
            _aimValue = valueGo.AddComponent<TextMesh>();
            _aimValue.font = UiFactory.Body;
            _aimValue.fontSize = 64;
            _aimValue.characterSize = 0.032f;
            _aimValue.anchor = TextAnchor.LowerCenter;
            _aimValue.alignment = TextAlignment.Center;
            _aimValue.fontStyle = FontStyle.Bold;
            var valueRenderer = valueGo.GetComponent<MeshRenderer>();
            valueRenderer.sortingOrder = SortingOrders.AimGuide + 5;
            if (_aimValue.font != null)
            {
                valueRenderer.sharedMaterial = _aimValue.font.material;
            }

            valueGo.SetActive(false);

            _aimLine = MakeLine("AimLine", 0.075f,
                new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, 0f),
                new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, 0.6f));
        }

        void HideAimValue()
        {
            if (_aimValue != null)
            {
                _aimValue.gameObject.SetActive(false);
            }

            if (_aimChip != null)
            {
                _aimChip.gameObject.SetActive(false);
            }
        }

        /// <summary>Idle reticle stays on screen but dimmed, so the next shot's aim is always visible.</summary>
        void SetReticleActive(bool active)
        {
            for (var i = 0; i < _reticleParts.Count; i++)
            {
                var c = _reticleColors[i];
                _reticleParts[i].color = active ? c : new Color(c.r, c.g, c.b, c.a * 0.45f);
            }

            _reticle.localScale = Vector3.one * (active ? 1f : 0.8f);
        }

        LineRenderer MakeLine(string name, float width, Color start, Color end)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_worldRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = start;
            line.endColor = end;
            line.sortingOrder = SortingOrders.AimGuide;
            line.enabled = false;
            return line;
        }

        void AddReticleRing(string name, float scale, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_reticle, false);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Shapes.Circle;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            _reticleParts.Add(sr);
            _reticleColors.Add(color);
        }

        void AddReticleTick(string name, Vector3 localPosition, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_reticle, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Shapes.RoundedRect;
            sr.color = Palette.Accent;
            sr.sortingOrder = SortingOrders.AimGuide + 2;
            _reticleParts.Add(sr);
            _reticleColors.Add(sr.color);
        }

        public void SetWorldVisible(bool visible)
        {
            if (_worldRoot != null)
            {
                _worldRoot.gameObject.SetActive(visible);
            }

            if (_effectRoot != null)
            {
                _effectRoot.gameObject.SetActive(visible);
            }
        }

        /// <summary>Shows the permanent base and cuts the current outfit into a jigsaw.</summary>
        public void LoadLevel(PaperLevel level)
        {
            ClearZones();

            _baseSheet = Resources.Load<Texture2D>(level.BasePath);
            _sheet = Resources.Load<Texture2D>(level.OutfitPath);
            if (_sheet == null)
            {
                // Legacy fallback while layers are being baked.
                _sheet = Resources.Load<Texture2D>("Art/Paper" + level.Character);
                _baseSheet = _sheet;
            }

            if (_sheet == null)
            {
                Debug.LogError("PaperTargetField: missing outfit " + level.OutfitPath);
                return;
            }

            if (_baseSheet == null)
            {
                _baseSheet = _sheet;
            }

            _targetRoot = new GameObject("PaperTarget").transform;
            _targetRoot.SetParent(_worldRoot, false);
            _targetRoot.position = TargetCentre;

            var ppu = _sheet.width / TargetWorldWidth;
            var imageWorldWidth = _sheet.width / ppu;
            var imageWorldHeight = _sheet.height / ppu;
            _imageHalfWidth = imageWorldWidth * 0.5f;
            _imageHalfHeight = imageWorldHeight * 0.5f;

            var authoredMask = Resources.Load<Texture2D>(level.MaskPath);
            _clothesMask = authoredMask != null
                ? PaperJigsawCutter.MaskFromTexture(authoredMask, _sheet.width, _sheet.height)
                : PaperJigsawCutter.MaskFromTexture(_sheet, _sheet.width, _sheet.height);

            BuildBaseLayer(ppu);
            var cut = PaperJigsawCutter.Cut(
                _clothesMask, _sheet.width, _sheet.height, level.Cols, level.Rows, 280);
            _region = cut.Region;
            _regionWidth = cut.Width;
            _regionHeight = cut.Height;

            BuildJigsawPieces(cut, ppu, imageWorldWidth, imageWorldHeight);

            _reticleWorld = TargetCentre;
            _reticle.position = _reticleWorld;
            _aimLine.enabled = false;
            SetReticleActive(false);
            if (_thumbDot != null)
            {
                _thumbDot.gameObject.SetActive(false);
            }

            HideAimValue();
            _aiming = false;
            _lockedZone = null;
            _lastAimKind = PaperAimKind.Empty;
            AimKindChanged?.Invoke(PaperAimKind.Empty);
        }

        /// <summary>Permanent figure: face, limbs, and plain underclothes. Never torn.</summary>
        void BuildBaseLayer(float ppu)
        {
            var w = _baseSheet.width;
            var h = _baseSheet.height;
            var src = _baseSheet.GetPixels32();

            _underlayTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _underlayTexture.SetPixels32(src);
            _underlayTexture.Apply(false);

            var sprite = Sprite.Create(
                _underlayTexture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);

            var go = new GameObject("Base");
            go.transform.SetParent(_targetRoot, false);
            go.transform.localPosition = Vector3.zero;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortingOrders.Underlay;
        }

        void BuildJigsawPieces(
            PaperJigsawCutter.Result cut, float ppu, float imageWorldWidth, float imageWorldHeight)
        {
            var w = _sheet.width;
            var h = _sheet.height;
            var src = _sheet.GetPixels32();
            var count = cut.PieceCount;
            var totalClothes = 0;
            for (var i = 0; i < cut.PixelCounts.Length; i++)
            {
                totalClothes += cut.PixelCounts[i];
            }

            var minX = new int[count];
            var minY = new int[count];
            var maxX = new int[count];
            var maxY = new int[count];
            for (var i = 0; i < count; i++)
            {
                minX[i] = w;
                minY[i] = h;
                maxX[i] = -1;
                maxY[i] = -1;
            }

            int RegionAt(int x, int y)
            {
                var cx = Mathf.Clamp(x * _regionWidth / w, 0, _regionWidth - 1);
                var cy = Mathf.Clamp(y * _regionHeight / h, 0, _regionHeight - 1);
                return _region[cy * _regionWidth + cx];
            }

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (!_clothesMask[y * w + x])
                    {
                        continue;
                    }

                    var id = RegionAt(x, y);
                    if (id >= count)
                    {
                        continue;
                    }

                    if (x < minX[id]) minX[id] = x;
                    if (x > maxX[id]) maxX[id] = x;
                    if (y < minY[id]) minY[id] = y;
                    if (y > maxY[id]) maxY[id] = y;
                }
            }

            var buffers = new Color32[count][];
            var bw = new int[count];
            var bh = new int[count];
            for (var i = 0; i < count; i++)
            {
                if (maxX[i] < minX[i] || cut.PixelCounts[i] <= 0)
                {
                    continue;
                }

                bw[i] = maxX[i] - minX[i] + 1;
                bh[i] = maxY[i] - minY[i] + 1;
                buffers[i] = new Color32[bw[i] * bh[i]];
            }

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (!_clothesMask[y * w + x])
                    {
                        continue;
                    }

                    var id = RegionAt(x, y);
                    if (id >= count || buffers[id] == null)
                    {
                        continue;
                    }

                    var color = src[y * w + x];
                    // Only darken borders between two cloth pieces — never against empty space —
                    // so the silhouette stays soft and the grid reads on the garment alone.
                    if (IsInteriorSeam(x, y, id, w, h, RegionAt))
                    {
                        color = new Color32(
                            (byte)(color.r * 0.48f),
                            (byte)(color.g * 0.48f),
                            (byte)(color.b * 0.48f),
                            color.a);
                    }

                    var bx = x - minX[id];
                    var by = y - minY[id];
                    buffers[id][by * bw[id] + bx] = color;
                }
            }

            for (var i = 0; i < count; i++)
            {
                if (buffers[i] == null)
                {
                    continue;
                }

                var tex = new Texture2D(bw[i], bh[i], TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                tex.SetPixels32(buffers[i]);
                tex.Apply(false);
                _zoneTextures.Add(tex);

                var sprite = Sprite.Create(
                    tex, new Rect(0, 0, bw[i], bh[i]), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);

                var centreX = (minX[i] + maxX[i] + 1) * 0.5f / w;
                var centreY = (minY[i] + maxY[i] + 1) * 0.5f / h;
                var score = PaperLevel.ScoreForPiece(cut.PixelCounts[i], totalClothes, centreX, centreY);
                var tier = PaperLevel.TierForScore(score);

                var go = new GameObject("Piece_" + i);
                go.transform.SetParent(_targetRoot, false);
                go.transform.localPosition = new Vector3(
                    (centreX - 0.5f) * imageWorldWidth,
                    (centreY - 0.5f) * imageWorldHeight,
                    0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                // One order above Block so the highlight rim can slot in between piece and base.
                sr.sortingOrder = SortingOrders.Block + 1;

                var zone = go.AddComponent<PaperZone>();
                zone.Configure(i, score, "衣片", "CLOTH", tier, sr);
                _zones.Add(zone);
            }
        }

        static bool IsInteriorSeam(int x, int y, int id, int w, int h, System.Func<int, int, int> regionAt)
        {
            return IsOtherPiece(x - 1, y, id, w, h, regionAt)
                   || IsOtherPiece(x + 1, y, id, w, h, regionAt)
                   || IsOtherPiece(x, y - 1, id, w, h, regionAt)
                   || IsOtherPiece(x, y + 1, id, w, h, regionAt);
        }

        static bool IsOtherPiece(int x, int y, int id, int w, int h, System.Func<int, int, int> regionAt)
        {
            if (x < 0 || y < 0 || x >= w || y >= h)
            {
                return false;
            }

            var other = regionAt(x, y);
            return other != id && other != PaperJigsawCutter.Empty;
        }

        void ClearZones()
        {
            for (var i = 0; i < _zones.Count; i++)
            {
                if (_zones[i] != null)
                {
                    Destroy(_zones[i].gameObject);
                }
            }

            _zones.Clear();

            for (var i = 0; i < _zoneTextures.Count; i++)
            {
                if (_zoneTextures[i] != null)
                {
                    Destroy(_zoneTextures[i]);
                }
            }

            _zoneTextures.Clear();

            if (_underlayTexture != null)
            {
                Destroy(_underlayTexture);
                _underlayTexture = null;
            }

            _region = null;
            _clothesMask = null;

            if (_targetRoot != null)
            {
                Destroy(_targetRoot.gameObject);
                _targetRoot = null;
            }
        }

        void Update()
        {
            FitCamera();

            if (!InputEnabled)
            {
                if (_aiming)
                {
                    EndAim();
                }

                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0) && !IsPointerOverUi())
            {
                // Relative aiming: the press works anywhere, the reticle never jumps to the thumb.
                _aiming = true;
                _dragOrigin = PointerWorld();
                _aimOrigin = _reticleWorld;
                _thumbDot.gameObject.SetActive(true);
                SetReticleActive(true);
                UpdateAim(_dragOrigin);
            }

            if (_aiming)
            {
                UpdateAim(PointerWorld());
            }

            if (UnityEngine.Input.GetMouseButtonUp(0) && _aiming)
            {
                var zone = _lockedZone;
                var impact = _reticleWorld;
                EndAim();
                Fire(zone, impact);
            }
        }

        void EndAim()
        {
            _aiming = false;
            _lockedZone = null;
            _aimLine.enabled = false;
            if (_thumbDot != null)
            {
                _thumbDot.gameObject.SetActive(false);
            }

            HideAimValue();
            SetReticleActive(false);
            ClearHighlights();
            if (_lastAimKind != PaperAimKind.Empty)
            {
                _lastAimKind = PaperAimKind.Empty;
                AimKindChanged?.Invoke(PaperAimKind.Empty);
            }
        }

        /// <summary>
        /// Trackpad aiming: the reticle moves by the finger delta since the press, scaled up so the
        /// whole sheet is reachable from the bottom of the screen. Aim carries over between shots,
        /// so hitting a neighbouring piece is a nudge rather than a whole new gesture.
        /// </summary>
        void UpdateAim(Vector3 finger)
        {
            _thumbDot.position = finger;

            var target = _aimOrigin + (finger - _dragOrigin) * AimSensitivity;
            _reticleWorld = ClampAim(target);

            var under = ZoneUnder(_reticleWorld);
            _lockedZone = under ?? MagnetZone(_reticleWorld);
            if (under == null && _lockedZone != null)
            {
                // Grabbed by assist rather than aimed at: pull the crosshair onto the piece so the
                // player sees exactly what the shot will take.
                _reticleWorld = Vector3.Lerp(_reticleWorld, _lockedZone.transform.position, 0.85f);
            }

            _reticle.position = _reticleWorld;

            // Line and reticle read hot when a piece is locked, cold when the shot would waste ammo.
            var locked = _lockedZone != null;
            var tint = locked ? Palette.Accent : Palette.TextMuted;
            _aimLine.enabled = true;
            _aimLine.startColor = new Color(tint.r, tint.g, tint.b, 0f);
            _aimLine.endColor = new Color(tint.r, tint.g, tint.b, locked ? 0.85f : 0.45f);
            _aimLine.SetPosition(0, _muzzleWorld);
            _aimLine.SetPosition(1, _reticleWorld);
            _reticle.localScale = Vector3.one * (locked ? 1.15f : 0.95f);

            _aimValue.gameObject.SetActive(locked);
            _aimChip.gameObject.SetActive(locked);
            if (locked)
            {
                _aimValue.text = "+" + _lockedZone.Score;
                _aimValue.color = Palette.TextPrimary;

                // Sits diagonally off the crosshair so it never covers the piece being aimed at,
                // and flips side or height near the screen edges.
                var width = 0.14f + _aimValue.text.Length * 0.085f;
                var hudFloor = _camera.ViewportToWorldPoint(new Vector3(0.5f, HudFloorViewport, 0f)).y;
                var right = _camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, 0f)).x;
                var side = _reticleWorld.x + 0.4f + width < right - 0.15f ? 1f : -1f;
                var lift = _reticleWorld.y + 0.42f < hudFloor - 0.2f ? 0.28f : -0.5f;
                var anchor = _reticleWorld + new Vector3(side * (0.34f + width * 0.5f), lift, 0f);

                _aimValue.transform.position = anchor;
                _aimChip.position = anchor + Vector3.up * 0.11f;
                _aimChip.localScale = new Vector3(width, 0.26f, 1f);
            }

            HighlightUnderReticle();
        }

        Vector3 ClampAim(Vector3 point)
        {
            var x = Mathf.Clamp(
                point.x,
                TargetCentre.x - _imageHalfWidth - AimMargin,
                TargetCentre.x + _imageHalfWidth + AimMargin);

            // Never let the reticle slide under the HUD strip, where it would be invisible.
            var hudFloor = _camera.ViewportToWorldPoint(new Vector3(0.5f, HudFloorViewport, 0f)).y;
            var top = Mathf.Min(TargetCentre.y + _imageHalfHeight, hudFloor);
            var bottom = Mathf.Max(MinAimHeight, TargetCentre.y - _imageHalfHeight - AimMargin);
            var y = Mathf.Clamp(point.y, bottom, Mathf.Max(bottom, top));
            return new Vector3(x, y, 0f);
        }

        PaperZone MagnetZone(Vector3 point)
        {
            PaperZone best = null;
            var bestDist = MagnetRadius * MagnetRadius;
            for (var i = 0; i < _zones.Count; i++)
            {
                var zone = _zones[i];
                if (zone == null || zone.IsBroken)
                {
                    continue;
                }

                var d = (zone.transform.position - point).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = zone;
                }
            }

            return best;
        }

        void ClearHighlights()
        {
            for (var i = 0; i < _zones.Count; i++)
            {
                if (_zones[i] != null)
                {
                    _zones[i].SetHighlighted(false);
                }
            }
        }

        static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        Vector3 PointerWorld()
        {
            var screen = UnityEngine.Input.mousePosition;
            var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(_camera.transform.position.z)));
            world.z = 0f;
            return world;
        }

        void HighlightUnderReticle()
        {
            var zone = _lockedZone != null ? _lockedZone : ZoneUnder(_reticleWorld);
            for (var i = 0; i < _zones.Count; i++)
            {
                _zones[i].SetHighlighted(_zones[i] == zone);
            }

            var kind = !_aiming ? PaperAimKind.Empty : AimKindAt(_reticleWorld, zone);
            if (kind != _lastAimKind)
            {
                _lastAimKind = kind;
                AimKindChanged?.Invoke(kind);
            }
        }

        PaperAimKind AimKindAt(Vector3 point, PaperZone zone)
        {
            if (zone != null)
            {
                return PaperAimKind.Clothes;
            }

            var u = (point.x - TargetCentre.x) / (_imageHalfWidth * 2f) + 0.5f;
            var v = (point.y - TargetCentre.y) / (_imageHalfHeight * 2f) + 0.5f;
            if (u < 0f || u > 1f || v < 0f || v > 1f || _baseSheet == null)
            {
                return PaperAimKind.Empty;
            }

            var px = Mathf.Clamp((int)(u * _baseSheet.width), 0, _baseSheet.width - 1);
            var py = Mathf.Clamp((int)(v * _baseSheet.height), 0, _baseSheet.height - 1);
            if (_baseSheet.GetPixel(px, py).a <= 0.5f)
            {
                return PaperAimKind.Empty;
            }

            return PaperAimKind.Face;
        }

        PaperZone ZoneUnder(Vector3 point)
        {
            if (_region == null)
            {
                return null;
            }

            var u = (point.x - TargetCentre.x) / (_imageHalfWidth * 2f) + 0.5f;
            var v = (point.y - TargetCentre.y) / (_imageHalfHeight * 2f) + 0.5f;
            if (u < 0f || u > 1f || v < 0f || v > 1f)
            {
                return null;
            }

            var cx = Mathf.Clamp((int)(u * _regionWidth), 0, _regionWidth - 1);
            var cy = Mathf.Clamp((int)(v * _regionHeight), 0, _regionHeight - 1);
            var id = _region[cy * _regionWidth + cx];
            if (id == PaperJigsawCutter.Empty)
            {
                return null;
            }

            for (var i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                if (z != null && !z.IsBroken && z.Id == id)
                {
                    return z;
                }
            }

            return null;
        }

        void Fire(PaperZone zone, Vector3 impact)
        {
            Sfx.Instance?.Fire();
            HitBurst.PlayRing(_effectRoot, _muzzleWorld, Palette.CannonBarrel, 0.8f);
            StartCoroutine(Tracer(_muzzleWorld, impact));

            // Prefer the magnet-locked piece; fall back to whatever sits under the impact point.
            zone ??= ZoneUnder(impact);
            if (zone != null)
            {
                var dir = (zone.transform.position - _muzzleWorld).normalized;
                zone.Break(dir);

                var strength = Mathf.InverseLerp(20f, 500f, zone.Score);
                HitBurst.PlayDirectional(_effectRoot, zone.transform.position, zone.Tier, 16, 5.5f + strength * 4f, dir, 120f);
                HitBurst.PlayRing(_effectRoot, zone.transform.position, zone.Tier, 1.6f + strength);
                CameraShake.Instance?.Shake(0.12f + strength * 0.22f);
                Sfx.Instance?.Break(1 + Mathf.RoundToInt(strength * 3f));
                if (strength > 0.6f)
                {
                    Haptics.Heavy();
                }
                else
                {
                    Haptics.Light();
                }
            }
            else
            {
                // Misses still land somewhere: mark the spot so the next nudge is informed.
                HitBurst.PlayRing(_effectRoot, impact, Palette.TextMuted, 0.7f);
                CameraShake.Instance?.Shake(0.05f);
                Sfx.Instance?.Hit(0.3f);
            }

            ShotResolved?.Invoke(zone, impact);
        }

        /// <summary>Short streak from muzzle to impact so every shot reads as travelling.</summary>
        System.Collections.IEnumerator Tracer(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(_effectRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.widthMultiplier = 0.09f;
            line.numCapVertices = 4;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = SortingOrders.AimGuide + 6;
            line.SetPosition(0, from);
            line.SetPosition(1, to);

            const float life = 0.14f;
            for (var t = 0f; t < life; t += Time.deltaTime)
            {
                var a = 1f - t / life;
                var head = new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, a);
                var tail = new Color(Palette.Accent.r, Palette.Accent.g, Palette.Accent.b, a * 0.1f);
                line.startColor = tail;
                line.endColor = head;
                yield return null;
            }

            Destroy(go);
        }
    }
}
