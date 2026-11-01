using PeglinBalatroScore.Core;
using UnityEngine;

namespace PeglinBalatroScore
{
    /// <summary>Colours for a flame, from its coolest visible pixels to the white-hot core.</summary>
    internal struct FlamePalette
    {
        public Color32 Ember;
        public Color32 Body;
        public Color32 Bright;
        public Color32 Core;

        public FlamePalette(Color32 ember, Color32 body, Color32 bright, Color32 core)
        {
            Ember = ember;
            Body = body;
            Bright = bright;
            Core = core;
        }
    }

    /// <summary>
    /// A FireSimulation drawn into a small point-filtered texture, so it scales up as chunky pixel-art fire. Runs at a
    /// fixed rate in real time, so the flames look the same at any game speed or frame rate.
    /// </summary>
    internal class PixelFlame
    {
        private const float StepsPerSecond = 30f;
        private const int Shades = 7;

        /// <summary>Heat below this is drawn as nothing (transparent flames) or the darkest shade (opaque fill).</summary>
        private const float VisibleHeat = 0.1f;

        /// <summary>Head room added on top of what the intensity needs, so a growing flame isn't cut off at the top.</summary>
        private const double GrowHeadroom = 1.15;

        private readonly Color32[] _shades = new Color32[Shades];
        private readonly bool _opaque;
        private readonly bool _growable;
        private readonly int _baseRows;
        private readonly int _seed;
        private FireSimulation _fire;
        private Texture2D _texture;
        private Color32[] _pixels;
        private float _stepTimer;
        private int _coldSteps;

        /// <summary>The texture being drawn into. Replaced when a growable flame gets taller, so read it each frame.</summary>
        public Texture Texture => _texture;

        /// <summary>How many times taller than its base height the fire area is right now (1 unless it has grown).</summary>
        public int HeightScale => _fire.Height / _baseRows;

        /// <param name="opaque">Fill every pixel (for colouring text) instead of leaving cool air transparent.</param>
        /// <param name="growable">
        /// Add rows when the intensity climbs past 1, so the flame can keep rising (off the screen if need be) with
        /// pixels the same size, and drop back to the base height once the fire has died down.
        /// </param>
        public PixelFlame(int width, int height, int seed, FlamePalette palette, bool opaque = false, bool growable = false)
        {
            _baseRows = height;
            _seed = seed;
            _opaque = opaque;
            _growable = growable;
            Build(width, 1, null);
            SetPalette(palette);
            Draw();
        }

        private void Build(int width, int scale, FireSimulation carryOver)
        {
            if (_texture != null)
            {
                Object.Destroy(_texture);
            }
            int rows = _baseRows * scale;
            _fire = new FireSimulation(width, rows, _seed + scale, _baseRows);
            if (carryOver != null)
            {
                _fire.CopyFrom(carryOver);
            }
            _texture = new Texture2D(width, rows, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PeglinBalatroScoreFlame",
            };
            _pixels = new Color32[width * rows];
        }

        public void SetPalette(FlamePalette palette)
        {
            // A few flat shades rather than a smooth gradient, to match the game's pixel art.
            Color32[] stops = { palette.Ember, palette.Body, palette.Bright, palette.Core };
            for (int i = 0; i < Shades; i++)
            {
                float t = (float)i / (Shades - 1) * (stops.Length - 1);
                int stop = Mathf.Min((int)t, stops.Length - 2);
                _shades[i] = Color32.Lerp(stops[stop], stops[stop + 1], t - stop);
            }
            _coldSteps = 0;
        }

        /// <summary>Advance by real time and redraw. Does nothing once the fire has been out for a while.</summary>
        public void Tick(float unscaledDeltaTime, double intensity)
        {
            _stepTimer += unscaledDeltaTime;
            if (_stepTimer < 1f / StepsPerSecond)
            {
                return;
            }
            _stepTimer = Mathf.Min(_stepTimer - 1f / StepsPerSecond, 1f / StepsPerSecond);

            _coldSteps = intensity > 0 ? 0 : _coldSteps + 1;
            if (_coldSteps > _fire.Height + 1)
            {
                if (_growable && HeightScale > 1)
                {
                    // Out cold: shrink back so the next shot doesn't carry a huge empty texture around.
                    Build(_fire.Width, 1, null);
                    Draw();
                }
                return;
            }
            if (_growable)
            {
                int wantedScale = Mathf.Max(1, Mathf.CeilToInt((float)(intensity * GrowHeadroom)));
                if (wantedScale > HeightScale)
                {
                    Build(_fire.Width, wantedScale, _fire);
                }
            }
            _fire.Step(intensity);
            Draw();
        }

        public void Clear()
        {
            _fire.Clear();
            _coldSteps = 0;
            Draw();
        }

        public void Destroy()
        {
            Object.Destroy(_texture);
        }

        private void Draw()
        {
            for (int y = 0; y < _fire.Height; y++)
            {
                for (int x = 0; x < _fire.Width; x++)
                {
                    float heat = _fire[x, y];
                    Color32 colour;
                    if (heat < VisibleHeat)
                    {
                        colour = _opaque ? _shades[0] : new Color32(0, 0, 0, 0);
                    }
                    else
                    {
                        int shade = Mathf.Clamp((int)((heat - VisibleHeat) / (1f - VisibleHeat) * Shades), 0, Shades - 1);
                        colour = _shades[shade];
                    }
                    // Texture rows run bottom to top, the same way round as the simulation.
                    _pixels[y * _fire.Width + x] = colour;
                }
            }
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }
    }
}
