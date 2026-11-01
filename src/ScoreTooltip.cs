using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeglinBalatroScore
{
    /// <summary>
    /// A parchment-coloured box, like the game's own tooltips, shown while the mouse is over the score. It hangs
    /// below the score, right-aligned with it, so it stays on screen at the right edge.
    /// </summary>
    internal class ScoreTooltip
    {
        private const float Width = 440f;
        private const float Padding = 16f;
        private const float Gap = 10f;

        private static readonly Color32 Paper = new Color32(238, 186, 120, 250);
        private static readonly Color32 Ink = new Color32(62, 39, 35, 255);

        private readonly RectTransform _rect;
        private readonly TextMeshProUGUI _text;
        private string _shown;

        public ScoreTooltip(Transform canvas)
        {
            _rect = ScoreBox.NewRect("ScoreTooltip", canvas);
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(1f, 1f);
            Image paper = _rect.gameObject.AddComponent<Image>();
            paper.color = Paper;
            paper.raycastTarget = false;
            Outline border = _rect.gameObject.AddComponent<Outline>();
            border.effectColor = Ink;
            border.effectDistance = new Vector2(3f, -3f);

            RectTransform textRect = ScoreBox.NewRect("Text", _rect);
            ScoreBox.Stretch(textRect);
            textRect.offsetMin = new Vector2(Padding, Padding);
            textRect.offsetMax = new Vector2(-Padding, -Padding);
            _text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            _text.fontSize = 22f;
            _text.color = Ink;
            _text.enableWordWrapping = true;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.richText = true;
            _text.raycastTarget = false;

            Hide();
        }

        public void SetFont(TMP_FontAsset font)
        {
            if (font != null)
            {
                _text.font = font;
                _shown = null;
            }
        }

        /// <summary>Show the text below a box given by its corners in canvas space, lined up with its right edge.</summary>
        public void Show(string text, Vector2 anchorBottomRight, float canvasBottom)
        {
            if (!_rect.gameObject.activeSelf)
            {
                _rect.gameObject.SetActive(true);
            }
            if (text != _shown)
            {
                _shown = text;
                _text.text = text;
                float height = _text.GetPreferredValues(text, Width - 2f * Padding, 0f).y + 2f * Padding;
                _rect.sizeDelta = new Vector2(Width, height);
            }
            Vector2 position = anchorBottomRight + new Vector2(0f, -Gap);
            // Keep the whole box on screen if the score sits low.
            position.y = Mathf.Max(position.y, canvasBottom + _rect.sizeDelta.y + Gap);
            _rect.anchoredPosition = position;
            _rect.SetAsLastSibling();
        }

        public void Hide()
        {
            if (_rect.gameObject.activeSelf)
            {
                _rect.gameObject.SetActive(false);
            }
        }
    }
}
