using System;
using Battle;
using Battle.Attacks;
using HarmonyLib;
using PeglinBalatroScore.Core;
using PeglinUtils.Locators;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeglinBalatroScore
{
    /// <summary>
    /// The Balatro-style "pegs x damage" score that sits on top of the enemy scroll in battle. Resets to 0 x 0 when a
    /// shot is fired, counts up as pegs are hit, then shows the game's real total for a few seconds.
    /// </summary>
    internal class ScoreHud : MonoBehaviour
    {
        private const float BoxHeight = 58f;

        /// <summary>Space for the "x" between the two boxes.</summary>
        private const float TimesGap = 48f;

        /// <summary>Like the numbers, the "x" sits high in its line and is nudged down to line up with them.</summary>
        private const float TimesDrop = -6f;

        // Where the widget lives, as fractions of the game's 16:9 view (Peglin letterboxes to 16:9 at any resolution).
        // It fills the gap right of the pegboard's brick wall, centred between the bottom of the forest strip and the
        // top of the enemy scroll.
        private const float ForestBottom = 0.312f;
        private const float ScrollTop = 0.413f;
        private const float WallRight = 0.8075f;
        private const float SideMargin = 0.01f;

        /// <summary>Space between the bottom of the score and a boss health bar pushed below it, as a share of screen height.</summary>
        private const float BossBarGap = 0.012f;

        private const float ShiftSpeed = 10f;

        /// <summary>How much of the gap between the forest and the scroll the boxes take up.</summary>
        private const float BandFill = 0.56f;

        private const float BurstSeconds = 0.8f;
        private const float BurstExtraIntensity = 0.9f;
        private const float IntensityRiseSpeed = 3f;
        private const float IntensityFallSpeed = 1.2f;
        private const float FadeSpeed = 6f;

        /// <summary>How long the total takes to fade out when its time is up, in real seconds.</summary>
        private const float ResultFadeSeconds = 0.3f;

        /// <summary>Shake strength at its strongest (1 is the shake at the boss's HP).</summary>
        private const float MaxShake = 1.6f;

        private static ScoreHud _instance;

        private readonly ScoreTracker _tracker = new ScoreTracker();

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private ScoreTooltip _tooltip;
        private TooltipManager _gameTooltips;
        private GameObject _bossBars;
        private readonly System.Collections.Generic.List<ScreenShift> _shifts = new System.Collections.Generic.List<ScreenShift>();
        private float _nextShiftSearch;
        private float _shift;
        private bool _loggedShift;
        private string _loggedFlameNote;
        private float _resultFade;
        private readonly System.Collections.Generic.List<Canvas> _tooltipCanvases = new System.Collections.Generic.List<Canvas>();
        private float _nextTooltipSearch;
        private bool _loggedSorting;
        private double _flameStart = FlameCurve.StartRatio;
        private RectTransform _root;
        private ScoreBox _pegs;
        private ScoreBox _damage;
        private ScoreBox _result;
        private TextMeshProUGUI _times;
        private OverkillStamp _overkill;

        private BattleController _battle;
        private EnemyInfoManager _scroll;
        private float _nextScrollSearch;
        private bool _fontApplied;
        private bool _warnedRead;
        private float _layoutWidth = -1f;
        private RectTransform _timesRect;

        private ShotDamage _lastRead;
        private bool _hasRead;
        private long _target;
        private long _previousTotal;
        private double _intensity;
        private float _burst;
        private float _alpha;
        private ScorePhase _shownPhase = ScorePhase.Ready;
        private bool _shownHeal;

        public static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }
            // Hidden and unsaved like PeglinQuickRestart's host: a plain object made at load time doesn't survive here.
            GameObject host = new GameObject("PeglinBalatroScore") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<ScoreHud>();
        }

        /// <summary>The attack went out: use its exact total (a few relics only apply their multiplier now).</summary>
        internal static void AttackFired(AttackManager attackManager, int pegTally, float damageMultiplier, long damageBonus, int critCount)
        {
            if (_instance == null || _instance._tracker.Phase != ScorePhase.Result)
            {
                return;
            }
            long total = attackManager.GetCurrentDamage(pegTally, damageMultiplier, damageBonus, critCount);
            _instance._tracker.RefineResult(total);
        }

        private void Awake()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            // Overlay until a game camera is found; then SyncCanvas puts it on that camera so tooltips can draw over it.
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)canvasObject.transform;

            _root = ScoreBox.NewRect("Score", canvasObject.transform);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);

            // Flames go first so they draw behind every box.
            RectTransform flames = ScoreBox.NewRect("Flames", _root);
            _pegs = new ScoreBox(_root, flames, "Pegs", TextAlignmentOptions.Right, 11, ScoreColours.PegsFlame);
            _damage = new ScoreBox(_root, flames, "Damage", TextAlignmentOptions.Left, 23, ScoreColours.DamageFlame);
            _pegs.SetColours(ScoreColours.PegsFace, ScoreColours.PegsShade);
            _damage.SetColours(ScoreColours.DamageFace, ScoreColours.DamageShade);

            _timesRect = ScoreBox.NewRect("Times", _root);
            _times = _timesRect.gameObject.AddComponent<TextMeshProUGUI>();
            _times.text = "x";
            _times.fontSize = 40f;
            // Centre on the glyph's drawn shape, not its advance width, so the gaps either side of the "x" match.
            _times.alignment = TextAlignmentOptions.CenterGeoAligned;
            _times.color = ScoreColours.Times;
            _times.raycastTarget = false;

            _result = new ScoreBox(_root, flames, "Result", TextAlignmentOptions.Center, 37, null);
            _result.SetColours(ScoreColours.ResultFace, ScoreColours.ResultShade);
            _result.AddBorder(ScoreColours.ResultBorder);

            _overkill = new OverkillStamp(_root);
            _tooltip = new ScoreTooltip(canvasObject.transform);

            LayoutWidget(312f);
            ShowValues(0, 0);
            ApplyPhaseLayout();
            _root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            PachinkoBall.OnShotFired = (PachinkoBall.PachinkoBallFired)Delegate.Combine(PachinkoBall.OnShotFired, new PachinkoBall.PachinkoBallFired(OnShotFired));
            BattleController.OnShotComplete = (BattleController.ShotComplete)Delegate.Combine(BattleController.OnShotComplete, new BattleController.ShotComplete(OnShotComplete));
            BattleController.OnBattleEnded = (BattleController.BattleEvent)Delegate.Combine(BattleController.OnBattleEnded, new BattleController.BattleEvent(OnBattleEnded));
            BattleController.OnBattleStarted = (BattleController.BattleEvent)Delegate.Combine(BattleController.OnBattleStarted, new BattleController.BattleEvent(OnBattleStarted));
        }

        private void OnDisable()
        {
            PachinkoBall.OnShotFired = (PachinkoBall.PachinkoBallFired)Delegate.Remove(PachinkoBall.OnShotFired, new PachinkoBall.PachinkoBallFired(OnShotFired));
            BattleController.OnShotComplete = (BattleController.ShotComplete)Delegate.Remove(BattleController.OnShotComplete, new BattleController.ShotComplete(OnShotComplete));
            BattleController.OnBattleEnded = (BattleController.BattleEvent)Delegate.Remove(BattleController.OnBattleEnded, new BattleController.BattleEvent(OnBattleEnded));
            BattleController.OnBattleStarted = (BattleController.BattleEvent)Delegate.Remove(BattleController.OnBattleStarted, new BattleController.BattleEvent(OnBattleStarted));
        }

        private void OnBattleStarted()
        {
            // Loading the boss prefab can take a moment the first time in an act; do it now rather than mid-shot.
            BossTarget.Get();
        }

        private void OnDestroy()
        {
            ModLog.Info("Score widget destroyed; it will be rebuilt when the next battle starts.");
            _pegs?.Destroy();
            _damage?.Destroy();
            _result?.Destroy();
            _overkill?.Destroy();
        }

        private void OnShotFired(Vector2 aimVector)
        {
            BattleController battle = FindBattle();
            // Extra balls from the same shot (summoning circle, multiball) also report a fire: keep counting.
            if (battle == null || !BattleController.BattleActive || _tracker.Phase == ScorePhase.Counting
                || BattleController.CurrentBattleState == BattleController.BattleState.NAVIGATION)
            {
                return;
            }
            bool isHeal = battle.attackManager != null && battle.attackManager.isHeal;
            _tracker.ResultHoldSeconds = Plugin.ResultSeconds.Value;
            _tracker.BeginShot(isHeal);
            _target = isHeal ? PlayerMaxHealth(battle) : BossTarget.Get();
            if (!isHeal && BattleController.IsBossBattle)
            {
                // In the boss fight itself the live boss is the final word (e.g. relics that change its HP).
                _target = Math.Max(_target, ToughestEnemyOnScreen(bossOnly: true));
            }
            if (_target <= 0)
            {
                _target = ToughestEnemyOnScreen(bossOnly: false);
            }
            int floor = BossTarget.FloorInAct();
            _flameStart = FlameCurve.StartRatioForFloor(floor);
            string flameNote = $"floor {floor}: flames light at {_flameStart:P0} of {_target} = {Math.Ceiling(_flameStart * _target)} {(isHeal ? "healing" : "damage")}";
            if (flameNote != _loggedFlameNote)
            {
                _loggedFlameNote = flameNote;
                ModLog.Info($"Score target for this battle, {flameNote}.");
            }
            _previousTotal = 0;
            _hasRead = false;
            _overkill.Hide();
        }

        private void OnShotComplete()
        {
            if (_tracker.Phase != ScorePhase.Counting)
            {
                return;
            }
            ReadGame(force: true);
            _tracker.CompleteShot(_tracker.Total);
        }

        private void OnBattleEnded()
        {
            // A result from the winning shot stays up for its full time; only a shot cut off mid-flight is dropped.
            if (_tracker.Phase == ScorePhase.Counting)
            {
                _tracker.Clear();
            }
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            BattleController battle = FindBattle();

            if (_tracker.Phase == ScorePhase.Counting)
            {
                if (battle == null)
                {
                    _tracker.Clear();
                }
                else if (BattleController.CurrentBattleState == BattleController.BattleState.AWAITING_SHOT)
                {
                    // The shot-complete event was missed somehow; show what we have rather than getting stuck.
                    _tracker.CompleteShot(_tracker.Total);
                }
                else
                {
                    ReadGame(force: false);
                }
            }
            _tracker.Tick(deltaTime);

            bool inBattle = battle != null && BattleController.BattleActive
                && BattleController.CurrentBattleState != BattleController.BattleState.NAVIGATION;
            bool wanted = Plugin.ShowScore.Value && Time.timeScale > 0f
                && (_tracker.Phase != ScorePhase.Ready || (inBattle && ScrollVisible()));
            _alpha = Mathf.MoveTowards(_alpha, wanted ? 1f : 0f, FadeSpeed * deltaTime);
            if (_alpha <= 0f)
            {
                if (_root.gameObject.activeSelf)
                {
                    _root.gameObject.SetActive(false);
                }
                _tooltip.Hide();
                return;
            }
            _root.gameObject.SetActive(true);
            ApplyFont();
            SyncCanvas();
            Place();
            Animate(deltaTime, time);
            UpdateTooltip();
        }

        /// <summary>While the mouse is over the score, explain it and show the last shot.</summary>
        private void UpdateTooltip()
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(_root, Input.mousePosition, CanvasCamera))
            {
                _tooltip.Hide();
                return;
            }
            string text = ScoreExplanation.Build(_tracker.LastShotTotal, _tracker.LastShotWasHeal);

            Vector3[] corners = new Vector3[4];
            _root.GetWorldCorners(corners);
            Vector2 bottomRight = _canvasRect.InverseTransformPoint(corners[3]);
            _tooltip.Show(text, bottomRight, -_canvasRect.rect.height / 2f);
        }

        private void ReadGame(bool force)
        {
            BattleController battle = FindBattle();
            if (battle == null)
            {
                return;
            }
            try
            {
                ShotDamage read = ShotDamage.ReadInputs(battle);
                if (!force && _hasRead && read.SameInputs(_lastRead))
                {
                    return;
                }
                read.Calculate(battle);
                _lastRead = read;
                _hasRead = true;
                _tracker.Update(read.PegTally, read.PerPeg, read.Total);
            }
            catch (Exception e)
            {
                if (!_warnedRead)
                {
                    _warnedRead = true;
                    Plugin.Log.LogWarning($"Couldn't read the shot's damage from the game: {e}");
                }
            }
        }

        private void Animate(float deltaTime, float time)
        {
            if (_tracker.Phase != _shownPhase || _tracker.IsHeal != _shownHeal)
            {
                bool toResult = _tracker.Phase == ScorePhase.Result;
                // When the result times out, fade it away over the 0 x 0 boxes. A new shot cuts it off at once.
                bool timedOut = _shownPhase == ScorePhase.Result && _tracker.Phase == ScorePhase.Ready;
                _resultFade = timedOut ? ResultFadeSeconds : 0f;
                _shownPhase = _tracker.Phase;
                _shownHeal = _tracker.IsHeal;
                ApplyPhaseLayout();
                if (toResult)
                {
                    _result.SetValue(-1, string.Empty);
                }
            }

            long total = _tracker.Total;
            if (_tracker.Phase == ScorePhase.Result)
            {
                bool firstShow = _result.Value < 0;
                _result.SetValue(total, ScoreFormat.Format(total));
                if (firstShow)
                {
                    _result.Punch(1f);
                }
                if (Plugin.ShowOverkill.Value && _target > 0 && total >= _target)
                {
                    // Healing past your max HP gets a green OVERHEAL instead.
                    _overkill.Show(_tracker.IsHeal);
                }
            }
            else
            {
                ShowValues(_tracker.Pegs, _tracker.PerPeg);
                if (_tracker.Phase == ScorePhase.Ready && _resultFade <= 0f)
                {
                    _overkill.Hide();
                }
            }

            if (_tracker.Phase != ScorePhase.Ready && FlameCurve.CrossedTarget(_previousTotal, total, _target))
            {
                _burst = BurstSeconds;
                _pegs.Punch(1f);
                _damage.Punch(1f);
                _result.Punch(1f);
            }
            _previousTotal = total;
            _burst = Mathf.Max(0f, _burst - deltaTime);

            double wantedIntensity = _tracker.Phase == ScorePhase.Ready ? 0 : FlameCurve.Intensity(total, _target, _flameStart);
            double speed = wantedIntensity > _intensity ? IntensityRiseSpeed : IntensityFallSpeed;
            _intensity = MoveTowards(_intensity, wantedIntensity, speed * deltaTime);
            double flames = Plugin.ShowFlames.Value ? _intensity + BurstExtraIntensity * (_burst / BurstSeconds) : 0;
            // The flames have no ceiling, but the shake does, so huge totals stay readable.
            float shake = Plugin.ShakeNumbers.Value ? Mathf.Min(MaxShake, (float)_intensity + _burst) : 0f;

            _pegs.Tick(deltaTime, time, shake, flames);
            _damage.Tick(deltaTime, time, shake, flames);
            _result.Tick(deltaTime, time, shake, 0);
            _overkill.Tick(deltaTime, time);
            FadeResult(deltaTime);
        }

        private void ShowValues(long pegs, long perPeg)
        {
            _pegs.SetValue(pegs, ScoreFormat.Format(pegs));
            _damage.SetValue(perPeg, ScoreFormat.Format(perPeg));
        }

        /// <summary>The boxes are replaced by the single result box while the total is on show.</summary>
        private void ApplyPhaseLayout()
        {
            bool result = _tracker.Phase == ScorePhase.Result;
            _pegs.SetVisible(1f);
            _damage.SetVisible(1f);
            _pegs.Rect.gameObject.SetActive(!result);
            _damage.Rect.gameObject.SetActive(!result);
            _times.gameObject.SetActive(!result);
            // A fading result stays up, on top of the boxes, until FadeResult finishes it.
            _result.SetVisible(result || _resultFade > 0f ? 1f : 0f);
            _result.Rect.gameObject.SetActive(result || _resultFade > 0f);

            bool heal = _tracker.IsHeal;
            _damage.SetColours(heal ? ScoreColours.HealFace : ScoreColours.DamageFace,
                heal ? ScoreColours.HealShade : ScoreColours.DamageShade,
                heal ? ScoreColours.HealFlame : ScoreColours.DamageFlame);
            if (result)
            {
                // Only on the way in: a healing total keeps its colour while it fades out.
                _result.SetTextColour(heal ? ScoreColours.HealFlame.Bright : ScoreColours.Text);
            }
        }

        /// <summary>Fade the timed-out result (and its stamp) away, then put it away.</summary>
        private void FadeResult(float deltaTime)
        {
            if (_resultFade <= 0f)
            {
                return;
            }
            _resultFade = Mathf.Max(0f, _resultFade - deltaTime);
            float alpha = _resultFade / ResultFadeSeconds;
            _result.SetVisible(alpha);
            _overkill.SetAlpha(alpha);
            if (_resultFade <= 0f)
            {
                _result.Rect.gameObject.SetActive(false);
                _overkill.Hide();
            }
        }

        /// <summary>
        /// Fill the gap right of the pegboard wall, centred between the forest strip and the enemy scroll. Worked out
        /// from the game's letterboxed 16:9 view, so it lands in the same place at any resolution.
        /// </summary>
        private void Place()
        {
            Camera camera = Camera.main;
            Rect view = camera != null ? camera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
            float left = view.x + view.width * (WallRight + SideMargin);
            float right = view.xMax - view.width * SideMargin;
            float top = view.yMax - view.height * ForestBottom;
            float bottom = view.yMax - view.height * ScrollTop;

            Vector2 topLeft = ToCanvas(new Vector2(left, top));
            Vector2 bottomRight = ToCanvas(new Vector2(right, bottom));
            float bandHeight = topLeft.y - bottomRight.y;
            float scale = Mathf.Max(0.1f, bandHeight * BandFill / BoxHeight * Plugin.Scale.Value);
            float width = (bottomRight.x - topLeft.x) / scale;
            if (Mathf.Abs(width - _layoutWidth) > 0.5f)
            {
                LayoutWidget(width);
            }

            _root.anchoredPosition = (topLeft + bottomRight) / 2f + new Vector2(Plugin.OffsetX.Value, Plugin.OffsetY.Value);
            _root.localScale = Vector3.one * scale;
        }

        private Vector2 ToCanvas(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, CanvasCamera, out Vector2 local);
            return local;
        }

        private void FindTooltipCanvases()
        {
            _tooltipCanvases.Clear();
            _gameTooltips = FindObjectOfType<TooltipManager>();
            if (_gameTooltips != null && _gameTooltips.GetComponent<Canvas>() != null)
            {
                _tooltipCanvases.Add(_gameTooltips.GetComponent<Canvas>());
            }
            // Relic tooltips sit hidden on the relic bar until hovered, so look through inactive ones too.
            foreach (RelicTooltip relicTooltip in Resources.FindObjectsOfTypeAll<RelicTooltip>())
            {
                if (!relicTooltip.gameObject.scene.IsValid())
                {
                    continue;
                }
                Canvas canvas = relicTooltip.GetComponentInParent<Canvas>(true);
                if (canvas != null && !_tooltipCanvases.Contains(canvas.rootCanvas))
                {
                    _tooltipCanvases.Add(canvas.rootCanvas);
                }
            }
        }

        private Camera CanvasCamera => _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        /// <summary>
        /// Draw on the game camera just above the enemy scroll and below the game's tooltips. An overlay canvas would
        /// always cover tooltips, since overlays draw after every camera.
        /// </summary>
        private void SyncCanvas()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }
            EnemyInfoManager scroll = FindScroll();
            Canvas reference = scroll != null && scroll.GetComponentInParent<Canvas>() != null ? scroll.GetComponentInParent<Canvas>().rootCanvas : null;
            if (Time.unscaledTime >= _nextTooltipSearch)
            {
                _nextTooltipSearch = Time.unscaledTime + 2f;
                FindTooltipCanvases();
            }
            Canvas tooltips = _gameTooltips != null ? _gameTooltips.GetComponent<Canvas>() : null;

            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = camera;
            int layer = reference != null ? reference.sortingLayerID : (tooltips != null ? tooltips.sortingLayerID : 0);
            int order = reference != null ? reference.sortingOrder + 1 : (tooltips != null ? tooltips.sortingOrder - 1 : 0);
            // Stay under every canvas that shows tooltips (the general tooltips and the relic bar's relic tooltips).
            foreach (Canvas tooltipCanvas in _tooltipCanvases)
            {
                if (tooltipCanvas != null && tooltipCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    && tooltipCanvas.sortingLayerID == layer && order >= tooltipCanvas.sortingOrder)
                {
                    order = tooltipCanvas.sortingOrder - 1;
                }
            }
            _canvas.sortingLayerID = layer;
            _canvas.sortingOrder = order;
            // Anywhere between the camera's clip planes works for an orthographic camera; match the scroll if it can.
            float distance = reference != null && reference.renderMode == RenderMode.ScreenSpaceCamera ? reference.planeDistance : 10f;
            _canvas.planeDistance = Mathf.Clamp(distance, camera.nearClipPlane + 0.01f, camera.farClipPlane - 0.01f);

            if (!_loggedSorting)
            {
                _loggedSorting = true;
                ModLog.Info($"Score drawn on camera {camera.name}, sorting layer {SortingLayer.IDToName(layer)} order {order} " +
                    $"(scroll {(reference != null ? reference.sortingOrder.ToString() : "?")}, tooltip canvases " +
                    $"{string.Join(", ", _tooltipCanvases.ConvertAll(c => c == null ? "?" : $"{c.name} {c.renderMode} {SortingLayer.IDToName(c.sortingLayerID)}/{c.sortingOrder}"))}).");
            }
        }

        /// <summary>Two boxes and the "x" across the given width; the result box and stamp span all of it.</summary>
        private void LayoutWidget(float width)
        {
            _layoutWidth = width;
            _root.sizeDelta = new Vector2(width, BoxHeight);
            float boxWidth = Mathf.Max(40f, (width - TimesGap) / 2f);
            float boxX = TimesGap / 2f + boxWidth / 2f;
            _pegs.Layout(new Vector2(-boxX, 0f), new Vector2(boxWidth, BoxHeight));
            _damage.Layout(new Vector2(boxX, 0f), new Vector2(boxWidth, BoxHeight));
            _timesRect.anchoredPosition = new Vector2(0f, TimesDrop);
            _timesRect.sizeDelta = new Vector2(TimesGap, BoxHeight);
            _result.Layout(Vector2.zero, new Vector2(width, BoxHeight));
            // Above the result box, among the flames, rather than over the total.
            _overkill.Layout(new Vector2(0f, BoxHeight / 2f + 30f), new Vector2(width * 0.9f, 44f));
        }

        /// <summary>
        /// In boss fights the game puts the boss health bar in the gap the score uses. Push the bar (and the enemy
        /// scroll under it) down just far enough to sit below the score, and put them back when the score is hidden.
        /// Runs after the game's own Update and animations so it has the last word on their positions.
        /// </summary>
        private void LateUpdate()
        {
            float wanted = 0f;
            if (_root.gameObject.activeSelf && BattleController.IsBossBattle && TryBossBarTop(out float barTop))
            {
                Vector3[] corners = new Vector3[4];
                _root.GetWorldCorners(corners);
                float scoreBottom = RectTransformUtility.WorldToScreenPoint(CanvasCamera, corners[0]).y;
                float gap = Screen.height * BossBarGap;
                // barTop is measured where the bar sits now, already pushed down by the current shift.
                wanted = Mathf.Max(0f, barTop + _shift - (scoreBottom - gap));
            }
            _shift = wanted <= 0f && _shift < 0.5f ? 0f : Mathf.Lerp(_shift, wanted, 1f - Mathf.Exp(-ShiftSpeed * Time.unscaledDeltaTime));

            if (Time.unscaledTime >= _nextShiftSearch || _shifts.Exists(s => !s.Alive))
            {
                _nextShiftSearch = Time.unscaledTime + 1f;
                FindShiftTargets();
            }
            foreach (ScreenShift shift in _shifts)
            {
                shift.Apply(_shift);
            }
            if (_shift > 0f && !_loggedShift)
            {
                _loggedShift = true;
                ModLog.Info($"Moved down to make room for the score (target {wanted:0}px): " +
                    string.Join(", ", _shifts.ConvertAll(s => s.Target != null ? s.Target.name : "?")));
            }
        }

        /// <summary>
        /// The boss health bars and every part of the enemy scroll. The scroll's art, mask and list don't all hang
        /// off one object, so each is moved, but only the top-most ones: moving a parent already moves its children.
        /// Existing shifts are kept so an object that's already moved isn't treated as being in its own spot.
        /// </summary>
        private void FindShiftTargets()
        {
            _shifts.RemoveAll(s => !s.Alive);
            System.Collections.Generic.List<Transform> candidates = new System.Collections.Generic.List<Transform>();
            if (_bossBars != null)
            {
                candidates.Add(_bossBars.transform);
            }
            UpcomingEnemyViewLocator upcoming = FindObjectOfType<UpcomingEnemyViewLocator>();
            if (upcoming != null)
            {
                candidates.Add(upcoming.transform);
            }
            EnemyInfoManager scroll = FindScroll();
            if (scroll != null)
            {
                candidates.Add(scroll.transform);
                if (scroll._scrollAnim != null)
                {
                    candidates.Add(scroll._scrollAnim.transform);
                }
                if (scroll._mask != null)
                {
                    candidates.Add(scroll._mask.transform);
                }
            }

            foreach (Transform candidate in candidates)
            {
                bool underAnother = candidates.Exists(other => other != candidate && candidate.IsChildOf(other));
                bool known = _shifts.Exists(s => s.Target == candidate);
                if (!underAnother && !known)
                {
                    // A newly found parent takes over from any child already being moved on its own.
                    foreach (ScreenShift child in _shifts.FindAll(s => s.Target.IsChildOf(candidate)))
                    {
                        child.Restore();
                        _shifts.Remove(child);
                    }
                    _shifts.Add(new ScreenShift(candidate));
                }
            }
        }

        /// <summary>Screen y of the top of whichever boss health bars are showing.</summary>
        private bool TryBossBarTop(out float top)
        {
            top = float.MinValue;
            if (_bossBars == null)
            {
                try
                {
                    _bossBars = GameObject.FindGameObjectWithTag("BossHealthBar");
                }
                catch (UnityException)
                {
                    // The tag doesn't exist in this game version.
                }
                if (_bossBars == null)
                {
                    return false;
                }
                _nextShiftSearch = 0f;
            }
            Vector3[] corners = new Vector3[4];
            foreach (CanvasGroup bar in _bossBars.GetComponentsInChildren<CanvasGroup>(false))
            {
                if (bar.alpha < 0.05f)
                {
                    continue;
                }
                Camera camera = ScreenShift.CameraFor(bar.transform);
                foreach (Graphic graphic in bar.GetComponentsInChildren<Graphic>(false))
                {
                    graphic.rectTransform.GetWorldCorners(corners);
                    top = Mathf.Max(top, RectTransformUtility.WorldToScreenPoint(camera, corners[1]).y);
                }
            }
            return top > float.MinValue;
        }

        private bool ScrollVisible()
        {
            EnemyInfoManager scroll = FindScroll();
            return scroll == null || scroll.isActiveAndEnabled;
        }

        private EnemyInfoManager FindScroll()
        {
            if (_scroll == null && Time.unscaledTime >= _nextScrollSearch)
            {
                _nextScrollSearch = Time.unscaledTime + 1f;
                _scroll = FindObjectOfType<EnemyInfoManager>();
            }
            return _scroll;
        }

        private BattleController FindBattle()
        {
            if (_battle == null && BattleController.BattleActive)
            {
                _battle = FindObjectOfType<BattleController>();
            }
            return _battle;
        }

        /// <summary>Use the game's own pixel font, taken from the scroll's text when it's around.</summary>
        private void ApplyFont()
        {
            if (_fontApplied)
            {
                return;
            }
            TMP_FontAsset font = null;
            EnemyInfoManager scroll = FindScroll();
            TMP_Text scrollText = scroll != null ? scroll.GetComponentInChildren<TMP_Text>(true) : null;
            if (scrollText != null && scrollText.font != null && scrollText.font != TMP_Settings.defaultFontAsset)
            {
                font = scrollText.font;
            }
            if (font == null)
            {
                foreach (TextMeshProUGUI text in FindObjectsOfType<TextMeshProUGUI>())
                {
                    if (text.font != null && text.font != TMP_Settings.defaultFontAsset && !text.transform.IsChildOf(transform))
                    {
                        font = text.font;
                        break;
                    }
                }
            }
            if (font == null)
            {
                return;
            }
            _pegs.SetFont(font);
            _damage.SetFont(font);
            _result.SetFont(font);
            _times.font = font;
            _overkill.SetFont(font);
            _tooltip.SetFont(font);
            _fontApplied = true;
        }

        private static long PlayerMaxHealth(BattleController battle)
        {
            PlayerHealthController health = battle.PlayerHealthController;
            return health != null ? (long)Mathf.Ceil(health.MaxHealth) : 0;
        }

        private static long ToughestEnemyOnScreen(bool bossOnly)
        {
            float toughest = 0f;
            foreach (Battle.Enemies.Enemy enemy in FindObjectsOfType<Battle.Enemies.Enemy>())
            {
                if (!bossOnly || enemy.enemyTypes.HasFlag(Battle.Enemies.Enemy.EnemyType.Boss))
                {
                    toughest = Mathf.Max(toughest, enemy.maxHealth);
                }
            }
            return (long)Mathf.Ceil(toughest);
        }

        private static double MoveTowards(double current, double target, double maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
            {
                return target;
            }
            return current + Math.Sign(target - current) * maxDelta;
        }
    }

    /// <summary>Make sure the widget exists for every battle, in case something destroyed it since the last one.</summary>
    [HarmonyPatch(typeof(BattleController), "OnEnable")]
    internal static class BattleEnablePatch
    {
        private static void Postfix()
        {
            ScoreHud.Ensure();
        }
    }

    /// <summary>Catches the exact numbers the attack goes out with, to correct the result on show.</summary>
    [HarmonyPatch(typeof(AttackManager), nameof(AttackManager.Attack))]
    internal static class AttackFiredPatch
    {
        private static void Prefix(AttackManager __instance, int pegMultipliersTally, float dmgMultiplier, long dmgBonus, int criticalHitCount)
        {
            try
            {
                ScoreHud.AttackFired(__instance, pegMultipliersTally, dmgMultiplier, dmgBonus, criticalHitCount);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't read the attack's final damage: {e.Message}");
            }
        }
    }
}
