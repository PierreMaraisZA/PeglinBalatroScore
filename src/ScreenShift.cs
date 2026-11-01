using UnityEngine;

namespace PeglinBalatroScore
{
    /// <summary>
    /// Moves one of the game's objects down the screen by a number of pixels, and puts it back when the shift returns
    /// to 0. Works whether the object is on an overlay canvas, a camera canvas or in the world. If the game moves the
    /// object itself (an animation, say), that new spot becomes the base the shift is applied to.
    /// </summary>
    internal class ScreenShift
    {
        private readonly Transform _target;
        private Vector3 _base;
        private Vector3 _lastSet;
        private bool _hasBase;

        public ScreenShift(Transform target)
        {
            _target = target;
        }

        public bool Alive => _target != null;

        public Transform Target => _target;

        /// <param name="pixels">How far below its own spot the object should sit, in screen pixels.</param>
        public void Apply(float pixels)
        {
            if (_target == null)
            {
                return;
            }
            Vector3 current = _target.position;
            if (!_hasBase || current != _lastSet)
            {
                _base = current;
                _hasBase = true;
            }
            Vector3 wanted = pixels <= 0f ? _base : ShiftedDown(_base, pixels);
            _target.position = wanted;
            _lastSet = wanted;
        }

        /// <summary>Back to where the game put it.</summary>
        public void Restore()
        {
            Apply(0f);
        }

        private Vector3 ShiftedDown(Vector3 world, float pixels)
        {
            Camera camera = CameraFor(_target);
            if (camera == null)
            {
                // Overlay canvas: world space is screen pixels.
                return world - new Vector3(0f, pixels, 0f);
            }
            Vector3 screen = camera.WorldToScreenPoint(world);
            screen.y -= pixels;
            return camera.ScreenToWorldPoint(screen);
        }

        /// <summary>The camera that draws this object, or null for an overlay canvas.</summary>
        internal static Camera CameraFor(Transform transform)
        {
            Canvas canvas = transform.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return null;
                }
                return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }
            return Camera.main;
        }
    }
}
