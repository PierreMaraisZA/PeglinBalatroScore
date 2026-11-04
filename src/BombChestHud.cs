using System;
using System.Collections;
using System.Collections.Generic;
using Battle;
using Battle.Attacks;
using HarmonyLib;
using PeglinBalatroScore.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PeglinBalatroScore
{
    /// <summary>
    /// A see-through chest behind Peglin that catches a bomb the moment each one is lit, while the orb is still
    /// bouncing. Every bomb drops into it; once the heap tops the rim, the extras spill off the sides. When the last
    /// bomb Peglin throws detonates, the chest bursts on that same frame into planks and every bomb left in it goes off
    /// together, then the bombs' total damage pops out of the middle and shatters. Runs on real time, so game speed
    /// doesn't change it, and it is synced to the game's own detonation event rather than a timer.
    /// </summary>
    internal class BombChestHud : MonoBehaviour
    {
        private const float FadeInSeconds = 0.25f;
        private const float FadeOutSeconds = 0.4f;

        /// <summary>How tall the chest is next to Peglin, as a share of his sprite's height.</summary>
        private const float HeightOfPeglin = 0.8f;

        /// <summary>Gap between the chest and Peglin's back, as a share of his sprite's width.</summary>
        private const float GapBehindPeglin = 0.1f;

        /// <summary>How far above the rim bombs start falling from, in chest units.</summary>
        private const float DropHeight = 60f;


        /// <summary>Only this many bombs play the explosion sound, so a big pile doesn't deafen anyone.</summary>
        private const int MaxExplosionSounds = 4;

        /// <summary>The prefab's animator state for the blast itself, past its built-in pre-explosion fuse.</summary>
        private const string ExplosionState = "explode";

        private const int Planks = 9;
        private const int Splinters = 14;

        /// <summary>Small white and dark chips thrown off with the digits when the number shatters.</summary>
        private const int NumberChips = 10;

        private static readonly AccessTools.FieldRef<BattleController, Transform> PlayerRef =
            AccessTools.FieldRefAccess<BattleController, Transform>("_playerTransform");
        private static readonly AccessTools.FieldRef<BattleController, GameObject> BombPrefabRef =
            AccessTools.FieldRefAccess<BattleController, GameObject>("_tossableBombPrefab");
        private static readonly AccessTools.FieldRef<BattleController, GameObject> RiggedBombPrefabRef =
            AccessTools.FieldRefAccess<BattleController, GameObject>("_tossableRiggedBombPrefab");

        // The game's count of bombs lit and waiting to be thrown. It goes up as the orb sets off bomb pegs (and when
        // relics add bombs) and back down as Peglin's thrown bombs detonate.
        private static readonly AccessTools.FieldRef<BattleController, int> BombsToThrowRef =
            AccessTools.FieldRefAccess<BattleController, int>("_bombsToThrowRegular");
        private static readonly AccessTools.FieldRef<BattleController, int> RiggedBombsToThrowRef =
            AccessTools.FieldRefAccess<BattleController, int>("_bombsToThrowRigged");

        private static BombChestHud _instance;

        private readonly BombPhysics _physics = new BombPhysics();
        private readonly ChestDebris _debris = new ChestDebris();
        private readonly List<ChestEvent> _events = new List<ChestEvent>();
        private readonly List<BombView> _bombs = new List<BombView>();
        private readonly Queue<BombView> _toExplode = new Queue<BombView>();
        private readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
        private readonly System.Random _random = new System.Random();

        /// <summary>Thrown bombs whose damage is already in the total, so each counts once however many it hits.</summary>
        private readonly HashSet<int> _countedBombs = new HashSet<int>();

        private BombChestTimeline _timeline;
        private BattleController _battle;
        private RectTransform _root;
        private CanvasGroup _group;
        private RectTransform _bombLayer;
        private RawImage _back;
        private RawImage _front;
        private RectTransform _debrisLayer;
        private DamagePop _pop;
        private Texture2D _backTexture;
        private Texture2D _frontTexture;
        private Texture2D _plankTexture;
        private Texture2D _splinterTexture;
        private float _bombRadius;
        private float _alpha;
        private bool _hiding;

        private int _soundsPlayed;
        private bool _warnedPeglin;
        private bool _loggedPlacement;
        private bool _built;
        private int _chestId;
        private float _nextCanvasSync;
        private int _seenBombs;
        private int _seenRiggedBombs;

        private class BombView
        {
            public PhysicsBall Ball;
            public Image Image;
            public bool Rigged;

            /// <summary>Gone off, or lined up to.</summary>
            public bool Done;
        }

        /// <summary>A thrown bomb hit an enemy for this much (from the DamageEnemyWithBomb patch); counted once per bomb.</summary>
        internal static void BombDamage(BombLob bomb, long amount)
        {
            if (_instance == null || _instance._timeline == null || bomb == null)
            {
                return;
            }
            if (_instance._countedBombs.Add(bomb.GetInstanceID()))
            {
                _instance._timeline.AddDamage(amount);
            }
        }

        /// <summary>
        /// A thrown bomb has detonated (from the OnBombDeath patch). The game decrements its bomb counters inside
        /// that method, so zero here means this was the last one: burst the chest on the same frame.
        /// </summary>
        internal static void BombDetonated(BattleController battle)
        {
            if (_instance == null || _instance._timeline == null || battle == null)
            {
                return;
            }
            if (BombsToThrowRef(battle) > 0 || RiggedBombsToThrowRef(battle) > 0)
            {
                return;
            }
            _instance._timeline.ThrowFinished();
        }

        private void Awake()
        {
            _instance = this;
            _timeline = new BombChestTimeline();
            _bombRadius = BombChestShape.BombRadius;
            _physics.SpillHeight = BombChestShape.SpillHeight;
            _physics.SpillPush = BombChestShape.SpillPush;
            BombChestShape.BuildWalls(_physics.Segments);
        }

        private void OnDestroy()
        {
            Destroy(_backTexture);
            Destroy(_frontTexture);
            Destroy(_plankTexture);
            Destroy(_splinterTexture);
        }

        /// <summary>The chest's UI, made on first use under the score's canvas.</summary>
        private bool Build()
        {
            if (_built)
            {
                return true;
            }
            ScoreHud hud = ScoreHud.Instance;
            if (hud == null || hud.CanvasRect == null)
            {
                return false;
            }
            _root = ScoreBox.NewRect("BombChest", hud.CanvasRect);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.sizeDelta = new Vector2(BombChestShape.OuterWidth, BombChestShape.OuterHeight);
            // Behind the score, so the score's tooltip still draws over the chest.
            _root.SetAsFirstSibling();
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _backTexture = ChestArt.Back();
            _frontTexture = ChestArt.Front();
            _plankTexture = ChestArt.Plank();
            _splinterTexture = ChestArt.Splinter();

            _back = ChestImage("Back", _backTexture);
            _bombLayer = ScoreBox.NewRect("Bombs", _root);
            Anchor(_bombLayer);
            _front = ChestImage("Front", _frontTexture);
            _debrisLayer = ScoreBox.NewRect("Debris", _root);
            Anchor(_debrisLayer);

            _pop = new DamagePop(_root);

            _root.gameObject.SetActive(false);
            _built = true;
            return true;
        }

        private RawImage ChestImage(string name, Texture texture)
        {
            RectTransform rect = ScoreBox.NewRect(name, _root);
            ScoreBox.Stretch(rect);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Children placed in chest units: origin at the middle of the chest's bottom edge, Y up.</summary>
        private static void Anchor(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        /// <summary>
        /// Drop a bomb into the chest for each new bomb the game has lit (during the shot, as the orb sets off bomb
        /// pegs). Once Peglin has thrown them all and the last has landed, or the battle ends before he does, tell the
        /// chest it's over: the total damage is complete by then.
        /// </summary>
        private void WatchBombs()
        {
            if (_battle == null && BattleController.BattleActive)
            {
                _battle = FindObjectOfType<BattleController>();
                _seenBombs = 0;
                _seenRiggedBombs = 0;
            }
            if (_battle == null || !BattleController.BattleActive)
            {
                _seenBombs = 0;
                _seenRiggedBombs = 0;
                // A battle that ends before Peglin throws still pays out what the chest caught; with the chest turned
                // off there is nothing to pay out, so drop it instead.
                if (Plugin.ShowBombChest.Value)
                {
                    _timeline.ThrowFinished();
                }
                else
                {
                    _timeline.Reset();
                }
                return;
            }
            if (!Plugin.ShowBombChest.Value)
            {
                // Turned off mid-throw: drop the chest rather than let it burst and fade on its own.
                _seenBombs = 0;
                _seenRiggedBombs = 0;
                _timeline.Reset();
                return;
            }
            int bombs = BombsToThrowRef(_battle);
            int rigged = RiggedBombsToThrowRef(_battle);
            for (int i = _seenRiggedBombs; i < rigged; i++)
            {
                _timeline.BombAdded(rigged: true);
            }
            for (int i = _seenBombs; i < bombs; i++)
            {
                _timeline.BombAdded(rigged: false);
            }
            // The counts only fall as Peglin's thrown bombs land, so back to none after some means the throw is done.
            if (bombs == 0 && rigged == 0 && (_seenBombs > 0 || _seenRiggedBombs > 0))
            {
                _timeline.ThrowFinished();
            }
            _seenBombs = bombs;
            _seenRiggedBombs = rigged;
        }

        private void Update()
        {
            WatchBombs();
            // The score hides while the game is paused; freeze the chest too rather than dropping bombs behind the menu.
            if (Time.timeScale <= 0f)
            {
                return;
            }
            if (_timeline == null || (_timeline.Phase == ChestPhase.Hidden && !_hiding && _alpha <= 0f))
            {
                return;
            }
            if (!Build())
            {
                return;
            }
            float deltaTime = Time.unscaledDeltaTime;

            if (!TryPlace())
            {
                // Peglin's gone (the battle scene unloaded): drop the chest.
                _timeline.Reset();
                ClearAll();
                _alpha = 0f;
                _hiding = false;
                _root.gameObject.SetActive(false);
                return;
            }

            _events.Clear();
            _timeline.Tick(deltaTime, _events);
            foreach (ChestEvent chestEvent in _events)
            {
                switch (chestEvent.Kind)
                {
                    case ChestEventKind.Drop:
                        DropBomb(chestEvent.Rigged, chestEvent.Instant);
                        break;
                    case ChestEventKind.Burst:
                        Burst();
                        break;
                    case ChestEventKind.DetonateAll:
                        DetonateRest();
                        break;
                    case ChestEventKind.ShowTotal:
                        // The number bursts out of the middle of where the chest was.
                        _pop.Show(TotalText(), new Vector2(0f, BombChestShape.OuterHeight / 2f));
                        break;
                    case ChestEventKind.Hide:
                        _hiding = true;
                        break;
                }
            }

            _physics.Advance(deltaTime);
            _debris.Step(Mathf.Min(deltaTime, 0.05f));
            ExplodeQueued();
            DrawBombs();
            DrawDebris();
            if (_pop.Showing)
            {
                // A late bomb can still land after the chest blows; keep the number up to date until it shatters.
                _pop.SetText(TotalText());
                _pop.Tick(deltaTime);
                if (_pop.Age >= BombChestTimeline.NumberShatterSeconds)
                {
                    ShatterNumber();
                }
            }

            bool visible = !_hiding && _timeline.Phase != ChestPhase.Hidden;
            _alpha = Mathf.MoveTowards(_alpha, visible ? 1f : 0f, deltaTime / (visible ? FadeInSeconds : FadeOutSeconds));
            _group.alpha = _alpha;
            if (!visible && _alpha <= 0f)
            {
                _hiding = false;
                ClearAll();
                _root.gameObject.SetActive(false);
            }
        }

        /// <summary>Sit on the ground just behind Peglin, sized to him, following him if he moves.</summary>
        private bool TryPlace()
        {
            if (_battle == null)
            {
                _battle = FindObjectOfType<BattleController>();
            }
            Transform player = _battle != null ? PlayerRef(_battle) : null;
            Camera camera = Camera.main;
            if (player == null || camera == null)
            {
                return false;
            }
            Bounds bounds = default;
            bool found = false;
            player.GetComponentsInChildren(_sprites);
            foreach (SpriteRenderer sprite in _sprites)
            {
                if (!sprite.enabled || sprite.sprite == null)
                {
                    continue;
                }
                if (found)
                {
                    bounds.Encapsulate(sprite.bounds);
                }
                else
                {
                    bounds = sprite.bounds;
                    found = true;
                }
            }
            if (!found)
            {
                if (!_warnedPeglin)
                {
                    _warnedPeglin = true;
                    Plugin.Log.LogWarning("Couldn't find Peglin's sprite, so the bomb chest is placed from his position instead.");
                }
                bounds = new Bounds(player.position, Vector3.one);
            }

            ScoreHud hud = ScoreHud.Instance;
            if (Time.unscaledTime >= _nextCanvasSync)
            {
                // Sorting and the font only need checking now and then, not every frame.
                _nextCanvasSync = Time.unscaledTime + 0.5f;
                hud.SyncCanvasNow();
                ScoreHud.ApplyFontNow();
                _pop?.SetSorting(hud.HudCanvas);
            }
            _root.gameObject.SetActive(true);
            Vector2 feetBack = ToCanvas(hud, camera, new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
            Vector2 headFront = ToCanvas(hud, camera, new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
            float peglinHeight = Mathf.Max(10f, headFront.y - feetBack.y);
            float peglinWidth = Mathf.Max(10f, headFront.x - feetBack.x);
            float scale = peglinHeight * HeightOfPeglin / BombChestShape.OuterHeight * Plugin.ChestScale.Value;
            float chestRight = feetBack.x - peglinWidth * GapBehindPeglin;
            _root.localScale = Vector3.one * scale;
            _root.anchoredPosition = new Vector2(chestRight - BombChestShape.OuterWidth * scale / 2f, feetBack.y);
            if (!_loggedPlacement)
            {
                _loggedPlacement = true;
                ModLog.Info($"Bomb chest placed behind Peglin at {_root.anchoredPosition} (Peglin {peglinWidth:0}x{peglinHeight:0} canvas units, chest scale {scale:0.00}).");
            }
            return true;
        }

        private static Vector2 ToCanvas(ScoreHud hud, Camera worldCamera, Vector3 world)
        {
            Vector2 screen = worldCamera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hud.CanvasRect, screen, hud.HudCamera, out Vector2 local);
            return local;
        }

        private void DropBomb(bool rigged, bool instant)
        {
            if (_timeline.ChestId != _chestId)
            {
                // First bomb of a new chest: start clean, even if the last chest was still fading. The previous chest
                // burst left only the ground in the physics, so put its floor and walls back.
                _chestId = _timeline.ChestId;
                ClearAll();
                BombChestShape.BuildWalls(_physics.Segments);
                _hiding = false;
                _front.enabled = true;
                _back.enabled = true;
            }
            // Always over the opening: if the pile is already over the rim, the physics spills it off the sides.
            float x = BombChestShape.DropX((float)(_random.NextDouble() * 2 - 1));
            float minY = instant ? BombChestShape.Wall + _bombRadius : BombChestShape.OuterHeight + DropHeight;
            float y = _physics.ClearDropHeight(x, _bombRadius, minY);
            PhysicsBall ball = _physics.AddBall(x, y, _bombRadius);
            ball.Angle = (float)(_random.NextDouble() * Math.PI * 2);

            RectTransform rect = ScoreBox.NewRect("Bomb", _bombLayer);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            // The sprite includes the fuse, so draw it a bit bigger than the round body that collides.
            rect.sizeDelta = Vector2.one * _bombRadius * 2.6f;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = BombSprite(rigged);
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (image.sprite == null)
            {
                image.color = rigged ? ScoreColours.BombRed : new Color32(40, 40, 52, 255);
            }
            _bombs.Add(new BombView { Ball = ball, Image = image, Rigged = rigged });
        }

        private void DrawBombs()
        {
            foreach (BombView bomb in _bombs)
            {
                RectTransform rect = bomb.Image.rectTransform;
                rect.anchoredPosition = new Vector2(bomb.Ball.X, bomb.Ball.Y);
                rect.localRotation = Quaternion.Euler(0f, 0f, bomb.Ball.Angle * Mathf.Rad2Deg);
            }
        }

        /// <summary>The last real bomb has detonated: every bomb still in the chest goes off one after another, with sound.</summary>
        private void DetonateRest()
        {
            foreach (BombView bomb in _bombs)
            {
                if (!bomb.Done)
                {
                    bomb.Done = true;
                    _toExplode.Enqueue(bomb);
                }
            }
            _soundsPlayed = 0;
        }

        private void Detonate(BombView bomb, bool withSound)
        {
            bomb.Done = true;
            PlayExplosionAt(new Vector2(bomb.Ball.X, bomb.Ball.Y), bomb.Rigged, withSound);
            bomb.Image.enabled = false;
            _physics.Remove(bomb.Ball);
        }

        /// <summary>The last real bomb detonated: the chest bursts into planks, and its bombs tumble to the ground.</summary>
        private void Burst()
        {
            BombChestShape.BuildGroundOnly(_physics.Segments);
            _front.enabled = false;
            _back.enabled = false;
            _debris.Burst(Planks, Splinters, _random.Next());
            for (int i = 0; i < _debris.Pieces.Count; i++)
            {
                DebrisPiece piece = _debris.Pieces[i];
                RectTransform rect = ScoreBox.NewRect(piece.Splinter ? "Splinter" : "Plank", _debrisLayer);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                Texture2D texture = piece.Splinter ? _splinterTexture : _plankTexture;
                rect.sizeDelta = new Vector2(texture.width, texture.height) * ChestArt.PixelUnits;
                RawImage image = rect.gameObject.AddComponent<RawImage>();
                image.texture = texture;
                image.raycastTarget = false;
            }
        }

        /// <summary>The number bursts apart: each digit flies off on its own, with a few white chips, and tumbles to the ground.</summary>
        private void ShatterNumber()
        {
            List<KeyValuePair<string, Vector2>> digits = _pop.Characters(_debrisLayer);
            _pop.Hide();
            if (digits.Count == 0)
            {
                return;
            }
            Vector2 centre = Vector2.zero;
            foreach (KeyValuePair<string, Vector2> digit in digits)
            {
                centre += digit.Value;
            }
            centre /= digits.Count;

            // Debris images are matched to debris pieces by order, so add each image as its piece is added.
            foreach (KeyValuePair<string, Vector2> digit in digits)
            {
                DamagePop.Outlined(_debrisLayer, "Digit", digit.Key, ScoreHud.GameFont);
                _debris.Shatter(digit.Value.x, digit.Value.y, centre.x, centre.y, _random);
            }
            float width = Mathf.Max(20f, DamagePop.FontSize * 0.6f * digits.Count);
            for (int i = 0; i < NumberChips; i++)
            {
                RectTransform chip = ScoreBox.NewRect("Chip", _debrisLayer);
                chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0.5f);
                chip.sizeDelta = Vector2.one * ChestArt.PixelUnits * 1.5f;
                Image image = chip.gameObject.AddComponent<Image>();
                image.color = i % 3 == 0 ? new Color32(24, 18, 16, 255) : new Color32(255, 255, 255, 255);
                image.raycastTarget = false;
                float x = centre.x + ((float)_random.NextDouble() - 0.5f) * width;
                float y = centre.y + ((float)_random.NextDouble() - 0.5f) * DamagePop.FontSize;
                _debris.Shatter(x, y, centre.x, centre.y, _random);
            }
        }

        private string TotalText()
        {
            return ScoreFormat.Format(_timeline.Damage);
        }

        /// <summary>Every visual bomb in the chest goes off on the same frame.</summary>
        private void ExplodeQueued()
        {
            while (_toExplode.Count > 0)
            {
                Detonate(_toExplode.Dequeue(), withSound: _soundsPlayed < MaxExplosionSounds);
            }
        }

        /// <summary>The game's own bomb explosion (animation and sound) where one of the chest's bombs sits.</summary>
        private void PlayExplosionAt(Vector2 chestPoint, bool rigged, bool withSound)
        {
            GameObject prefab = BombPrefab(rigged);
            Camera camera = Camera.main;
            ScoreHud hud = ScoreHud.Instance;
            if (prefab == null || camera == null || hud == null)
            {
                return;
            }
            Vector3 uiWorld = _bombLayer.TransformPoint(chestPoint);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(hud.HudCamera, uiWorld);
            Transform player = _battle != null ? PlayerRef(_battle) : null;
            float depth = player != null ? camera.WorldToScreenPoint(player.position).z : 10f;
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));

            GameObject boom = Instantiate(prefab, world, Quaternion.identity);
            // A copy of the game's thrown bomb with its brains and physics switched off: only the explosion plays.
            // It isn't hooked up to the battle, so it deals no damage.
            BombLob lob = boom.GetComponent<BombLob>();
            if (lob != null)
            {
                lob.enabled = false;
            }
            Rigidbody2D body = boom.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.isKinematic = true;
                body.simulated = false;
            }
            foreach (Collider2D collider in boom.GetComponentsInChildren<Collider2D>())
            {
                collider.enabled = false;
            }
            Canvas canvas = hud.HudCanvas;
            foreach (SpriteRenderer sprite in boom.GetComponentsInChildren<SpriteRenderer>())
            {
                // Above the chest's canvas, so the blast covers the bombs and planks.
                sprite.sortingLayerID = canvas.sortingLayerID;
                sprite.sortingOrder = canvas.sortingOrder + 5;
            }

            AudioSource audio = boom.GetComponent<AudioSource>();
            withSound = withSound && audio != null;
            if (audio != null)
            {
                audio.volume = withSound ? 0.4f : 0f;
            }
            if (withSound)
            {
                _soundsPlayed++;
            }
            Animator animator = boom.GetComponent<Animator>();
            if (animator != null)
            {
                int state = Animator.StringToHash(ExplosionState);
                if (animator.HasState(0, state))
                {
                    // The game's prefab starts with a 0.3s pre-explosion fuse; the chest goes off with the bomb
                    // that just detonated, so jump straight to the blast instead of replaying that fuse.
                    animator.Play(state, 0, 0f);
                }
                else
                {
                    animator.SetTrigger(ExplosionState);
                }
            }
            StartCoroutine(FinishExplosion(boom, audio, withSound));
        }

        private static IEnumerator FinishExplosion(GameObject boom, AudioSource audio, bool withSound)
        {
            // The explosion animation normally starts the sound; if it didn't, play it ourselves.
            yield return new WaitForSecondsRealtime(0.05f);
            if (withSound && audio != null && audio.clip != null && !audio.isPlaying)
            {
                audio.Play();
            }
            yield return new WaitForSecondsRealtime(1.2f);
            if (boom != null)
            {
                Destroy(boom);
            }
        }

        private void DrawDebris()
        {
            int count = Mathf.Min(_debris.Pieces.Count, _debrisLayer.childCount);
            for (int i = 0; i < count; i++)
            {
                DebrisPiece piece = _debris.Pieces[i];
                RectTransform rect = (RectTransform)_debrisLayer.GetChild(i);
                rect.anchoredPosition = new Vector2(piece.X, piece.Y);
                rect.localRotation = Quaternion.Euler(0f, 0f, piece.Angle * Mathf.Rad2Deg);
            }
        }

        private void ClearAll()
        {
            _physics.Clear();
            _debris.Clear();
            _toExplode.Clear();
            _bombs.Clear();
            _countedBombs.Clear();
            if (_bombLayer != null)
            {
                DestroyChildren(_bombLayer);
                DestroyChildren(_debrisLayer);
            }
            _pop?.Hide();
            if (_front != null)
            {
                _front.enabled = true;
                _back.enabled = true;
            }
        }

        private static void DestroyChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                // Destroy is deferred, so hide it now: a detached, still-active UI child would draw without a canvas.
                child.SetActive(false);
                Destroy(child);
            }
            parent.DetachChildren();
        }

        private GameObject BombPrefab(bool rigged)
        {
            if (_battle == null)
            {
                return null;
            }
            GameObject prefab = rigged ? RiggedBombPrefabRef(_battle) : BombPrefabRef(_battle);
            return prefab != null ? prefab : BombPrefabRef(_battle);
        }

        private Sprite BombSprite(bool rigged)
        {
            GameObject prefab = BombPrefab(rigged);
            SpriteRenderer sprite = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
            return sprite != null ? sprite.sprite : null;
        }
    }

    /// <summary>
    /// Counts each thrown bomb's damage once: the value the game hands to every enemy the bomb hits, already
    /// including relics and Cruciball. It's read from the first hit of each bomb, so a bomb that hits three enemies
    /// still counts once. (Recomputing it ourselves would mean calling the game's damage function, which also uses up
    /// relic charges.)
    /// </summary>
    [HarmonyPatch(typeof(BattleController), "DamageEnemyWithBomb")]
    internal static class BombDamagePatch
    {
        private static void Prefix(BombLob bomb, long damage)
        {
            try
            {
                BombChestHud.BombDamage(bomb, damage);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Bomb chest couldn't count a bomb's damage: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Fires as each thrown bomb detonates, after the game has applied its damage and decremented its bomb counters.
    /// The chest uses this instead of a fuse timer, so it bursts on the same frame as the last real bomb.
    /// </summary>
    [HarmonyPatch(typeof(BattleController), "OnBombDeath")]
    internal static class BombDetonatedPatch
    {
        private static void Postfix(BattleController __instance)
        {
            try
            {
                BombChestHud.BombDetonated(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Bomb chest couldn't sync its explosion: {e.Message}");
            }
        }
    }
}
