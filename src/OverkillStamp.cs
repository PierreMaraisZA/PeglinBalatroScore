using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeglinBalatroScore
{
    /// <summary>
    /// A burning stamp slammed above the result when a shot beats the target: OVERKILL in fire colours for damage,
    /// OVERHEAL in heal green when healing past your max HP. The letters are filled with live fire (when the font's
    /// shader supports a face texture), flicker through the flame colours, and have flames licking up behind them.
    /// </summary>
    internal class OverkillStamp
    {
        private const string DamageWord = "OVERKILL!";
        private const string HealWord = "OVERHEAL!";
        private const float StampTime = 0.18f;
        private const float StampFromScale = 2.4f;
        private const float RestAngle = -4f;

        private static readonly int FaceTexId = Shader.PropertyToID("_FaceTex");

        private readonly RectTransform _rect;
        private readonly CanvasGroup _group;
        private readonly TextMeshProUGUI _text;
        private readonly TextMeshProUGUI _shadow;
        private readonly PixelFlame _backFlame;
        private readonly PixelFlame _letterFill;
        private Material _material;
        private FlamePalette _palette = ScoreColours.DamageFlame;
        private float _age = -1f;

        public bool Showing => _age >= 0f;

        public OverkillStamp(Transform parent)
        {
            _rect = ScoreBox.NewRect("Overkill", parent);
            _group = _rect.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _backFlame = new PixelFlame(48, 22, 91, _palette);
            RectTransform flameRect = ScoreBox.NewRect("Flames", _rect);
            // As wide as the stamp, rising from its lower half.
            flameRect.pivot = new Vector2(0.5f, 0f);
            flameRect.anchorMin = new Vector2(0f, 0f);
            flameRect.anchorMax = new Vector2(1f, 0f);
            flameRect.anchoredPosition = new Vector2(0f, 6f);
            flameRect.sizeDelta = new Vector2(0f, 70f);
            RawImage flameImage = flameRect.gameObject.AddComponent<RawImage>();
            flameImage.texture = _backFlame.Texture;
            flameImage.raycastTarget = false;

            _letterFill = new PixelFlame(12, 16, 57, _palette, opaque: true);

            _shadow = NewText("Shadow", _rect, ScoreColours.Outline);
            _shadow.rectTransform.anchoredPosition = new Vector2(4f, -4f);
            _text = NewText("Text", _rect, Color.white);
            _text.enableVertexGradient = true;

            Hide();
        }

        /// <summary>Centre the stamp at a point in the widget, sized to fit the width it's given.</summary>
        public void Layout(Vector2 position, Vector2 size)
        {
            _rect.anchoredPosition = position;
            _rect.sizeDelta = size;
        }

        public void SetFont(TMP_FontAsset font)
        {
            if (font == null || _text.font == font)
            {
                return;
            }
            _text.font = font;
            _shadow.font = font;
            // The material has to belong to the font's atlas, so make the fire-filled copy after switching fonts.
            if (_material != null)
            {
                Object.Destroy(_material);
            }
            _material = new Material(font.material);
            if (_material.HasProperty(FaceTexId))
            {
                _material.SetTexture(FaceTexId, _letterFill.Texture);
                _text.fontSharedMaterial = _material;
            }
        }

        /// <param name="heal">OVERHEAL in heal green rather than OVERKILL in fire colours.</param>
        public void Show(bool heal)
        {
            if (Showing)
            {
                return;
            }
            string word = heal ? HealWord : DamageWord;
            _text.text = word;
            _shadow.text = word;
            _palette = heal ? ScoreColours.HealFlame : ScoreColours.DamageFlame;
            _backFlame.SetPalette(_palette);
            _letterFill.SetPalette(_palette);
            _age = 0f;
            _group.alpha = 1f;
            _backFlame.Clear();
        }

        /// <summary>Fade the stamp along with the result it sits on.</summary>
        public void SetAlpha(float alpha)
        {
            if (Showing)
            {
                _group.alpha = alpha;
            }
        }

        public void Hide()
        {
            _age = -1f;
            _group.alpha = 0f;
        }

        public void Tick(float deltaTime, float time)
        {
            if (!Showing)
            {
                return;
            }
            _age += deltaTime;

            // Slam down from big and tilted, then settle into a slow burning wobble.
            float t = Mathf.Clamp01(_age / StampTime);
            float scale = Mathf.Lerp(StampFromScale, 1f, 1f - (1f - t) * (1f - t));
            float wobble = t >= 1f ? Mathf.Sin(time * 5f) * 2f + Random.Range(-0.6f, 0.6f) : 0f;
            _rect.localScale = Vector3.one * scale;
            _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(RestAngle - 18f, RestAngle, t) + wobble);

            // Flicker the letters through the flame colours, hot at the bottom like the flames below them.
            float flicker = Mathf.PerlinNoise(time * 9f, 0.3f);
            Color32 top = Color32.Lerp(_palette.Bright, _palette.Core, flicker);
            Color32 bottom = Color32.Lerp(_palette.Ember, _palette.Body, Mathf.PerlinNoise(time * 7f, 2.1f));
            _text.colorGradient = new VertexGradient(top, top, bottom, bottom);

            _backFlame.Tick(deltaTime, t >= 1f ? 1.0 : 1.4);
            _letterFill.Tick(deltaTime, 1.1);
        }

        public void Destroy()
        {
            _backFlame.Destroy();
            _letterFill.Destroy();
            if (_material != null)
            {
                Object.Destroy(_material);
            }
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, Color colour)
        {
            RectTransform rect = ScoreBox.NewRect(name, parent);
            ScoreBox.Stretch(rect);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = DamageWord;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMax = 46f;
            text.fontSizeMin = 16f;
            text.color = colour;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
