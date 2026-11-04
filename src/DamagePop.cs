using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PeglinBalatroScore
{
    /// <summary>
    /// The bomb chest's damage number: big, bomb-red and outlined so it reads over the forest, popping out of the middle
    /// of the burst chest and rising a little, until the chest tells it to shatter.
    /// </summary>
    internal class DamagePop
    {
        public const float FontSize = 30f;

        private const float PopSeconds = 0.25f;
        private const float RiseDistance = 14f;

        /// <summary>The chest's blasts are drawn five sorting orders above the HUD canvas; sit six above it.</summary>
        private const int SortingOffsetAboveHud = 6;

        private static readonly Color32 Fill = ScoreColours.BombRed;
        private static readonly Color32 Edge = new Color32(24, 18, 16, 255);

        /// <summary>Outline offsets (in chest units) for the four copies drawn behind the number, plus a drop shadow.</summary>
        private static readonly Vector2[] OutlineOffsets =
        {
            new Vector2(-2f, 0f), new Vector2(2f, 0f), new Vector2(0f, -2f), new Vector2(0f, 2f), new Vector2(2f, -3f),
        };

        private readonly RectTransform _rect;
        private readonly Canvas _canvas;
        private readonly List<TextMeshProUGUI> _texts = new List<TextMeshProUGUI>();
        private readonly TextMeshProUGUI _main;
        private Vector2 _start;
        private float _age = -1f;

        public DamagePop(Transform parent)
        {
            _canvas = NewCanvas(parent);
            _rect = Outlined(_canvas.transform, "DamagePop", string.Empty, ScoreHud.GameFont, _texts);
            _main = _texts[_texts.Count - 1];
            Hide();
        }

        /// <summary>
        /// Keep the number above the bomb blasts. The chest's explosions are world sprites five sorting orders above
        /// the HUD canvas, so the number needs its own canvas above them or their smoke hides it.
        /// </summary>
        public void SetSorting(Canvas hud)
        {
            if (_canvas == null || hud == null)
            {
                return;
            }
            _canvas.sortingLayerID = hud.sortingLayerID;
            _canvas.sortingOrder = hud.sortingOrder + SortingOffsetAboveHud;
        }

        private static Canvas NewCanvas(Transform parent)
        {
            RectTransform rect = ScoreBox.NewRect("DamagePopCanvas", parent);
            ScoreBox.Stretch(rect);
            Canvas canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            return canvas;
        }

        public bool Showing => _age >= 0f;

        /// <summary>Seconds since it popped out.</summary>
        public float Age => _age;

        /// <summary>Pop out at a point in the chest's own units.</summary>
        public void Show(string text, Vector2 at)
        {
            SetText(text);
            SetFont(ScoreHud.GameFont);
            _start = at;
            _age = 0f;
            _rect.anchoredPosition = at;
            _rect.localScale = Vector3.zero;
            _rect.gameObject.SetActive(true);
        }

        public void SetText(string text)
        {
            foreach (TextMeshProUGUI copy in _texts)
            {
                if (copy.text != text)
                {
                    copy.text = text;
                }
            }
        }

        public void Hide()
        {
            _age = -1f;
            _rect.gameObject.SetActive(false);
        }

        public void Tick(float deltaTime)
        {
            if (!Showing)
            {
                return;
            }
            _age += deltaTime;
            // Burst out bigger than full size, then settle back.
            float t = Mathf.Clamp01(_age / PopSeconds);
            float scale = t < 1f ? Mathf.Sin(t * Mathf.PI * 0.75f) / Mathf.Sin(Mathf.PI * 0.75f) * (1f + 0.35f * (1f - t)) : 1f;
            _rect.localScale = Vector3.one * Mathf.Max(0f, scale);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_age / Core.BombChestTimeline.NumberShatterSeconds));
            _rect.anchoredPosition = _start + new Vector2(0f, RiseDistance * rise);
            _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_age * 7f) * 3f);
        }

        /// <summary>Each visible digit and where its middle is, in the given transform's space (to shatter it).</summary>
        public List<KeyValuePair<string, Vector2>> Characters(Transform space)
        {
            List<KeyValuePair<string, Vector2>> characters = new List<KeyValuePair<string, Vector2>>();
            _main.ForceMeshUpdate();
            TMP_TextInfo info = _main.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }
                Vector3 middle = (character.bottomLeft + character.topRight) / 2f;
                Vector3 world = _main.rectTransform.TransformPoint(middle);
                characters.Add(new KeyValuePair<string, Vector2>(character.character.ToString(), space.InverseTransformPoint(world)));
            }
            return characters;
        }

        public void SetFont(TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }
            foreach (TextMeshProUGUI copy in _texts)
            {
                copy.font = font;
            }
        }

        /// <summary>A bomb-red, dark-outlined piece of text, centred on its own position (also used for flying digits).</summary>
        public static RectTransform Outlined(Transform parent, string name, string text, TMP_FontAsset font, List<TextMeshProUGUI> copies = null)
        {
            RectTransform root = ScoreBox.NewRect(name, parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(200f, FontSize * 1.4f);
            foreach (Vector2 offset in OutlineOffsets)
            {
                TextMeshProUGUI back = NewText(root, text, font, Edge);
                back.rectTransform.anchoredPosition = offset;
                copies?.Add(back);
            }
            TextMeshProUGUI front = NewText(root, text, font, Fill);
            copies?.Add(front);
            return root;
        }

        private static TextMeshProUGUI NewText(RectTransform parent, string text, TMP_FontAsset font, Color32 colour)
        {
            RectTransform rect = ScoreBox.NewRect("Text", parent);
            ScoreBox.Stretch(rect);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                tmp.font = font;
            }
            tmp.text = text;
            tmp.fontSize = FontSize;
            tmp.color = colour;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
