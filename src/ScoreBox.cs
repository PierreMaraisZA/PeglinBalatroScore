using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeglinBalatroScore
{
    /// <summary>
    /// One chunky number box (pegs, damage or the result), Balatro style: the number bobs gently, jolts when it
    /// grows, and shakes harder as the score heats up. Optionally has its own pixel flame rising behind it.
    /// </summary>
    internal class ScoreBox
    {
        private const float Raise = 6f;
        private const float PunchDecay = 7f;
        private const float FlashDecay = 6f;
        private const float BobSpeed = 2.4f;
        private const float BobHeight = 2.5f;

        /// <summary>The pixel font sits high in its line, so the number is nudged down to look centred in the box.</summary>
        private const float TextDrop = -5f;

        /// <summary>How far below the box's top edge the flame starts, so its ragged base is tucked out of sight.</summary>
        private const float FlameRootInset = 6f;

        /// <summary>Height of the flame area, in box heights.</summary>
        private const float FlameHeight = 3.4f;

        public readonly RectTransform Rect;

        private readonly RectTransform _body;
        private readonly Image _face;
        private readonly Image _shade;
        private readonly Image _flash;
        private readonly Outline _outline;
        private readonly TextMeshProUGUI _text;
        private readonly TextMeshProUGUI _shadow;
        private readonly RectTransform _textRect;
        private readonly CanvasGroup _group;
        private readonly float _bobPhase;
        private readonly PixelFlame _flame;
        private readonly RawImage _flameImage;
        private readonly RectTransform _flameRect;
        private float _flameBaseHeight;

        private long _value = -1;
        private float _punch;
        private float _tilt;
        private float _flashAlpha;

        public ScoreBox(Transform parent, Transform flameLayer, string name, TextAlignmentOptions alignment, int seed,
            FlamePalette? flamePalette)
        {
            Rect = NewRect(name, parent);
            _group = Rect.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _body = NewRect("Body", Rect);
            Stretch(_body);

            // The darker shade shows as a thick bottom lip under the raised face, like Balatro's boxes.
            _shade = NewRect("Shade", _body).gameObject.AddComponent<Image>();
            Stretch(_shade.rectTransform);
            _shade.raycastTarget = false;
            _outline = _shade.gameObject.AddComponent<Outline>();
            _outline.effectColor = ScoreColours.Outline;
            _outline.effectDistance = new Vector2(3f, -3f);

            _face = NewRect("Face", _body).gameObject.AddComponent<Image>();
            Stretch(_face.rectTransform);
            _face.rectTransform.offsetMin = new Vector2(0f, Raise);
            _face.raycastTarget = false;

            _flash = NewRect("Flash", _body).gameObject.AddComponent<Image>();
            Stretch(_flash.rectTransform);
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _flash.raycastTarget = false;

            _textRect = NewRect("Number", _face.rectTransform);
            Stretch(_textRect);
            _textRect.offsetMin = new Vector2(12f, 2f);
            _textRect.offsetMax = new Vector2(-12f, -2f);
            _shadow = NewText("Shadow", _textRect, alignment, ScoreColours.TextShadow);
            _shadow.rectTransform.anchoredPosition = new Vector2(3f, -3f);
            _text = NewText("Text", _textRect, alignment, ScoreColours.Text);

            _bobPhase = seed * 1.7f;

            if (flamePalette.HasValue)
            {
                _flame = new PixelFlame(26, 46, seed, flamePalette.Value, growable: true);
                _flameRect = NewRect(name + "Flame", flameLayer);
                _flameRect.pivot = new Vector2(0.5f, 0f);
                _flameImage = _flameRect.gameObject.AddComponent<RawImage>();
                _flameImage.texture = _flame.Texture;
                _flameImage.raycastTarget = false;
            }
        }

        /// <summary>Position and size the box (and its flame) within the widget.</summary>
        public void Layout(Vector2 position, Vector2 size)
        {
            Rect.anchoredPosition = position;
            Rect.sizeDelta = size;
            if (_flameRect != null)
            {
                // Exactly as wide as the box so it never spills past the sides, and rooted just inside its top edge
                // so even a small flame shows above the box instead of hiding behind it.
                _flameRect.anchoredPosition = position + new Vector2(0f, size.y / 2f - FlameRootInset);
                // Tall enough that a score at the boss's HP sends up a column several boxes high; FitFlame stretches
                // it further when bigger scores grow the fire.
                _flameBaseHeight = size.y * FlameHeight;
                FitFlame();
            }
        }

        /// <summary>Match the flame image to its texture, which gets taller (same-size pixels) as the fire grows.</summary>
        private void FitFlame()
        {
            if (_flameImage.texture != _flame.Texture)
            {
                _flameImage.texture = _flame.Texture;
            }
            _flameRect.sizeDelta = new Vector2(Rect.sizeDelta.x, _flameBaseHeight * _flame.HeightScale);
        }

        public long Value => _value;

        public void SetColours(Color32 face, Color32 shade, FlamePalette? flamePalette = null)
        {
            _face.color = face;
            _shade.color = shade;
            if (flamePalette.HasValue && _flame != null)
            {
                _flame.SetPalette(flamePalette.Value);
            }
        }

        /// <summary>A coloured rim around the raised face.</summary>
        public void AddBorder(Color32 colour)
        {
            Outline border = _face.gameObject.AddComponent<Outline>();
            border.effectColor = colour;
            border.effectDistance = new Vector2(3f, 3f);
        }

        public void SetFont(TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }
            _text.font = font;
            _shadow.font = font;
        }

        public void SetTextColour(Color32 colour)
        {
            _text.color = colour;
        }

        public void SetVisible(float alpha)
        {
            _group.alpha = alpha;
            if (_flameImage != null)
            {
                _flameImage.enabled = alpha > 0f;
            }
        }

        /// <summary>Shows a number. A bigger number than before lands with a violent jolt (scaled by how much bigger).</summary>
        public void SetValue(long value, string text)
        {
            if (value == _value)
            {
                return;
            }
            bool grew = _value >= 0 && value > _value;
            float growth = _value > 0 ? Mathf.Clamp01((float)(value - _value) / _value) : 1f;
            _value = value;
            _text.text = text;
            _shadow.text = text;
            if (grew)
            {
                Punch(0.55f + 0.45f * growth);
            }
        }

        /// <summary>Jolt the number (and flash the box) as if something was slammed into it.</summary>
        public void Punch(float strength)
        {
            _punch = Mathf.Max(_punch, strength);
            _tilt = (Random.value < 0.5f ? -1f : 1f) * Random.Range(6f, 12f);
            _flashAlpha = Mathf.Max(_flashAlpha, 0.55f * strength);
        }

        /// <param name="shake">0 for still numbers; grows with the flame intensity.</param>
        /// <param name="flameIntensity">0 for no flame; 1 reaches the top of the flame area.</param>
        public void Tick(float deltaTime, float time, float shake, double flameIntensity)
        {
            _punch = Mathf.MoveTowards(_punch, 0f, _punch * PunchDecay * deltaTime + 0.2f * deltaTime);
            _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, FlashDecay * deltaTime * Mathf.Max(_flashAlpha, 0.1f));
            _flash.color = new Color(1f, 1f, 1f, _flashAlpha);

            Vector2 offset = new Vector2(0f, TextDrop + Mathf.Sin(time * BobSpeed + _bobPhase) * BobHeight);
            float angle = _tilt * _punch;
            if (shake > 0f)
            {
                offset += Random.insideUnitCircle * (shake * 5f);
                angle += Random.Range(-1f, 1f) * shake * 4f;
            }
            _textRect.anchoredPosition = offset;
            _textRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            _textRect.localScale = Vector3.one * (1f + 0.5f * _punch);
            _body.localScale = Vector3.one * (1f + 0.1f * _punch);

            if (_flame != null)
            {
                _flame.Tick(deltaTime, flameIntensity);
                FitFlame();
            }
        }

        public void ClearFlame()
        {
            _flame?.Clear();
        }

        public void Destroy()
        {
            _flame?.Destroy();
        }

        internal static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, TextAlignmentOptions alignment, Color32 colour)
        {
            RectTransform rect = NewRect(name, parent);
            Stretch(rect);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.alignment = alignment;
            text.color = colour;
            text.enableAutoSizing = true;
            text.fontSizeMax = 46f;
            text.fontSizeMin = 14f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = "0";
            return text;
        }
    }
}
