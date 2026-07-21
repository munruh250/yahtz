using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Yahtzee.Presentation
{
    /// <summary>Marks a physical object in the kitchen as tappable, so interaction stays diegetic
    /// — you reach out and touch the thing rather than hunting for a button. Currently Oma's
    /// scorecard, which is how you peek at her scores.</summary>
    public sealed class TappableProp : MonoBehaviour
    {
        private Action _onTapped;

        public void Init(Action onTapped) => _onTapped = onTapped;

        public void Tap() => _onTapped?.Invoke();
    }

    /// <summary>The single router for taps that land on the table — dice and props both.
    ///
    /// It has to be one router, not two: the dice and Oma's card overlap on screen, so when
    /// each had its own raycast a tap through a die into her card behind it fired *both*,
    /// keeping a die and flipping to her scorecard at once. Now the nearest hit wins and a tap
    /// does exactly one thing.</summary>
    public sealed class WorldTapInput : MonoBehaviour
    {
        private const float PickDistance = 8f;

        private Camera _camera;
        private GameController _controller;
        private Func<bool> _enabled;

        public void Init(Camera camera, GameController controller, Func<bool> enabled)
        {
            _camera = camera;
            _controller = controller;
            _enabled = enabled;
        }

        private void Update()
        {
            if (_camera == null || !Input.GetMouseButtonDown(0) || !_enabled())
                return;
            if (IsPointerOverUi())
                return;
            DispatchTap(Input.mousePosition);
        }

        /// <summary>Whatever is nearest under the point gets the tap: a die is kept/released, a
        /// prop is activated. Public so tests can drive the real pick path.</summary>
        public void DispatchTap(Vector3 screenPoint)
        {
            var hits = Physics.RaycastAll(_camera.ScreenPointToRay(screenPoint), PickDistance);
            float nearest = float.MaxValue;
            Die3D die = null;
            TappableProp prop = null;

            foreach (var hit in hits)
            {
                if (hit.distance >= nearest)
                    continue;
                // Every hit is considered, not just the first: the invisible fence stands between
                // the camera and the table, so a first-hit raycast just reports a wall.
                var hitDie = hit.collider.GetComponent<Die3D>();
                var hitProp = hit.collider.GetComponent<TappableProp>();
                if (hitDie == null && hitProp == null)
                    continue;
                nearest = hit.distance;
                die = hitDie;
                prop = hitProp;
            }

            if (die != null)
                _controller.OnDieTapped(die.Index);
            else
                prop?.Tap();
        }

        /// <summary>Nearest tappable prop under a screen point. Considers every hit along the ray
        /// for the same reason the dice pick does: the invisible fence sits between the camera
        /// and the table, so a first-hit raycast would just report a wall.</summary>
        public TappableProp PropAtScreenPoint(Vector3 screenPoint)
        {
            var hits = Physics.RaycastAll(_camera.ScreenPointToRay(screenPoint), PickDistance);
            TappableProp nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (var hit in hits)
            {
                var prop = hit.collider.GetComponent<TappableProp>();
                if (prop == null || hit.distance >= nearestDistance)
                    continue;
                nearestDistance = hit.distance;
                nearest = prop;
            }
            return nearest;
        }

        private static bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
                return false;
            return Input.touchCount > 0
                ? EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
                : EventSystem.current.IsPointerOverGameObject();
        }
    }
}
